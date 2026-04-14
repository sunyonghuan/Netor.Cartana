using System.Text.Json;

namespace Cortana.Plugins.Reminder;

/// <summary>
/// 提醒数据持久化，基于 JSON 文件。
/// </summary>
public sealed class ReminderStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<ReminderItem> _items = [];

    public ReminderStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "reminders.json");
        Load();
    }

    public List<ReminderItem> GetAll()
    {
        _lock.Wait();
        try { return [.. _items]; }
        finally { _lock.Release(); }
    }

    public ReminderItem? GetById(string id)
    {
        _lock.Wait();
        try { return _items.FirstOrDefault(r => r.Id == id); }
        finally { _lock.Release(); }
    }

    public void Add(ReminderItem item)
    {
        _lock.Wait();
        try
        {
            _items.Add(item);
            Save();
        }
        finally { _lock.Release(); }
    }

    public bool Update(ReminderItem item)
    {
        _lock.Wait();
        try
        {
            var idx = _items.FindIndex(r => r.Id == item.Id);
            if (idx < 0) return false;
            _items[idx] = item;
            Save();
            return true;
        }
        finally { _lock.Release(); }
    }

    public bool Delete(string id)
    {
        _lock.Wait();
        try
        {
            var removed = _items.RemoveAll(r => r.Id == id);
            if (removed > 0) Save();
            return removed > 0;
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 获取已到期且启用的提醒。
    /// </summary>
    public List<ReminderItem> GetDueReminders(DateTimeOffset now)
    {
        _lock.Wait();
        try { return _items.Where(r => r.IsEnabled && r.TriggerTime <= now).ToList(); }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 按标签筛选提醒。
    /// </summary>
    public List<ReminderItem> GetByTag(string tag)
    {
        _lock.Wait();
        try { return _items.Where(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList(); }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 按关键词搜索标题和内容。
    /// </summary>
    public List<ReminderItem> Search(string keyword)
    {
        _lock.Wait();
        try
        {
            return _items.Where(r =>
                r.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                r.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                r.Tags.Any(t => t.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 批量删除指定标签的所有提醒。
    /// </summary>
    public int DeleteByTag(string tag)
    {
        _lock.Wait();
        try
        {
            var removed = _items.RemoveAll(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            if (removed > 0) Save();
            return removed;
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// 触发后推进：更新下次触发时间或标记到期删除。
    /// </summary>
    public void AdvanceOrRemove(ReminderItem item)
    {
        _lock.Wait();
        try
        {
            item.TriggeredCount++;

            // 达到最大次数 或 非重复类型 → 删除
            if (item.RepeatType is "once" or null ||
                (item.MaxTriggerCount > 0 && item.TriggeredCount >= item.MaxTriggerCount))
            {
                _items.RemoveAll(r => r.Id == item.Id);
                Save();
                return;
            }

            // 推进到下一个未来的触发时间（跳过所有已错过的周期）
            var now = DateTimeOffset.Now;
            const int maxIterations = 1000; // 防止死循环
            var iterations = 0;
            while (item.TriggerTime <= now && iterations++ < maxIterations)
            {
                item.TriggerTime = item.RepeatType switch
                {
                    "daily" => item.TriggerTime.AddDays(1),
                    "weekly" => item.TriggerTime.AddDays(7),
                    "monthly" => item.TriggerTime.AddMonths(1),
                    "custom" when item.RepeatIntervalMinutes > 0
                        => item.TriggerTime.AddMinutes(item.RepeatIntervalMinutes),
                    _ => DateTimeOffset.MaxValue // 未知类型：设为最大值，等同于禁用
                };
            }

            Save();
        }
        finally { _lock.Release(); }
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _items = JsonSerializer.Deserialize(json, PluginJsonContext.Default.ListReminderItem) ?? [];
        }
        catch
        {
            _items = [];
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_items, PluginJsonContext.Default.ListReminderItem);
        File.WriteAllText(_filePath, json);
    }
}
