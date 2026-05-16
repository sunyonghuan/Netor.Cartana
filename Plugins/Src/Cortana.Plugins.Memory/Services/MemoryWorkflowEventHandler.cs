using System.Text;
using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Storage;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// 处理 PluginBus workflow topic 的实时事件和历史批次入库。
///
/// 阶段 4B 引入（决策 5-B / 决策 4-A，详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §4B.4 + §4B.5）。
///
/// 入库策略（[03] §12.3 + [07] §4.3）：
/// - <c>workflow.task.completed</c> 且 status == "completed" → 写入 ObservationRecord（FinalReport 进 Content）
/// - <c>workflow.history.batch</c> / <c>workflow.history.completed</c> → 历史回放批次，逐条入库
/// - 其他 workflow 事件（started / failed / cancelled / step.* / title.updated） → 忽略 + LogDebug
///
/// 与 <see cref="MemoryConversationEventHandler"/> 对称：构造参数与 helper 方法签名保持一致，方便后续抽象成基类。
/// </summary>
public sealed class MemoryWorkflowEventHandler(
    PluginSettings settings,
    ILogger<MemoryWorkflowEventHandler> logger,
    IMemoryStore store)
{
    /// <summary>
    /// 尝试处理 workflow 事件或历史回放响应。
    /// </summary>
    /// <param name="root">PluginBus 消息根元素（已 JsonDocument.Parse）。</param>
    /// <param name="op">消息操作名（如 <c>workflow.event.publish</c> / <c>workflow.history.batch</c>）。</param>
    /// <returns>消息已被本 handler 处理时返回 <see langword="true"/>。</returns>
    public bool TryHandle(JsonElement root, string? op)
    {
        var eventType = root.TryGetProperty("eventType", out var eventTypeElement) ? eventTypeElement.GetString() : null;

        // 1) 历史回放批次（决策 4-A：Task 级粒度）
        if (string.Equals(eventType, "workflow.history.batch", StringComparison.Ordinal)
            || string.Equals(op, "workflow.history.batch", StringComparison.Ordinal))
        {
            if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                IngestExportBatch(payload);
            }

            return true;
        }

        // 2) 历史回放结束信号
        if (string.Equals(eventType, "workflow.history.completed", StringComparison.Ordinal)
            || string.Equals(op, "workflow.history.completed", StringComparison.Ordinal))
        {
            return true;
        }

        // 3) 实时 workflow 事件路由
        if (root.TryGetProperty("payload", out var live) && live.ValueKind == JsonValueKind.Object)
        {
            return TryIngestLiveEvent(eventType, live);
        }

        return false;
    }

    /// <summary>
    /// 摄入历史回放批次（workflow.history.batch）。
    /// 每条 <see cref="WorkflowExportRecord"/> 都视作一个完成的任务，仅当 Status == "completed" 时入库
    /// （与决策 5-B 保持一致：仅 FinalReport 进长期记忆，失败 / 取消任务不污染记忆库）。
    /// </summary>
    private void IngestExportBatch(JsonElement payload)
    {
        if (!payload.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return;

        var count = 0;
        var skipped = 0;
        var skippedByOwner = 0;
        var list = new List<ObservationRecord>();
        foreach (var it in items.EnumerateArray())
        {
            var status = GetString(it, "status", "Status");
            if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            // 阶段 6 Phase 4：历史回放也检查 owner 配置位（决策 6-4-A 修订）。
            // 历史导出记录可能由旧版本生成（无 AllowMemoryIngest 字段），IsMemoryIngestAllowed 默认返回 true
            // 保持向后兼容（决策 6-4-B：default true）。
            if (!IsMemoryIngestAllowed(it))
            {
                skippedByOwner++;
                continue;
            }

            var record = BuildRecordFromTaskCompleted(it, eventType: "workflow.task.completed");
            if (record is not null)
            {
                list.Add(record);
                count++;
            }
        }

        if (list.Count > 0)
        {
            store.BulkInsertObservations(list);
        }
        logger.LogInformation(
            "Workflow 历史批次：入库 {Count} 条，跳过 {Skipped} 条（非 completed），跳过 {SkippedByOwner} 条（owner 不允许）",
            count, skipped, skippedByOwner);
    }

    /// <summary>
    /// 摄入实时 workflow 事件。返回 <c>true</c> 表示已识别（不论是否入库）。
    /// 仅 <c>workflow.task.completed</c> 且 status == "completed" 时入库；其他事件返回 true 但不入库（避免噪音）。
    /// </summary>
    private bool TryIngestLiveEvent(string? eventType, JsonElement live)
    {
        if (string.IsNullOrEmpty(eventType) || !eventType.StartsWith("workflow.", StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(eventType, "workflow.task.completed", StringComparison.Ordinal))
        {
            // 其他 workflow 事件（started / failed / cancelled / step.* / title.updated）按 [03] §12.3 不入库
            logger.LogDebug("Workflow 事件被忽略（非 task.completed）：{EventType}", eventType);
            return true;
        }

        // host 端 WorkflowTaskCompletedArgs 由 OperationCanceledException 触发的失败 / 取消路径
        // 走的是 OnWorkflowTaskFailed 事件，不会走到 OnWorkflowTaskCompleted。
        // 但为防御性地处理意外字段污染，这里仍校验 payload 是否含 Status="completed"（若有）。
        var statusFromPayload = GetString(live, "status", "Status");
        if (!string.IsNullOrEmpty(statusFromPayload)
            && !string.Equals(statusFromPayload, "completed", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Workflow 事件 task.completed 但 payload.Status={Status}，跳过入库", statusFromPayload);
            return true;
        }

        // 阶段 6 Phase 4：检查 owner 长期记忆 ingest 开关（决策 6-4-A 修订）。
        // host 端在发布事件前已查 Manager.AllowWorkflowMemory 填充 AllowMemoryIngest；
        // false 时本插件丢弃 ingest 副作用（事件正常发，其他订阅者如 UI 任务列表 / 详情面板 / PluginBus relay 不受影响）。
        // 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §阶段 6 #5。
        if (!IsMemoryIngestAllowed(live))
        {
            logger.LogDebug(
                "Workflow 事件 task.completed 被 owner 配置阻止入长期记忆：TaskId={TaskId}（Agent.AllowWorkflowMemory=false）",
                GetString(live, "taskId", "TaskId"));
            return true;
        }

        var record = BuildRecordFromTaskCompleted(live, eventType);
        if (record is not null)
        {
            store.InsertObservation(record);
            logger.LogInformation("Workflow 任务已入记忆：{TaskId}", record.Id);
        }
        return true;
    }

    /// <summary>
    /// 从 task.completed 事件 payload / WorkflowExportRecord 构造 <see cref="ObservationRecord"/>。
    /// 字段映射详见 docs/未来版本策划/多智能体编排模式策划/07-事件分流与插件兼容设计.md §4.3。
    /// </summary>
    /// <returns>合法时返回 record；若 taskId / FinalReport 都缺失则返回 null。</returns>
    private ObservationRecord? BuildRecordFromTaskCompleted(JsonElement payload, string eventType)
    {
        // taskId 是必需字段；WorkflowExportRecord 用 "taskId"，task.completed 事件 args 用 "TaskId"（PascalCase）
        var taskId = GetString(payload, "taskId", "TaskId");
        if (string.IsNullOrWhiteSpace(taskId))
        {
            logger.LogDebug("Workflow {EventType} 缺少 taskId，跳过入库", eventType);
            return null;
        }

        var finalReport = GetString(payload, "finalReport", "FinalReport");
        if (string.IsNullOrWhiteSpace(finalReport))
        {
            logger.LogDebug("Workflow {EventType} 任务 {TaskId} 无 FinalReport，跳过入库", eventType, taskId);
            return null;
        }

        var completedAt = GetInt64(payload, "completedAt", "CompletedAt", "occurredAt", "OccurredAt");
        var createdTimestamp = completedAt ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new ObservationRecord
        {
            Id = taskId,
            AgentId = GetString(payload, "managerAgentId", "ManagerAgentId"),
            AgentName = GetString(payload, "managerAgentName", "ManagerAgentName"),
            WorkspaceId = ResolveWorkspaceId(payload),
            SessionId = GetString(payload, "sourceSessionId", "SourceSessionId") ?? string.Empty,
            TurnId = null,                       // workflow 任务无 turn 概念
            MessageId = taskId,                  // 与 Id 同源，便于 dedup
            EventType = eventType,               // 固定 "workflow.task.completed"
            Role = "assistant",                  // FinalReport 视作 assistant 消息
            Content = finalReport,
            AttachmentsJson = "[]",              // workflow 任务暂不携带附件
            CreatedTimestamp = createdTimestamp,
            ModelName = null,                    // 多模型协作场景，单条记录无法对应单一模型
            TraceId = GetString(payload, "traceId", "TraceId"),
            SourceFactsJson = payload.GetRawText(),
            CreatedAt = ToIsoTime(createdTimestamp),
        };
    }

    // ────────────────────────────────────────────────────────────────────
    // 与 MemoryConversationEventHandler 对称的工具方法（小型重复，保持手感一致；
    // 阶段 5B+ 若引入 IMemoryEventHandlerBase 抽象时再抽公共基类）
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 阶段 6 Phase 4：判断 task.completed 事件 payload 中 owner 是否允许该结果入长期记忆（决策 6-4-A 修订）。
    /// 字段名兼容 PascalCase（CLR record 默认） + camelCase（JSON 规范化），缺失时默认 true（决策 6-4-B 向后兼容）。
    /// 历史 export 记录可能由旧版本生成无该字段，返回 true 避免破坏既有入库行为。
    /// </summary>
    private static bool IsMemoryIngestAllowed(JsonElement payload)
    {
        foreach (var name in new[] { "allowMemoryIngest", "AllowMemoryIngest" })
        {
            if (!payload.TryGetProperty(name, out var v)) continue;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(v.GetString(), out var b) => b,
                JsonValueKind.Number when v.TryGetInt32(out var n) => n != 0,
                _ => true,
            };
        }
        return true;   // 决策 6-4-B：字段缺失 → 默认允许（旧版本 / 4B 入库行为）
    }

    private string? ResolveWorkspaceId(JsonElement el)
    {
        if (el.TryGetProperty("workspaceId", out var workspace) || el.TryGetProperty("WorkspaceId", out workspace))
        {
            var value = workspace.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return IsHexHash(value) ? value : Md5Hash(value);
            }
        }

        return string.IsNullOrWhiteSpace(settings.WorkspaceDirectory) ? null : Md5Hash(settings.WorkspaceDirectory);
    }

    private static bool IsHexHash(string value)
    {
        return value.Length == 32 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
    }

    private static string Md5Hash(string input)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    private static string ToIsoTime(long timestamp)
    {
        return timestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("O")
            : DateTimeOffset.UtcNow.ToString("O");
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            }
        }

        return null;
    }

    private static long? GetInt64(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number)) return number;
        }

        return null;
    }
}
