using System.Text.Json;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Plugin.Native;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// Memory 插件访问宿主大模型能力的轻量客户端。
/// </summary>
public sealed class HostModelCapabilityClient(
    PluginSettings settings,
    MemoryPluginBusConnection pluginBus,
    ILogger<HostModelCapabilityClient> logger)
{
    private const string FallbackPluginId = "memory_engine";

    /// <summary>
    /// 调用宿主授权的大模型能力并返回模型输出文本。
    /// </summary>
    /// <param name="purpose">调用目的。</param>
    /// <param name="instruction">任务指令。</param>
    /// <param name="input">业务输入。</param>
    /// <param name="outputFormat">期望输出格式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>宿主大模型返回的文本；不可用或失败时返回 <see langword="null"/>。</returns>
    public async Task<string?> InvokeAsync(
        string purpose,
        string instruction,
        string input,
        string outputFormat,
        CancellationToken cancellationToken = default)
    {
        if (!pluginBus.IsOpen) return null;

        try
        {
            var request = new HostModelCapabilityRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                PluginId = ResolvePluginId(),
                Purpose = purpose,
                Instruction = instruction,
                Input = input,
                OutputFormat = outputFormat,
                TimeoutMs = 0,
                MaxOutputTokens = 4096
            };

            var response = await pluginBus.InvokeModelAsync(request, cancellationToken).ConfigureAwait(false);
            if (response is null) return null;
            if (response.Success) return response.Content;

            logger.LogWarning("宿主模型能力调用失败：{Code} {Message}", response.ErrorCode, response.ErrorMessage);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "宿主模型能力不可用，使用降级记忆处理。 ");
        }

        return null;
    }

    /// <summary>
    /// 解析当前插件标识，优先使用宿主下发扩展配置，其次读取调试或发布目录中的 <c>plugin.json</c>。
    /// </summary>
    /// <returns>插件标识。</returns>
    private string ResolvePluginId()
    {
        if (settings.Extensions.TryGetValue("pluginId", out var extensionPluginId)
            && !string.IsNullOrWhiteSpace(extensionPluginId))
        {
            return extensionPluginId;
        }

        var manifestPath = Path.Combine(AppContext.BaseDirectory, "plugin.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                var metadata = JsonSerializer.Deserialize(json, MemoryHostModelCapabilityJsonContext.Default.HostPluginManifestMetadata);
                if (!string.IsNullOrWhiteSpace(metadata?.Id)) return metadata.Id;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "读取插件清单失败，使用默认插件 ID。Path={Path}", manifestPath);
            }
        }

        return FallbackPluginId;
    }

}
