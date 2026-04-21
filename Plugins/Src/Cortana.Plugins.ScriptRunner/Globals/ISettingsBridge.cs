namespace Cortana.Plugins.ScriptRunner.Globals;

/// <summary>
/// 本插件私有数据域的 KV 持久化（仅 JSON 文件，落在 DataDirectory/settings.json）。
/// 脚本读写的数据仅对自己可见，不跨插件、不回传到宿主。
/// </summary>
public interface ISettingsBridge
{
    /// <summary>读取字符串 KV。</summary>
    string? Get(string key);

    /// <summary>写入字符串 KV，立即落盘。</summary>
    void Set(string key, string value);

    /// <summary>删除指定 key。</summary>
    bool Remove(string key);

    /// <summary>当前全部 KV 快照。</summary>
    IReadOnlyDictionary<string, string> Snapshot();
}
