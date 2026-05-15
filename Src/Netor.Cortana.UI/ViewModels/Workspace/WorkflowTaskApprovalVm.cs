using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

using Netor.Cortana.AI.Workflow;
using Netor.Cortana.Entitys;

namespace Netor.Cortana.UI.ViewModels.Workspace;

/// <summary>
/// 阶段 5B：Workflow HITL（人在回路）批准卡片的 ViewModel。
///
/// 职责：
/// - 接收来自 <see cref="WorkflowTaskPausedArgs"/> 的暂停详情（PauseReason + RequestId + RequestPayloadJson）
/// - 解析 RequestPayloadJson 渲染计划详情（Plan / IsStalled / Progress 摘要）
/// - 提供三个操作：批准 / 修改建议 / 拒绝（取消任务）
/// - 通过 <see cref="IWorkflowExecutor.ResumeAsync"/> 回写用户响应
///
/// 决策 5B-A（API 选型）：使用 SDK StreamingRun.SendResponseAsync 单响应模式。
/// 决策 5B-B（拒绝路径）：rejected 触发 OperationCanceledException 走 HandleTaskCancelled。
///
/// 详见：docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.1。
/// </summary>
public sealed class WorkflowTaskApprovalVm : INotifyPropertyChanged
{
    private readonly IWorkflowExecutor _executor;

    private string _taskId = string.Empty;
    private string _requestId = string.Empty;
    private string _pauseReason = string.Empty;
    private string _planText = string.Empty;
    private bool _isStalled;
    private string _progressSummary = string.Empty;
    private string _revisionInput = string.Empty;
    private bool _isSubmitting;
    private bool _isVisible;

    public WorkflowTaskApprovalVm()
    {
        _executor = App.Services.GetRequiredService<IWorkflowExecutor>();
    }

    /// <summary>关联的任务 ID（用于 ResumeAsync 配对）。</summary>
    public string TaskId
    {
        get => _taskId;
        private set => SetField(ref _taskId, value);
    }

    /// <summary>SDK ExternalRequest.RequestId（防止旧响应误生效）。</summary>
    public string RequestId
    {
        get => _requestId;
        private set => SetField(ref _requestId, value);
    }

    /// <summary>暂停原因（如 "magentic.plan.signoff"）。</summary>
    public string PauseReason
    {
        get => _pauseReason;
        private set
        {
            if (SetField(ref _pauseReason, value))
                OnPropertyChanged(nameof(PauseReasonText));
        }
    }

    /// <summary>UI 友好文本，用于卡片顶部 banner。</summary>
    public string PauseReasonText => _pauseReason switch
    {
        "magentic.plan.signoff" => "Magentic 规划完成，请审阅并批准",
        "external.request" => "工作流请求外部输入",
        _ => "工作流正在等待人工响应"
    };

    /// <summary>计划详情文本（Markdown 风格，View 层用 SelectableTextBlock 展示）。</summary>
    public string PlanText
    {
        get => _planText;
        private set => SetField(ref _planText, value);
    }

    /// <summary>是否已进入 stall 状态（连续无进展）。</summary>
    public bool IsStalled
    {
        get => _isStalled;
        private set
        {
            if (SetField(ref _isStalled, value))
                OnPropertyChanged(nameof(StallWarning));
        }
    }

    /// <summary>失速警告文本（仅当 IsStalled 时显示）。</summary>
    public string StallWarning => _isStalled
        ? "⚠️ 当前任务已多次重规划仍未取得进展，请评估是否继续。"
        : string.Empty;

    /// <summary>当前进度摘要（来自 MagenticProgressLedger）。</summary>
    public string ProgressSummary
    {
        get => _progressSummary;
        private set => SetField(ref _progressSummary, value);
    }

    /// <summary>用户输入的修改建议（仅当点击 [提交修改] 时使用）。</summary>
    public string RevisionInput
    {
        get => _revisionInput;
        set => SetField(ref _revisionInput, value);
    }

    /// <summary>是否正在提交响应（按钮置灰防双击）。</summary>
    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            if (SetField(ref _isSubmitting, value))
                OnPropertyChanged(nameof(IsInteractive));
        }
    }

    /// <summary>是否可交互（IsSubmitting 反过来）。</summary>
    public bool IsInteractive => !_isSubmitting;

    /// <summary>整个卡片是否可见（绑定到 View 的 IsVisible）。</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// 从 <see cref="WorkflowTaskPausedArgs"/> 装载卡片。
    /// 由 <c>TaskDetailVm</c> 在收到 OnWorkflowTaskPaused 事件时调用。
    /// </summary>
    public void Load(WorkflowTaskPausedArgs args)
    {
        TaskId = args.TaskId;
        RequestId = args.RequestId;
        PauseReason = args.PauseReason;

        ParsePayload(args.RequestPayloadJson);
        IsVisible = true;
    }

    /// <summary>清空卡片状态（任务恢复或卸载时调用）。</summary>
    public void Clear()
    {
        TaskId = string.Empty;
        RequestId = string.Empty;
        PauseReason = string.Empty;
        PlanText = string.Empty;
        ProgressSummary = string.Empty;
        RevisionInput = string.Empty;
        IsStalled = false;
        IsSubmitting = false;
        IsVisible = false;
    }

    /// <summary>批准计划：发空 ChatMessage 列表给 SDK，Magentic 视为通过。</summary>
    public async Task<bool> ApproveAsync(CancellationToken ct)
    {
        return await SubmitResponseAsync("approved", revision: null, ct);
    }

    /// <summary>提交修改建议：把 <see cref="RevisionInput"/> 包装为 ChatMessage 发回 SDK。</summary>
    public async Task<bool> SubmitRevisionAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_revisionInput))
        {
            return false;
        }
        var msg = new ChatMessage(ChatRole.User, _revisionInput.Trim());
        return await SubmitResponseAsync("revised", revision: [msg], ct);
    }

    /// <summary>拒绝计划：等价于取消整个任务（走 HandleTaskCancelled）。</summary>
    public async Task<bool> RejectAsync(CancellationToken ct)
    {
        return await SubmitResponseAsync("rejected", revision: null, ct);
    }

    private async Task<bool> SubmitResponseAsync(
        string action,
        IReadOnlyList<ChatMessage>? revision,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_taskId) || string.IsNullOrEmpty(_requestId))
        {
            return false;
        }

        IsSubmitting = true;
        try
        {
            var ok = await _executor.ResumeAsync(_taskId, _requestId, action, revision, ct);
            if (ok)
            {
                Clear();
            }
            return ok;
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>
    /// 解析 <see cref="WorkflowTaskPausedArgs.RequestPayloadJson"/>（host 端手动构造的简化 JSON）。
    /// Magentic plan signoff 的字段：plan / isStalled / isStarted / isRequestSatisfied / isInLoop /
    /// isProgressBeingMade / nextSpeaker / instructionOrQuestion。
    /// </summary>
    private void ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            PlanText = "(无计划详情)";
            ProgressSummary = string.Empty;
            IsStalled = false;
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            PlanText = root.TryGetProperty("plan", out var planEl) ? (planEl.GetString() ?? "(空计划)") : "(无 plan 字段)";
            IsStalled = root.TryGetProperty("isStalled", out var stalledEl) && stalledEl.GetBoolean();

            // 进度摘要（仅当 host 端写入了 isStarted 字段时拼装）
            if (root.TryGetProperty("isStarted", out _))
            {
                var parts = new List<string>();
                if (root.TryGetProperty("nextSpeaker", out var nextEl))
                {
                    var next = nextEl.GetString();
                    if (!string.IsNullOrWhiteSpace(next))
                        parts.Add($"下一位发言者：{next}");
                }
                if (root.TryGetProperty("instructionOrQuestion", out var instrEl))
                {
                    var instr = instrEl.GetString();
                    if (!string.IsNullOrWhiteSpace(instr))
                        parts.Add($"待执行指令：{instr}");
                }
                if (root.TryGetProperty("isRequestSatisfied", out var satEl) && satEl.GetBoolean())
                {
                    parts.Add("任务已被认定为满足需求");
                }
                if (root.TryGetProperty("isInLoop", out var loopEl) && loopEl.GetBoolean())
                {
                    parts.Add("⚠️ 检测到执行循环");
                }
                ProgressSummary = string.Join("\n", parts);
            }
            else
            {
                ProgressSummary = string.Empty;
            }
        }
        catch (Exception)
        {
            // 解析失败兜底：原文展示
            PlanText = payloadJson;
            ProgressSummary = "(解析 payload 失败，已显示原始 JSON)";
            IsStalled = false;
        }
    }

    // ──── INotifyPropertyChanged ────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
