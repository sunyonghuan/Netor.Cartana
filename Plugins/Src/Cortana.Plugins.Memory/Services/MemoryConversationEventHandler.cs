using System.Text;
using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Storage;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// 处理 PluginBus conversation topic 的历史批次和实时事件入库。
/// </summary>
public sealed class MemoryConversationEventHandler(
    PluginSettings settings,
    ILogger<MemoryConversationEventHandler> logger,
    IMemoryStore store)
{
    private readonly Dictionary<string, AssistantStreamBuffer> _assistantStreams = new(StringComparer.Ordinal);

    /// <summary>
    /// 尝试处理 conversation 事件或历史回放响应。
    /// </summary>
    /// <param name="root">PluginBus 消息根元素。</param>
    /// <param name="op">消息操作名。</param>
    /// <returns>消息已处理时返回 <see langword="true"/>。</returns>
    public bool TryHandle(JsonElement root, string? op)
    {
        var eventType = root.TryGetProperty("eventType", out var eventTypeElement) ? eventTypeElement.GetString() : null;
        if (string.Equals(eventType, "conversation.history.batch", StringComparison.Ordinal)
            || string.Equals(op, "conversation.history.batch", StringComparison.Ordinal))
        {
            if (root.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
            {
                IngestExportBatch(payload);
            }

            return true;
        }

        if (string.Equals(eventType, "conversation.history.completed", StringComparison.Ordinal)
            || string.Equals(op, "conversation.history.completed", StringComparison.Ordinal))
        {
            return true;
        }

        if (root.TryGetProperty("payload", out var live) && live.ValueKind == JsonValueKind.Object)
        {
            TryIngestLiveEvent(eventType, live);
            return true;
        }

        return false;
    }

    private void IngestExportBatch(JsonElement payload)
    {
        if (!payload.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return;

        var count = 0;
        var list = new List<ObservationRecord>();
        foreach (var it in items.EnumerateArray())
        {
            var r = new ObservationRecord
            {
                Id = it.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty,
                AgentId = it.TryGetProperty("agentId", out var agEl) ? agEl.GetString() : null,
                AgentName = GetString(it, "agentName", "AgentName"),
                WorkspaceId = ResolveWorkspaceId(it),
                SessionId = it.TryGetProperty("sessionId", out var sidEl) ? sidEl.GetString() ?? string.Empty : string.Empty,
                TurnId = it.TryGetProperty("turnId", out var tEl) ? tEl.GetString() : null,
                MessageId = it.TryGetProperty("messageId", out var midEl) ? midEl.GetString() : null,
                EventType = it.TryGetProperty("eventType", out var etEl) ? etEl.GetString() : (it.TryGetProperty("type", out var t2) ? t2.GetString() : null),
                Role = it.TryGetProperty("role", out var roleEl) ? (roleEl.GetString() ?? string.Empty) : string.Empty,
                Content = it.TryGetProperty("content", out var cEl) && cEl.ValueKind != JsonValueKind.Null ? cEl.GetString() : null,
                AttachmentsJson = ResolveAttachments(it),
                CreatedTimestamp = it.TryGetProperty("createdTimestamp", out var tsEl) ? tsEl.GetInt64() : (it.TryGetProperty("timestamp", out var ts2) && ts2.ValueKind == JsonValueKind.Number ? ts2.GetInt64() : 0L),
                ModelName = it.TryGetProperty("modelName", out var mEl) && mEl.ValueKind != JsonValueKind.Null ? mEl.GetString() : null,
                TraceId = it.TryGetProperty("traceId", out var trEl) ? trEl.GetString() : (it.TryGetProperty("TraceId", out trEl) ? trEl.GetString() : null),
                SourceFactsJson = it.GetRawText(),
                CreatedAt = ToIsoTime(it.TryGetProperty("createdTimestamp", out var createdEl) && createdEl.ValueKind == JsonValueKind.Number ? createdEl.GetInt64() : 0L)
            };
            list.Add(r);
            count++;
        }
        store.BulkInsertObservations(list);
        logger.LogInformation("导入历史批次 {Count} 条", count);
    }

    private void TryIngestLiveEvent(string? eventType, JsonElement live)
    {
        string id;
        string sessionId = GetString(live, "sessionId", "SessionId") ?? string.Empty;
        string role;
        string? content = null;
        long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string? model = GetString(live, "modelName", "ModelName", "modelId", "ModelId");

        if (string.Equals(eventType, "conversation.user.message", StringComparison.Ordinal))
        {
            role = "user";
            id = GetString(live, "userMessageId", "UserMessageId") ?? Guid.NewGuid().ToString("N");
            content = GetString(live, "content", "Content");
        }
        else if (string.Equals(eventType, "conversation.assistant.delta", StringComparison.Ordinal))
        {
            BufferAssistantDelta(live);
            return;
        }
        else if (string.Equals(eventType, "conversation.turn.completed", StringComparison.Ordinal))
        {
            role = "assistant";
            id = GetString(live, "assistantMessageId", "AssistantMessageId") ?? Guid.NewGuid().ToString("N");
            if (!IsSuccessfulTurnCompletion(live))
            {
                CompleteAssistantStream(live);
                return;
            }

            content = GetString(live, "assistantResponse", "AssistantResponse", "content", "Content") ?? CompleteAssistantStream(live);
            if (string.IsNullOrWhiteSpace(content)) return;
        }
        else
        {
            return;
        }

        string? agentId = GetString(live, "agentId", "AgentId");
        string? agentName = GetString(live, "agentName", "AgentName");
        string? turnId = GetString(live, "turnId", "TurnId");
        string? messageId = null;
        if (string.Equals(eventType, "conversation.user.message", StringComparison.Ordinal))
            messageId = GetString(live, "userMessageId", "UserMessageId");
        else if (string.Equals(eventType, "conversation.turn.completed", StringComparison.Ordinal))
            messageId = GetString(live, "assistantMessageId", "AssistantMessageId");

        var createdTimestamp = GetInt64(live, "occurredAt", "createdTimestamp", "timestamp") ?? ts;
        var record = new ObservationRecord
        {
            Id = id,
            AgentId = agentId,
            AgentName = agentName,
            WorkspaceId = ResolveWorkspaceId(live),
            SessionId = sessionId,
            TurnId = turnId,
            MessageId = messageId,
            EventType = eventType,
            Role = role,
            Content = content,
            AttachmentsJson = ResolveAttachments(live),
            CreatedTimestamp = createdTimestamp,
            ModelName = model,
            TraceId = GetString(live, "traceId", "TraceId"),
            SourceFactsJson = live.GetRawText(),
            CreatedAt = ToIsoTime(createdTimestamp)
        };

        store.InsertObservation(record);
    }

    private void BufferAssistantDelta(JsonElement live)
    {
        var messageId = GetString(live, "assistantMessageId", "AssistantMessageId");
        var delta = GetString(live, "delta", "Delta", "content", "Content");
        if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(delta)) return;

        var buffer = GetOrCreateAssistantStream(messageId);
        buffer.Append(delta);
    }

    private string? CompleteAssistantStream(JsonElement live)
    {
        var messageId = GetString(live, "assistantMessageId", "AssistantMessageId");
        if (string.IsNullOrEmpty(messageId)) return null;
        if (!_assistantStreams.Remove(messageId, out var buffer)) return null;
        return buffer.ToString();
    }

    private AssistantStreamBuffer GetOrCreateAssistantStream(string messageId)
    {
        if (_assistantStreams.TryGetValue(messageId, out var buffer)) return buffer;

        buffer = new AssistantStreamBuffer();
        _assistantStreams[messageId] = buffer;
        return buffer;
    }

    private static bool IsSuccessfulTurnCompletion(JsonElement live)
    {
        var status = GetString(live, "status", "Status");
        return string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "0", StringComparison.Ordinal);
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

    private static string ResolveAttachments(JsonElement el)
    {
        if (el.TryGetProperty("attachments", out var a) || el.TryGetProperty("Attachments", out a) || el.TryGetProperty("assets", out a) || el.TryGetProperty("Assets", out a))
        {
            if (a.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                return a.GetRawText();
        }
        return "[]";
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

    private sealed class AssistantStreamBuffer
    {
        private readonly StringBuilder _content = new();

        public void Append(string delta) => _content.Append(delta);

        public override string ToString() => _content.ToString();
    }
}
