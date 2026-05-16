using Microsoft.Data.Sqlite;

namespace Netor.Cortana.Entitys.Services;

/// <summary>
/// 阶段 5B：Workflow Checkpoint BLOB I/O 存储库。
/// <c>WorkflowCheckpoints</c> 表的薄包装，承担 BLOB 写入 / 读取 / 列表 / 删除。
///
/// 上层 <c>SqliteCheckpointStore</c>（SDK <c>JsonCheckpointStore</c> 实现）通过此 Repo
/// 把 SDK 序列化的 <c>JsonElement</c> 字节数组持久化到 SQLite。
///
/// 决策 5B-C（Checkpoint 存储）：SQLite BLOB 同库，与 OrchestrationTask 同事务边界，
/// CASCADE 级联删除保证任务被删时 Checkpoint 不残留。
///
/// 详见 docs/未来版本策划/多智能体编排模式策划/04-实施阶段.md §5B.2。
/// </summary>
public sealed class WorkflowCheckpointRepository(CortanaDbContext db)
{
    private readonly CortanaDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// 插入 Checkpoint BLOB。<paramref name="checkpointId"/> 由 SDK 生成（GUID-N），全局唯一。
    /// 同 (taskId, checkpointId) 主键碰撞时抛出 SqliteException（理论上不应发生）。
    /// </summary>
    public void Insert(string taskId, string checkpointId, byte[] payload, long createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);
        ArgumentException.ThrowIfNullOrEmpty(checkpointId);
        ArgumentNullException.ThrowIfNull(payload);

        _db.Execute("""
            INSERT INTO WorkflowCheckpoints (TaskId, CheckpointId, Payload, CreatedAt)
            VALUES (@TaskId, @CheckpointId, @Payload, @CreatedAt)
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@TaskId", taskId);
                cmd.Parameters.AddWithValue("@CheckpointId", checkpointId);
                cmd.Parameters.Add("@Payload", SqliteType.Blob).Value = payload;
                cmd.Parameters.AddWithValue("@CreatedAt", createdAt);
            });
    }

    /// <summary>
    /// 按 (taskId, checkpointId) 主键查询 Payload；不存在返回 null。
    /// </summary>
    public byte[]? FindPayload(string taskId, string checkpointId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(checkpointId))
            return null;

        return _db.QueryFirstOrDefault(
            "SELECT Payload FROM WorkflowCheckpoints WHERE TaskId = @TaskId AND CheckpointId = @CheckpointId",
            r => (byte[])r["Payload"],
            cmd =>
            {
                cmd.Parameters.AddWithValue("@TaskId", taskId);
                cmd.Parameters.AddWithValue("@CheckpointId", checkpointId);
            });
    }

    /// <summary>
    /// 列出某 task 的所有 Checkpoint id（按 CreatedAt DESC，最新在前）。
    /// 用于 SDK <c>JsonCheckpointStore.RetrieveIndexAsync</c> 和 Phase 2.5 启动孤儿扫描。
    /// </summary>
    public List<(string CheckpointId, long CreatedAt)> ListByTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return [];

        return _db.Query(
            "SELECT CheckpointId, CreatedAt FROM WorkflowCheckpoints WHERE TaskId = @TaskId ORDER BY CreatedAt DESC",
            r => (r.GetString(r.GetOrdinal("CheckpointId")), r.GetInt64(r.GetOrdinal("CreatedAt"))),
            cmd => cmd.Parameters.AddWithValue("@TaskId", taskId));
    }

    /// <summary>
    /// 显式删除某 task 的全部 Checkpoint（任务正常完成时调用，避免 BLOB 长期堆积）。
    /// 注：OrchestrationTask 被删时 ON DELETE CASCADE 已会自动清理，本方法用于"任务完成但未删"场景。
    /// </summary>
    public int DeleteByTaskId(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return 0;

        return _db.Execute("DELETE FROM WorkflowCheckpoints WHERE TaskId = @TaskId",
            cmd => cmd.Parameters.AddWithValue("@TaskId", taskId));
    }
}
