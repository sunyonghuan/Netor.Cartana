using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;

using Netor.Cortana.AI.Workflow.DynamicAgents;
using Netor.Cortana.Entitys;

namespace Netor.Cortana.UI.ViewModels.Workspace;

/// <summary>
/// P2-4：Magentic Manager 创建动态子智能体的审批卡片 ViewModel。
/// </summary>
/// <remarks>
/// 详见 Docs/未来版本策划/聊天式任务发起与动态智能体/03-实施阶段.md §4 plan §B.1。
///
/// 与 <see cref="WorkflowTaskApprovalVm"/> 同模式（同一个 TaskDetailVm 同时持有两个，互不干扰）：
/// - 收到 <see cref="DynamicAgentCreationRequestedArgs"/> 后调用 <see cref="Load"/> 显示卡片
/// - 用户点 ✓ 批准 / ✓✓ 全部批准 / ✕ 拒绝 → <see cref="DynamicAgentCreationGate.ResolveDecision"/> 解锁工具内 await
/// - <see cref="Clear"/> 由 OnDynamicAgentCreationResolved 订阅触发，避免幽灵卡片
/// </remarks>
public sealed class DynamicAgentCreationApprovalVm : INotifyPropertyChanged
{
    private readonly DynamicAgentCreationGate _gate;

    private string _taskId = string.Empty;
    private string _requestId = string.Empty;
    private string _proposedName = string.Empty;
    private string _proposedResponsibility = string.Empty;
    private string _proposedInstructions = string.Empty;
    private string _proposedRequiredToolsText = string.Empty;
    private int _currentCount;
    private int _maxSubAgents;
    private bool _isSubmitting;
    private bool _isVisible;

    public DynamicAgentCreationApprovalVm()
    {
        _gate = App.Services.GetRequiredService<DynamicAgentCreationGate>();
    }

    public string TaskId
    {
        get => _taskId;
        private set => SetField(ref _taskId, value);
    }

    public string RequestId
    {
        get => _requestId;
        private set => SetField(ref _requestId, value);
    }

    public string ProposedName
    {
        get => _proposedName;
        private set => SetField(ref _proposedName, value);
    }

    public string ProposedResponsibility
    {
        get => _proposedResponsibility;
        private set => SetField(ref _proposedResponsibility, value);
    }

    public string ProposedInstructions
    {
        get => _proposedInstructions;
        private set => SetField(ref _proposedInstructions, value);
    }

    /// <summary>"工具1, 工具2" 拼接好的展示文本（空集合时为"无"）。</summary>
    public string ProposedRequiredToolsText
    {
        get => _proposedRequiredToolsText;
        private set => SetField(ref _proposedRequiredToolsText, value);
    }

    public int CurrentCount
    {
        get => _currentCount;
        private set
        {
            if (SetField(ref _currentCount, value))
                OnPropertyChanged(nameof(CountText));
        }
    }

    public int MaxSubAgents
    {
        get => _maxSubAgents;
        private set
        {
            if (SetField(ref _maxSubAgents, value))
                OnPropertyChanged(nameof(CountText));
        }
    }

    /// <summary>"已创建 N / 上限 M" 展示文本。</summary>
    public string CountText => $"已创建 {_currentCount} / 上限 {_maxSubAgents}";

    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            if (SetField(ref _isSubmitting, value))
                OnPropertyChanged(nameof(IsInteractive));
        }
    }

    public bool IsInteractive => !_isSubmitting;

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetField(ref _isVisible, value);
    }

    /// <summary>
    /// 收到 <see cref="DynamicAgentCreationRequestedArgs"/> 时由 <c>TaskDetailVm</c> 调用，把卡片填充上来。
    /// </summary>
    public void Load(DynamicAgentCreationRequestedArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        TaskId = args.TaskId;
        RequestId = args.RequestId;
        ProposedName = args.ProposedName;
        ProposedResponsibility = args.ProposedResponsibility;
        ProposedInstructions = args.ProposedInstructions;
        ProposedRequiredToolsText = args.ProposedRequiredTools is { Count: > 0 }
            ? string.Join(", ", args.ProposedRequiredTools)
            : "（无）";
        CurrentCount = args.CurrentCount;
        MaxSubAgents = args.MaxSubAgents;
        IsSubmitting = false;
        IsVisible = true;
    }

    /// <summary>清空卡片（任务结束 / 收到 Resolved 事件 / 切换任务时调用）。</summary>
    public void Clear()
    {
        TaskId = string.Empty;
        RequestId = string.Empty;
        ProposedName = string.Empty;
        ProposedResponsibility = string.Empty;
        ProposedInstructions = string.Empty;
        ProposedRequiredToolsText = string.Empty;
        CurrentCount = 0;
        MaxSubAgents = 0;
        IsSubmitting = false;
        IsVisible = false;
    }

    public Task<bool> ApproveAsync()
        => SubmitAsync(DynamicAgentCreationDecision.Approved);

    public Task<bool> ApproveAllAsync()
        => SubmitAsync(DynamicAgentCreationDecision.ApprovedAll);

    public Task<bool> RejectAsync()
        => SubmitAsync(DynamicAgentCreationDecision.Rejected);

    private Task<bool> SubmitAsync(DynamicAgentCreationDecision decision)
    {
        if (string.IsNullOrEmpty(_requestId))
        {
            return Task.FromResult(false);
        }

        IsSubmitting = true;
        try
        {
            // ApprovedAll 在 Gate 内由 CreateSubAgentTool 解锁后顺手触发 EnableAutoApproveForTask；
            // 这里只回写决策，不直接动 auto-approve 表，避免双重写入。
            var ok = _gate.ResolveDecision(_requestId, decision);
            if (ok)
            {
                Clear();
            }
            return Task.FromResult(ok);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

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
