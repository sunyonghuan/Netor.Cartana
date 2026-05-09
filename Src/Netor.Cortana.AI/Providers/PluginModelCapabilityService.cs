using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

using Netor.Cortana.AI.Drivers;
using Netor.Cortana.Entitys;
using Netor.Cortana.Entitys.ModelCapability;
using Netor.Cortana.Entitys.Services;

using System.Collections.Concurrent;
using System.Diagnostics;

namespace Netor.Cortana.AI.Providers;

public sealed class PluginModelCapabilityService(
    SystemSettingsService settingsService,
    AiProviderService providerService,
    AiModelService modelService,
    AiProviderDriverRegistry driverRegistry,
    ILogger<PluginModelCapabilityService> logger) : IPluginModelCapabilityService
{
    private const string LlmCapability = "llm";
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _concurrencyLocks = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ModelCapabilityResponse> InvokeAsync(ModelCapabilityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Type, "request", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不支持的 model-capability 请求。 ");
        }

        if (string.IsNullOrWhiteSpace(request.PluginId))
        {
            throw new InvalidOperationException("插件 ID 不能为空。 ");
        }

        var settings = ReadSettings(request.PluginId);
        if (!settings.Enabled)
        {
            throw new UnauthorizedAccessException("插件未授权使用大模型。 ");
        }

        var model = ResolveModel(settings)
            ?? throw new InvalidOperationException("插件授权未绑定有效模型。 ");
        var provider = providerService.GetById(model.ProviderId)
            ?? throw new InvalidOperationException("插件授权模型所属厂商不存在。 ");
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException("插件授权模型所属厂商未启用。 ");
        }

        var effectiveTimeout = GetEffectiveTimeout(request.TimeoutMs, settings.TimeoutMs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(effectiveTimeout));

        var throttle = _concurrencyLocks.GetOrAdd(request.PluginId, _ => new SemaphoreSlim(settings.MaxConcurrency, settings.MaxConcurrency));
        if (!await throttle.WaitAsync(TimeSpan.FromMilliseconds(effectiveTimeout), timeoutCts.Token).ConfigureAwait(false))
        {
            throw new TimeoutException("插件大模型授权并发已满，请稍后重试。 ");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var driver = driverRegistry.Resolve(provider);
            using var client = driver.CreateChatClient(provider, model);
            var messages = BuildMessages(request);
            var options = BuildOptions(driver, provider, request, settings);
            var response = await client.GetResponseAsync(messages, options, timeoutCts.Token).ConfigureAwait(false);

            stopwatch.Stop();
            return new ModelCapabilityResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Content = response.Text ?? string.Empty
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("插件大模型调用超时。 ");
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not InvalidOperationException and not TimeoutException)
        {
            logger.LogWarning(ex, "插件大模型调用失败：PluginId={PluginId}, Purpose={Purpose}", request.PluginId, request.Purpose);
            throw;
        }
        finally
        {
            throttle.Release();
        }
    }

    private PluginLlmSettings ReadSettings(string pluginId)
    {
        var prefix = $"Plugin:{pluginId}:Capability:{LlmCapability}";
        return new PluginLlmSettings(
            settingsService.GetValue($"{prefix}:Enabled", false),
            settingsService.GetValue($"{prefix}:ProviderId", string.Empty),
            settingsService.GetValue($"{prefix}:ModelId", string.Empty),
            settingsService.GetValue($"{prefix}:MaxInputTokens", 128000),
            settingsService.GetValue($"{prefix}:MaxOutputTokens", 128000),
            settingsService.GetValue($"{prefix}:TimeoutMs", 30000),
            Math.Max(1, settingsService.GetValue($"{prefix}:MaxConcurrency", 3)),
            settingsService.GetValue($"{prefix}:AllowBackground", true));
    }

    private AiModelEntity? ResolveModel(PluginLlmSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ModelId))
        {
            var configured = modelService.GetById(settings.ModelId);
            if (configured is not null && configured.IsEnabled) return configured;
        }

        if (!string.IsNullOrWhiteSpace(settings.ProviderId))
        {
            var models = modelService.GetByProviderId(settings.ProviderId);
            return models.FirstOrDefault(model => model.IsDefault) ?? models.FirstOrDefault();
        }

        var provider = providerService.GetAll().FirstOrDefault(item => item.IsDefault) ?? providerService.GetAll().FirstOrDefault();
        if (provider is null) return null;

        var providerModels = modelService.GetByProviderId(provider.Id);
        return providerModels.FirstOrDefault(model => model.IsDefault) ?? providerModels.FirstOrDefault();
    }

    private static List<ChatMessage> BuildMessages(ModelCapabilityRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(request.Instruction))
        {
            messages.Add(new ChatMessage(ChatRole.System, request.Instruction));
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Input ?? string.Empty));
        return messages;
    }

    private static ChatOptions BuildOptions(
        IAiProviderDriver driver,
        AiProviderEntity provider,
        ModelCapabilityRequest request,
        PluginLlmSettings settings)
    {
        var outputTokens = request.MaxOutputTokens > 0
            ? Math.Min(request.MaxOutputTokens, settings.MaxOutputTokens)
            : settings.MaxOutputTokens;

        var agent = new AgentEntity
        {
            Name = "Plugin Model Capability",
            Instructions = string.Empty,
            Temperature = 0.1,
            TopP = 1.0,
            MaxTokens = outputTokens,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            MaxHistoryMessages = 0,
            IsEnabled = true
        };

        return driver.BuildChatOptions(provider, agent);
    }

    private static int GetEffectiveTimeout(int requestTimeoutMs, int configuredTimeoutMs)
    {
        var configured = configuredTimeoutMs <= 0 ? 30000 : configuredTimeoutMs;
        return requestTimeoutMs <= 0 ? configured : Math.Min(requestTimeoutMs, configured);
    }

    private sealed record PluginLlmSettings(
        bool Enabled,
        string ProviderId,
        string ModelId,
        int MaxInputTokens,
        int MaxOutputTokens,
        int TimeoutMs,
        int MaxConcurrency,
        bool AllowBackground);
}
