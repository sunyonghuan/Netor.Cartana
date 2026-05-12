using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0 ? args[0] : @"e:\Netor.me\Cortana\Plugins\Src\Cortana.Plugins.Memory\bin\Release\net10.0\memory.db";
var workspacePath = args.Length > 1 ? args[1] : @"E:\Netor.me\Cortana\Plugins";
var query = args.Length > 2 ? args[2] : "偏好 工作流 分析 策略 提示词 输出 风格 交易";
var agentId = args.Length > 3 ? args[3] : "agent.default.xiaoyue";
var workspaceHash = IsHexHash(workspacePath) ? workspacePath : Md5Hash(workspacePath);

Console.WriteLine($"DB: {dbPath}");
Console.WriteLine($"WorkspacePath: {workspacePath}");
Console.WriteLine($"WorkspaceHash: {workspaceHash}");
Console.WriteLine($"AgentId: {agentId}");
Console.WriteLine();

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

PrintCounts(conn, "memory_fragments");
PrintCounts(conn, "memory_abstractions");
Console.WriteLine();

PrintDistinct(conn, "memory_fragments");
PrintDistinct(conn, "memory_abstractions");
Console.WriteLine();

RunRecallProbe(conn, "manual-cross-agent-hash-workspace", null, workspaceHash, query);
RunRecallProbe(conn, "auto-current-agent-path-workspace", agentId, workspacePath, query);
RunRecallProbe(conn, "auto-current-agent-hash-workspace", agentId, workspaceHash, query);
RunRecallProbe(conn, "fallback-cross-agent-path-workspace", null, workspacePath, query);
RunRecallProbe(conn, "fallback-cross-agent-hash-workspace", null, workspaceHash, query);

static void RunRecallProbe(SqliteConnection conn, string label, string? agentId, string? workspaceId, string query)
{
    var terms = query.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '，', '。', '；', '：', '！', '？', '!', '?', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(t => t.Length >= 2)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(8)
        .ToArray();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"SELECT * FROM (
  SELECT id, 'fragment' AS kind, agentId, workspaceId, topic, title, summary, detail, confidence, lifecycleState, confirmationState
  FROM memory_fragments
  WHERE (@agent IS NULL OR agentId = @agent)
    AND (@workspace IS NULL OR workspaceId IS NULL OR workspaceId = @workspace)
    AND confidence >= 0.35
    AND lifecycleState <> 'forgotten'
    AND confirmationState <> 'rejected'
    AND (" + BuildFilter(terms, "topic", "title", "summary", "detail") + @")
    AND ((lifecycleState = 'active' AND confirmationState = 'confirmed') OR (@hasQuery = 1 AND lifecycleState = 'candidate' AND confirmationState = 'pending'))
  UNION ALL
  SELECT id, 'abstraction' AS kind, agentId, workspaceId, abstractionType AS topic, title, summary, statement AS detail, confidence, lifecycleState, confirmationState
  FROM memory_abstractions
  WHERE (@agent IS NULL OR agentId = @agent)
    AND (@workspace IS NULL OR workspaceId IS NULL OR workspaceId = @workspace)
    AND confidence >= 0.35
    AND lifecycleState <> 'forgotten'
    AND confirmationState <> 'rejected'
    AND (" + BuildFilter(terms, "abstractionType", "title", "summary", "statement") + @")
    AND ((lifecycleState = 'active' AND confirmationState = 'confirmed') OR (@hasQuery = 1 AND lifecycleState = 'candidate' AND confirmationState = 'pending'))
)
LIMIT 20";
    cmd.Parameters.AddWithValue("@agent", (object?)agentId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@workspace", (object?)workspaceId ?? DBNull.Value);
    cmd.Parameters.AddWithValue("@hasQuery", terms.Length > 0 ? 1 : 0);
    for (var i = 0; i < terms.Length; i++) cmd.Parameters.AddWithValue($"@term{i}", $"%{EscapeLikeTerm(terms[i])}%");

    var rows = 0;
    Console.WriteLine($"[{label}] agent={(agentId ?? "<null>")} workspace={(workspaceId ?? "<null>")}");
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        rows++;
        Console.WriteLine($"  {rows}. {reader.GetString(1)} id={reader.GetString(0)} agent={reader.GetString(2)} ws={(reader.IsDBNull(3) ? "<null>" : reader.GetString(3))} topic={reader.GetString(4)} state={reader.GetString(9)}/{reader.GetString(10)} conf={reader.GetDouble(8):0.###}");
    }
    Console.WriteLine($"  => hits={rows}");
    Console.WriteLine();
}

static string BuildFilter(string[] terms, string topicColumn, string titleColumn, string summaryColumn, string detailColumn)
{
    if (terms.Length == 0) return "1 = 1";
    return string.Join(" OR ", Enumerable.Range(0, terms.Length).Select(i => $"{topicColumn} LIKE @term{i} ESCAPE '\\' OR ifnull({titleColumn}, '') LIKE @term{i} ESCAPE '\\' OR {summaryColumn} LIKE @term{i} ESCAPE '\\' OR ifnull({detailColumn}, '') LIKE @term{i} ESCAPE '\\'"));
}

static void PrintCounts(SqliteConnection conn, string table)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
    Console.WriteLine($"{table}: {cmd.ExecuteScalar()}");
}

static void PrintDistinct(SqliteConnection conn, string table)
{
    Console.WriteLine($"{table} distinct scopes:");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT ifnull(agentId, '<null>'), ifnull(workspaceId, '<null>'), COUNT(*) FROM {table} GROUP BY agentId, workspaceId ORDER BY COUNT(*) DESC";
    using var reader = cmd.ExecuteReader();
    while (reader.Read()) Console.WriteLine($"  agent={reader.GetString(0)} ws={reader.GetString(1)} count={reader.GetInt32(2)}");
}

static bool IsHexHash(string value) => value.Length == 32 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
static string Md5Hash(string input) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(input)));
static string EscapeLikeTerm(string term) => term.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
