using Microsoft.Data.Sqlite;

var paths = args.Length > 0 ? args : new[]
{
    @"E:\Netor.me\Cortana\Src\Netor.Cortana.UI\cortana.db",
    @"E:\Netor.me\Cortana\artifacts\DbInspect\cortana_copy.db",
    @"E:\Netor.me\Cortana\cortana.db"
};

foreach (var path in paths)
{
    Console.WriteLine($"DB: {path}");
    if (!File.Exists(path))
    {
        Console.WriteLine("  not found");
        continue;
    }

    try
    {
        using var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        conn.Open();
        if (!HasTable(conn, "Agents"))
        {
            Console.WriteLine("  no Agents table");
            continue;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, IsDefault, IsEnabled, SortOrder FROM Agents ORDER BY IsDefault DESC, SortOrder, CreatedTimestamp DESC LIMIT 20";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"  id={reader.GetString(0)} name={reader.GetString(1)} default={reader.GetInt32(2)} enabled={reader.GetInt32(3)} sort={reader.GetInt32(4)}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  error: {ex.Message}");
    }

    Console.WriteLine();
}

static bool HasTable(SqliteConnection conn, string name)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
    cmd.Parameters.AddWithValue("@name", name);
    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
}
