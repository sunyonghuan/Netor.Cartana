using System.Text;

using Microsoft.Extensions.Logging;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.Plugin.BuiltIn.FileBrowser;

/// <summary>
/// 文件操作器 — 在工作目录范围内执行文件的创建、修改、删除、移动等操作。
/// 所有路径操作均强制约束在 <see cref="IAppPaths.WorkspaceDirectory"/> 之内。
/// </summary>
public sealed class FileOperator
{
    private readonly IAppPaths _appPaths;
    private readonly ILogger<FileOperator> _logger;

    private const string BackupFolder = ".cortana/backups";

    public FileOperator(IAppPaths appPaths, ILogger<FileOperator> logger)
    {
        ArgumentNullException.ThrowIfNull(appPaths);
        ArgumentNullException.ThrowIfNull(logger);

        _appPaths = appPaths;
        _logger = logger;
    }

    // ──────── 文件操作 ────────

    /// <summary>
    /// 创建新文件（不覆盖已存在的文件）。
    /// </summary>
    public string CreateFile(string path, string content)
    {
        try
        {
            var fullPath = ResolveSafePath(path);

            if (File.Exists(fullPath))
                return $"错误：文件已存在 - {fullPath}";

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content ?? "", Encoding.UTF8);

            _logger.LogInformation("文件已创建：{Path}", fullPath);
            return $"文件已创建：{fullPath}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "创建文件失败：{Path}", path);
            return $"错误：创建文件失败 - {ex.Message}";
        }
    }

    /// <summary>
    /// 修改/覆盖文件内容。
    /// </summary>
    public string WriteFile(string path, string content, bool backup = true)
    {
        try
        {
            var fullPath = ResolveSafePath(path);

            if (!File.Exists(fullPath))
                return $"错误：文件不存在 - {fullPath}";

            string? backupPath = null;
            if (backup)
                backupPath = BackupFile(fullPath);

            File.WriteAllText(fullPath, content ?? "", Encoding.UTF8);

            _logger.LogInformation("文件已修改：{Path}", fullPath);
            return backupPath is not null
                ? $"文件已修改：{fullPath}，已备份到 {backupPath}"
                : $"文件已修改：{fullPath}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "修改文件失败：{Path}", path);
            return $"错误：修改文件失败 - {ex.Message}";
        }
    }

    /// <summary>
    /// 删除文件。
    /// </summary>
    public string DeleteFile(string path, bool backup = true)
    {
        try
        {
            var fullPath = ResolveSafePath(path);

            if (!File.Exists(fullPath))
                return $"错误：文件不存在 - {fullPath}";

            string? backupPath = null;
            if (backup)
                backupPath = BackupFile(fullPath);

            File.Delete(fullPath);

            _logger.LogInformation("文件已删除：{Path}", fullPath);
            return backupPath is not null
                ? $"文件已删除：{fullPath}，已备份到 {backupPath}"
                : $"文件已删除：{fullPath}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "删除文件失败：{Path}", path);
            return $"错误：删除文件失败 - {ex.Message}";
        }
    }

    /// <summary>
    /// 移动/重命名文件。
    /// </summary>
    public string MoveFile(string sourcePath, string destPath)
    {
        try
        {
            var fullSource = ResolveSafePath(sourcePath);
            var fullDest = ResolveSafePath(destPath);

            if (!File.Exists(fullSource))
                return $"错误：源文件不存在 - {fullSource}";

            if (File.Exists(fullDest))
                return $"错误：目标文件已存在 - {fullDest}";

            var destDir = Path.GetDirectoryName(fullDest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            File.Move(fullSource, fullDest);

            _logger.LogInformation("文件已移动：{Source} -> {Dest}", fullSource, fullDest);
            return $"文件已移动：{fullSource} -> {fullDest}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "移动文件失败：{Source} -> {Dest}", sourcePath, destPath);
            return $"错误：移动文件失败 - {ex.Message}";
        }
    }

    // ──────── 文件夹操作 ────────

    /// <summary>
    /// 创建文件夹（含递归创建父目录）。
    /// </summary>
    public string CreateDirectory(string path)
    {
        try
        {
            var fullPath = ResolveSafePath(path);

            if (Directory.Exists(fullPath))
                return $"文件夹已存在：{fullPath}";

            Directory.CreateDirectory(fullPath);

            _logger.LogInformation("文件夹已创建：{Path}", fullPath);
            return $"文件夹已创建：{fullPath}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "创建文件夹失败：{Path}", path);
            return $"错误：创建文件夹失败 - {ex.Message}";
        }
    }

    /// <summary>
    /// 删除空文件夹。
    /// </summary>
    public string DeleteDirectory(string path)
    {
        try
        {
            var fullPath = ResolveSafePath(path);

            if (!Directory.Exists(fullPath))
                return $"错误：文件夹不存在 - {fullPath}";

            if (Directory.EnumerateFileSystemEntries(fullPath).Any())
                return $"错误：文件夹非空，不允许删除 - {fullPath}";

            Directory.Delete(fullPath);

            _logger.LogInformation("文件夹已删除：{Path}", fullPath);
            return $"文件夹已删除：{fullPath}";
        }
        catch (Exception ex) when (ex is not SecurityException)
        {
            _logger.LogError(ex, "删除文件夹失败：{Path}", path);
            return $"错误：删除文件夹失败 - {ex.Message}";
        }
    }

    // ──────── 安全校验 ────────

    /// <summary>
    /// 解析路径并校验其在工作目录范围内。
    /// </summary>
    private string ResolveSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new SecurityException("路径不能为空");

        var workspace = Path.GetFullPath(_appPaths.WorkspaceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspace, path));

        if (!fullPath.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !fullPath.Equals(workspace, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"禁止操作工作目录以外的路径：{fullPath}");
        }

        return fullPath;
    }

    // ──────── 备份 ────────

    /// <summary>
    /// 备份文件到 .cortana/backups/ 目录下，返回备份路径。
    /// </summary>
    private string BackupFile(string fullPath)
    {
        var workspace = Path.GetFullPath(_appPaths.WorkspaceDirectory);
        var relativePath = Path.GetRelativePath(workspace, fullPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(workspace, BackupFolder, $"{relativePath}.{timestamp}.bak");

        var backupDir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
            Directory.CreateDirectory(backupDir);

        File.Copy(fullPath, backupPath, overwrite: true);

        _logger.LogInformation("文件已备份：{Source} -> {Backup}", fullPath, backupPath);
        return backupPath;
    }

    /// <summary>
    /// 安全异常，用于路径越界。
    /// </summary>
    private sealed class SecurityException(string message) : Exception(message);
}
