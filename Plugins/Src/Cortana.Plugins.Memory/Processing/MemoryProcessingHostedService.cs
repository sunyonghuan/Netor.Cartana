using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Services;
using Cortana.Plugins.Memory.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Processing;

/// <summary>
/// 长期运行的记忆整理后台服务，定期处理新观察记录并触发抽象整理。
/// </summary>
/// <remarks>
/// 该服务由 .NET Host 托管，在插件启动后持续运行。它不直接访问数据库，
/// 而是通过记忆处理服务和设置服务完成调度，从而保持后台任务与存储实现解耦。
/// </remarks>
public sealed class MemoryProcessingHostedService(
    IMemoryProcessingService processingService,
    IMemorySettingsService settingsService,
    IMemoryStore store,
    ILogger<MemoryProcessingHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 托管服务主循环，按照配置间隔周期性触发记忆整理。
    /// </summary>
    /// <param name="stoppingToken">Host 停止时触发的取消令牌。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MemoryProcessingHostedService 已启动，等待 30 秒后开始首轮处理。");

        // 启动延迟：等待 feed 连接建立和首批 observations 摄取完成
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = settingsService.GetOptions();

                // 执行记忆衰减
                if (options.Decay.Enabled)
                {
                    RunDecay(options);
                }

                // 自动确认高质量候选片段
                AutoConfirmCandidates(options);

                if (options.Abstraction.Enabled)
                {
                    var result = await processingService.ProcessAsync(new MemoryProcessingRequest
                    {
                        MaxObservationCount = 100,
                        TriggerSource = "hosted-service",
                        TraceId = Guid.NewGuid().ToString("N")
                    }, stoppingToken).ConfigureAwait(false);

                    if (result.ProcessedObservationCount > 0 || result.FailedObservationCount > 0)
                    {
                        logger.LogInformation(
                            "记忆整理完成：Processed={ProcessedCount}, Created={CreatedCount}, Merged={MergedCount}, Failed={FailedCount}",
                            result.ProcessedObservationCount,
                            result.CreatedFragmentCount,
                            result.MergedFragmentCount,
                            result.FailedObservationCount);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "记忆整理后台服务本轮处理失败。");
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "记忆整理后台服务参数异常。");
            }

            var delay = GetDelay();
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }

        logger.LogInformation("MemoryProcessingHostedService 已停止。");
    }

    /// <summary>
    /// 计算下一轮记忆整理前需要等待的时间。
    /// </summary>
    /// <returns>配置的扫描间隔；当配置无效或过小时返回最小等待时间。</returns>
    private TimeSpan GetDelay()
    {
        var options = settingsService.GetDecayOptions();
        if (options.ScanIntervalMinutes <= 0) return MinimumDelay;

        var configuredDelay = TimeSpan.FromMinutes(options.ScanIntervalMinutes);
        return configuredDelay < MinimumDelay ? MinimumDelay : configuredDelay;
    }

    /// <summary>
    /// 执行记忆衰减：降低 retentionScore，低于阈值的标记为 fading/forgotten。
    /// </summary>
    private void RunDecay(MemoryOptions options)
    {
        try
        {
            var affected = store.ApplyDecay(options.Retention.MinimumScore, options.Retention.ForgetThreshold);
            if (affected > 0)
            {
                logger.LogInformation("记忆衰减完成：{AffectedCount} 条记忆已更新。", affected);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "记忆衰减执行失败。");
        }
    }

    /// <summary>
    /// 自动确认被多次召回的候选片段，使其可参与抽象生成。
    /// </summary>
    private void AutoConfirmCandidates(MemoryOptions options)
    {
        try
        {
            // 被召回 3 次以上的候选片段自动确认
            const int requiredAccessCount = 3;
            var confirmed = store.AutoConfirmCandidates(requiredAccessCount);
            if (confirmed > 0)
            {
                logger.LogInformation("自动确认候选片段：{ConfirmedCount} 条已升级为 active/confirmed。", confirmed);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "自动确认候选片段失败。");
        }
    }
}
