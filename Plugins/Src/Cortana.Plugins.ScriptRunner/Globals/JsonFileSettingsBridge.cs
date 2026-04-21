using System.Collections.Concurrent;
using System.Text.Json;

namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// 基于 JSON 文件的 KV 持久化（settings.json，文件级锁）。
/// </summary>
internal sealed class JsonFileSettingsBridge : ISettingsBridge
{
    private readonly string _filePath;
    private readonly Lock _fileLock = new();
    private readonly ConcurrentDictionary<string, string> _cache;

    public JsonFileSettingsBridge(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "settings.json");
        _cache = Load();
    }

    public string? Get(string key) => _cache.TryGetValue(key, out var v) ? v : null;

    public void Set(string key, string value)
    {
        _cache[key] = value;
        Persist();
    }

    public bool Remove(string key)
    {
        var removed = _cache.TryRemove(key, out _);
        if (removed) Persist();
        return removed;
    }

    public IReadOnlyDictionary<string, string> Snapshot() => new Dictionary<string, string>(_cache);

    private ConcurrentDictionary<string, string> Load()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_filePath)) return new ConcurrentDictionary<string, string>();
            try
            {
                var json = File.ReadAllText(_filePath);
                var dict = JsonSerializer.Deserialize(json, Protocol.ProtocolJsonContext.Default.DictionaryStringString);
                return dict is null
                    ? new ConcurrentDictionary<string, string>()
                    : new ConcurrentDictionary<string, string>(dict);
            }
            catch
            {
                return new ConcurrentDictionary<string, string>();
            }
        }
    }

    private void Persist()
    {
        lock (_fileLock)
        {
            var snapshot = new Dictionary<string, string>(_cache);
            var json = JsonSerializer.Serialize(snapshot, Protocol.ProtocolJsonContext.Default.DictionaryStringString);
            File.WriteAllText(_filePath, json);
        }
    }
}
