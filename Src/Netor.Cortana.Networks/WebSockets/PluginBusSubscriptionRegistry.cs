using System.Collections.Concurrent;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.Networks;

/// <summary>
/// 管理 PluginBus 客户端订阅关系，并提供按 topic 查询目标连接的能力。
/// </summary>
internal sealed class PluginBusSubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _subscriptions = new(StringComparer.Ordinal);

    /// <summary>
    /// 获取当前至少订阅一个 topic 的客户端数量。
    /// </summary>
    public int Count => _subscriptions.Count;

    /// <summary>
    /// 注册或更新客户端订阅的 topic 集合。
    /// </summary>
    /// <param name="clientId">客户端连接标识。</param>
    /// <param name="topics">客户端请求订阅的 topic 集合。</param>
    /// <returns>规范化后的 topic 集合。</returns>
    public string[] Subscribe(string clientId, IEnumerable<string> topics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(topics);

        var normalized = topics
            .Select(static topic => topic.Trim())
            .Where(static topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            normalized = [CortanaWsEndpoints.ConversationTopic];
        }

        _subscriptions[clientId] = new HashSet<string>(normalized, StringComparer.Ordinal);
        return normalized;
    }

    /// <summary>
    /// 移除客户端全部订阅。
    /// </summary>
    /// <param name="clientId">客户端连接标识。</param>
    public void Remove(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return;
        _subscriptions.TryRemove(clientId, out _);
    }

    /// <summary>
    /// 清空所有订阅关系。
    /// </summary>
    public void Clear() => _subscriptions.Clear();

    /// <summary>
    /// 查询订阅了指定 topic 的客户端标识。
    /// </summary>
    /// <param name="topic">目标 topic。</param>
    /// <returns>已订阅该 topic 的客户端标识序列。</returns>
    public IEnumerable<string> GetSubscribers(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) yield break;

        foreach (var pair in _subscriptions)
        {
            if (pair.Value.Contains(topic))
            {
                yield return pair.Key;
            }
        }
    }

    /// <summary>
    /// 判断客户端是否订阅了指定 topic。
    /// </summary>
    /// <param name="clientId">客户端连接标识。</param>
    /// <param name="topic">目标 topic。</param>
    /// <returns>如果已订阅则为 <see langword="true"/>。</returns>
    public bool IsSubscribed(string clientId, string topic)
    {
        return _subscriptions.TryGetValue(clientId, out var topics) && topics.Contains(topic);
    }
}
