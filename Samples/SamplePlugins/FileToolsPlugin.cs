using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Netor.Cortana.Plugin.Abstractions;

using System.Security.Cryptography;
using System.Text;

namespace SamplePlugins;

/// <summary>
/// 文件操作插件 — 提供文件读写、目录管理、哈希计算等工具。
/// </summary>
public sealed class FileToolsPlugin : IPlugin
{
    private readonly List<AITool> _tools = [];
    private ILogger? _logger;
    private string _workspaceDir = string.Empty;

    public string Id => "com.sample.filetools";
    public string Name => "文件工具";
    public Version Version => new(1, 0, 0);
    public string Description => "提供文件读写、目录浏览、哈希计算等文件操作能力";
    public IReadOnlyList<string> Tags => ["文件", "工具"];
    public IReadOnlyList<AITool> Tools => _tools;

    public string? Instructions => """
        当用户需要操作文件时，使用以下工具：
        - sys_file_read: 读取文件内容（文本文件）
        - sys_file_write: 写入内容到文件
        - sys_file_list_dir: 列出目录下的文件和子目录
        - sys_file_search: 在目录中搜索匹配的文件
        - sys_file_info: 获取文件详细信息（大小、修改时间等）
        - sys_file_hash: 计算文件的 MD5/SHA256 哈希值
        - sys_file_create_dir: 创建目录
        - sys_file_delete: 删除文件
        所有文件路径相对于当前工作区目录。
        """;

    public Task InitializeAsync(IPluginContext context)
    {
        _logger = context.LoggerFactory.CreateLogger<FileToolsPlugin>();
        _workspaceDir = context.WorkspaceDirectory;

        _tools.Add(AIFunctionFactory.Create(ReadFile, "sys_file_read", "读取指定文件的文本内容"));
        _tools.Add(AIFunctionFactory.Create(WriteFile, "sys_file_write", "将文本内容写入指定文件"));
        _tools.Add(AIFunctionFactory.Create(ListDirectory, "sys_file_list_dir", "列出指定目录下的文件和子目录"));
        _tools.Add(AIFunctionFactory.Create(SearchFiles, "sys_file_search", "在指定目录中按名称模式搜索文件"));
        _tools.Add(AIFunctionFactory.Create(GetFileInfo, "sys_file_info", "获取指定文件的详细信息"));
        _tools.Add(AIFunctionFactory.Create(ComputeHash, "sys_file_hash", "计算指定文件的哈希值"));
        _tools.Add(AIFunctionFactory.Create(CreateDirectory, "sys_file_create_dir", "创建目录（支持多级）"));
        _tools.Add(AIFunctionFactory.Create(DeleteFile, "sys_file_delete", "删除指定文件"));

        _logger.LogInformation("FileToolsPlugin 初始化完成，工作区：{Dir}", _workspaceDir);
        return Task.CompletedTask;
    }

    private string ResolvePath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_workspaceDir, relativePath));

        // 安全检查：防止路径逃逸
        if (!fullPath.StartsWith(_workspaceDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("路径不在工作区范围内");

        return fullPath;
    }

    private string ReadFile(string path, int maxLines = 200)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) return $"错误：文件不存在 - {path}";

        var lines = File.ReadLines(fullPath).Take(maxLines).ToList();
        var result = string.Join("\n", lines);

        if (lines.Count >= maxLines)
            result += $"\n... (已截断，仅显示前 {maxLines} 行)";

        _logger?.LogDebug("读取文件 {Path}，{Count} 行", path, lines.Count);
        return result;
    }

    private string WriteFile(string path, string content)
    {
        var fullPath = ResolvePath(path);

        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(fullPath, content, Encoding.UTF8);
        _logger?.LogInformation("写入文件 {Path}，{Length} 字符", path, content.Length);
        return $"已写入 {path}（{content.Length} 字符）";
    }

    private string ListDirectory(string path = ".")
    {
        var fullPath = ResolvePath(path);
        if (!Directory.Exists(fullPath)) return $"错误：目录不存在 - {path}";

        var sb = new StringBuilder();
        sb.AppendLine($"目录: {path}");

        foreach (var dir in Directory.GetDirectories(fullPath).OrderBy(d => d))
            sb.AppendLine($"  📁 {Path.GetFileName(dir)}/");

        foreach (var file in Directory.GetFiles(fullPath).OrderBy(f => f))
        {
            var info = new FileInfo(file);
            sb.AppendLine($"  📄 {info.Name} ({FormatSize(info.Length)})");
        }

        return sb.ToString().TrimEnd();
    }

    private string SearchFiles(string directory, string pattern, bool recursive = true)
    {
        var fullPath = ResolvePath(directory);
        if (!Directory.Exists(fullPath)) return $"错误：目录不存在 - {directory}";

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(fullPath, pattern, option)
            .Take(50)
            .Select(f => Path.GetRelativePath(_workspaceDir, f))
            .ToList();

        return files.Count > 0
            ? $"找到 {files.Count} 个文件：\n" + string.Join("\n", files.Select(f => $"  {f}"))
            : $"未找到匹配 \"{pattern}\" 的文件";
    }

    private string GetFileInfo(string path)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) return $"错误：文件不存在 - {path}";

        var info = new FileInfo(fullPath);
        return $"""
            文件: {info.Name}
            大小: {FormatSize(info.Length)}
            创建时间: {info.CreationTime:yyyy-MM-dd HH:mm:ss}
            修改时间: {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}
            只读: {info.IsReadOnly}
            扩展名: {info.Extension}
            """;
    }

    private string ComputeHash(string path, string algorithm = "SHA256")
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) return $"错误：文件不存在 - {path}";

        using var stream = File.OpenRead(fullPath);
        byte[] hash;
        string algoName;

        if (algorithm.Equals("MD5", StringComparison.OrdinalIgnoreCase))
        {
            hash = MD5.HashData(stream);
            algoName = "MD5";
        }
        else
        {
            hash = SHA256.HashData(stream);
            algoName = "SHA256";
        }

        return $"{algoName}({Path.GetFileName(fullPath)}) = {Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private string CreateDirectory(string path)
    {
        var fullPath = ResolvePath(path);

        if (Directory.Exists(fullPath))
            return $"目录已存在：{path}";

        Directory.CreateDirectory(fullPath);
        _logger?.LogInformation("创建目录 {Path}", path);
        return $"已创建目录：{path}";
    }

    private string DeleteFile(string path)
    {
        var fullPath = ResolvePath(path);
        if (!File.Exists(fullPath)) return $"错误：文件不存在 - {path}";

        File.Delete(fullPath);
        _logger?.LogInformation("删除文件 {Path}", path);
        return $"已删除：{path}";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
