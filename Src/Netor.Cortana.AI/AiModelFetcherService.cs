using Netor.Cortana.Entitys;
using Netor.Cortana.Entitys.Services;

using System.Text.Json;

namespace Netor.Cortana.AI;

/// <summary>
/// 通过 OpenAI 兼容的 /v1/models 端点拉取远程可用模型列表，
/// 并转换为 <see cref="AiModelEntity"/> 持久化到本地数据库。
/// </summary>
/// <param name="httpClient">由 DI 容器注入的 HttpClient</param>
/// <param name="modelService">AI 模型数据服务</param>
public sealed class AiModelFetcherService(HttpClient httpClient, AiModelService modelService)
{
    private readonly HttpClient _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly AiModelService _modelService = modelService ?? throw new ArgumentNullException(nameof(modelService));

    /// <summary>
    /// 从远程 API 拉取模型列表并写入数据库。
    /// </summary>
    /// <param name="provider">AI 服务提供商实体（需包含 Url 和 Key）</param>
    /// <returns>写入的模型实体列表</returns>
    public async Task<List<AiModelEntity>> FetchAndSaveModelsAsync(AiProviderEntity provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var models = await FetchModelsFromApiAsync(provider.Url, provider.Key);

        var entities = new List<AiModelEntity>();
        foreach (var model in models)
        {
            entities.Add(new AiModelEntity
            {
                Name = model.Id,
                DisplayName = (string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName) ?? "没有名称",
                Description = model.OwnedBy ?? string.Empty,
                ModelType = "chat",
                IsEnabled = true,
                ProviderId = provider.Id,
            });
        }

        if (entities.Count > 0)
        {
            // 先清除旧数据再写入
            _modelService.DeleteByProviderId(provider.Id);
            _modelService.BatchInsert(entities);
        }

        return entities;
    }

    /// <summary>
    /// 调用 /v1/models 端点获取原始模型列表。
    /// </summary>
    private async Task<List<ModelInfo>> FetchModelsFromApiAsync(string baseUrl, string apiKey)
    {
        var url = baseUrl.TrimEnd('/') + "/models";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        var result = new List<ModelInfo>();

        using var doc = await JsonDocument.ParseAsync(stream);
        if (!doc.RootElement.TryGetProperty("data", out var dataArray))
            return result;

        foreach (var item in dataArray.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            result.Add(new ModelInfo
            {
                Id = id,
                DisplayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() : null,
                OwnedBy = item.TryGetProperty("owned_by", out var ob) ? ob.GetString() : null,
            });
        }

        return result;
    }

    /// <summary>
    /// 远程 API 返回的模型简要信息。
    /// </summary>
    private sealed class ModelInfo
    {
        public string Id { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? OwnedBy { get; set; }
    }
}
