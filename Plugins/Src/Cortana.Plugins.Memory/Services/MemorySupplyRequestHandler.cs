using System.Text.Json;

using Cortana.Plugins.Memory.Models;
using Cortana.Plugins.Memory.Serialization;

using Microsoft.Extensions.Logging;

namespace Cortana.Plugins.Memory.Services;

/// <summary>
/// 处理宿主通过 PluginBus 发来的长期记忆供应请求。
/// </summary>
public sealed class MemorySupplyRequestHandler(
    MemoryPluginBusConnection pluginBus,
    MemorySupplyControlHandler supplyControlHandler,
    ILogger<MemorySupplyRequestHandler> logger)
{
    /// <summary>
    /// 处理长期记忆供应请求，并通过 PluginBus 返回响应或错误。
    /// </summary>
    /// <param name="text">供应请求 JSON 文本。</param>
    /// <param name="cancellationToken">取消处理的令牌。</param>
    public async Task HandleAsync(string text, CancellationToken cancellationToken)
    {
        MemoryContextSupplyRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize(text, MemoryInternalJsonContext.Chinese.MemoryContextSupplyRequest);
            if (request is null)
            {
                await SendErrorAsync(null, null, "INVALID_REQUEST", "请求内容为空。", false, cancellationToken).ConfigureAwait(false);
                return;
            }

            var response = supplyControlHandler.Handle(request);
            var json = JsonSerializer.Serialize(response, MemoryInternalJsonContext.Chinese.MemoryContextSupplyPackage);
            await pluginBus.SendTextAsync(json, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await SendErrorAsync(request?.RequestId, request?.TraceId, "INVALID_ARGUMENT", ex.Message, false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "处理长期记忆供应请求失败。RequestId={RequestId}", request?.RequestId);
            await SendErrorAsync(request?.RequestId, request?.TraceId, "INTERNAL_ERROR", ex.Message, true, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendErrorAsync(
        string? requestId,
        string? traceId,
        string code,
        string message,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var error = new MemoryContextSupplyError
        {
            RequestId = requestId,
            TraceId = traceId,
            Code = code,
            Message = message,
            Retryable = retryable
        };
        var json = JsonSerializer.Serialize(error, MemoryInternalJsonContext.Chinese.MemoryContextSupplyError);
        await pluginBus.SendTextAsync(json, cancellationToken).ConfigureAwait(false);
    }
}
