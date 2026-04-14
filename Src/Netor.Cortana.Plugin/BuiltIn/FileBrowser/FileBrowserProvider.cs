using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin.BuiltIn.FileBrowser;

/// <summary>
/// 文件浏览 AI 工具提供者
/// </summary>
public sealed class FileBrowserProvider : AIContextProvider
{
    private readonly ILogger<FileBrowserProvider> _logger;
    private readonly FileBrowser _fileBrowser;
    private readonly List<AITool> _tools = [];

    public FileBrowserProvider(ILogger<FileBrowserProvider> logger, FileBrowser fileBrowser)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileBrowser);

        _logger = logger;
        _fileBrowser = fileBrowser;
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_tools.Count == 0)
        {
            RegisterTools();
        }

        return new ValueTask<AIContext>(new AIContext { Tools = _tools, Instructions = """
    ### 文件浏览工具使用规范

    你拥有在允许范围内浏览和查看系统文件的能力。所有操作受到安全约束限制。

    #### 可用工具
    - **sys_list_directory** - 列出指定目录中的所有文件和文件夹
    - **sys_get_file_info** - 获取单个文件或文件夹的详细信息
    - **sys_read_file** - 读取文本文件的内容（仅支持10MB以内的文件）
    - **sys_search_files** - 使用模式（如 *.txt）搜索文件（异步，最多返回50个结果）
    - **sys_get_drives** - 获取系统中所有可用的驱动器
    - **sys_open_in_explorer** - 在资源管理器中打开目录，或定位文件/目录

    #### 安全约束
    - **禁止访问的目录**：C:\Windows, C:\Program Files, C:\Program Files (x86), C:\ProgramData, C:\System Volume Information, C:\$Recycle.Bin
    - **允许的文件类型**：仅支持文本、代码、图片、视频、音频、压缩文件、文档等特定扩展名
    - **文件大小限制**：读取文件内容时最大 10MB
    - **返回数量限制**：目录列表最多 100 项，搜索结果最多 50 个

    #### 工具使用建议
    1. 首先使用 **sys_get_drives** 或 **sys_list_directory** 确认目录结构
    2. 使用 **sys_get_file_info** 检查文件详情（大小、权限等）后再读取
    3. 对于大文件，使用 **sys_search_files** 时指定递归选项和搜索模式
    4. 使用 **sys_open_in_explorer** 时，mode=open 表示打开目录，mode=select 表示在资源管理器中定位文件或目录
    5. 如果遇到权限错误，说明该目录不在允许范围内

    #### 参数说明
    - **path**：目录或文件路径，使用绝对路径或相对路径均可
    - **pattern**：搜索模式，支持通配符（如 *.txt, *.cs）
    - **recursive**：是否递归搜索，默认为 true
    - **mode**：资源管理器模式，支持 open 或 select

    #### 输出说明
    - 成功操作返回 "✓" 前缀的结果
    - 失败操作返回 "✗" 前缀的错误信息
    - 警告信息返回 "⚠️" 前缀
    """ });
    }

    private void RegisterTools()
    {
        // 工具1：列出目录
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_list_directory",
            description: "列出指定目录中的所有文件和文件夹。返回文件名、大小、修改时间等信息。最多返回100项。",
            method: ListDirectoryAsync));

        // 工具2：获取文件信息
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_file_info",
            description: "获取单个文件或文件夹的详细信息。",
            method: GetFileInfoAsync));

        // 工具3：读取文件内容
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_read_file",
            description: "读取文本文件的内容。仅支持小于10MB的文本/代码文件。",
            method: ReadFileAsync));

        // 工具4：搜索文件
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_search_files",
            description: "搜索符合模式的文件。支持通配符（如 *.txt）。异步搜索，最多返回50个结果。",
            method: SearchFilesAsync));

        // 工具5：获取驱动器列表
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_get_drives",
            description: "获取系统中所有可用的驱动器（如 C:\\, D:\\ 等）。",
            method: GetDrivesAsync));

        // 工具6：资源管理器操作
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_open_in_explorer",
            description: "在资源管理器中打开目录，或定位文件/目录。mode=open 时打开目录，mode=select 时定位目标。",
            method: OpenInExplorerAsync));
    }

    private async Task<string> ListDirectoryAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return "✗ 错误：路径不能为空";

            var result = _fileBrowser.ListDirectory(path);

            if (result.Items.Count == 0 || result.Items[0].FullPath.StartsWith("错误"))
                return $"✗ {result.Items.FirstOrDefault()?.FullPath ?? "无法访问目录"}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✓ 目录: {path}");
            sb.AppendLine($"文件: {result.TotalFiles} | 文件夹: {result.TotalFolders}");
            if (result.HasMore)
                sb.AppendLine($"(显示前{result.LimitCount}项，还有更多...)");
            sb.AppendLine();

            foreach (var item in result.Items)
            {
                if (item.Type == "folder")
                {
                    sb.AppendLine($"📁 [{item.ItemCount}] {item.Name}/");
                }
                else
                {
                    var icon = item.FileTypeCategory switch
                    {
                        AllowedFileType.Image => "🖼️",
                        AllowedFileType.Video => "🎬",
                        AllowedFileType.Code => "💻",
                        AllowedFileType.Text => "📄",
                        _ => "📋"
                    };

                    sb.AppendLine($"{icon} {item.Name} ({item.SizeFormatted})");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "列出目录失败: {Path}", path);
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> GetFileInfoAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return "✗ 错误：路径不能为空";

            var fileInfo = _fileBrowser.GetFileInfo(path);

            if (fileInfo == null)
                return "✗ 错误：找不到文件或文件夹";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✓ 文件信息: {fileInfo.Name}");
            sb.AppendLine($"路径: {fileInfo.FullPath}");
            sb.AppendLine($"类型: {fileInfo.Type}");

            if (fileInfo.Type == "file")
            {
                sb.AppendLine($"大小: {fileInfo.SizeFormatted}");
                sb.AppendLine($"扩展名: {fileInfo.Extension}");
                sb.AppendLine($"类别: {fileInfo.FileTypeCategory}");
                sb.AppendLine($"只读: {fileInfo.IsReadOnly}");
            }
            else
            {
                sb.AppendLine($"子项数: {fileInfo.ItemCount}");
            }

            sb.AppendLine($"隐藏: {fileInfo.IsHidden}");
            sb.AppendLine($"创建时间: {fileInfo.Created:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"修改时间: {fileInfo.Modified:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件信息失败: {Path}", path);
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> ReadFileAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return "✗ 错误：路径不能为空";

            var content = _fileBrowser.ReadFileContent(path);

            if (content == null || content.StartsWith("错误"))
                return content ?? "✗ 无法读取文件";

            // 限制输出大小（防止过长）
            var maxLength = 50000;
            if (content.Length > maxLength)
            {
                content = content.Substring(0, maxLength) + $"\n\n... (文件过长，只显示前 {maxLength} 字符)";
            }

            return $"✓ 文件内容:\n\n{content}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取文件失败: {Path}", path);
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> SearchFilesAsync(
        string rootPath,
        string pattern,
        bool recursive = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return "✗ 错误：路径不能为空";

        if (string.IsNullOrWhiteSpace(pattern))
            return "✗ 错误：搜索模式不能为空";

        try
        {
            var progress = new Progress<int>(count => { });
            var result = await _fileBrowser.SearchFilesAsync(rootPath, pattern, recursive, 50, progress, ct);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔍 搜索: {rootPath} (模式: {pattern})");

            if (!result.IsCompleted)
                sb.AppendLine("⚠️ 搜索被中断");

            if (result.Files.Count == 0)
            {
                sb.AppendLine("✓ 没有找到匹配的文件");
                return sb.ToString();
            }

            sb.AppendLine($"✓ 找到 {result.Files.Count} 个文件 ({result.ElapsedMs}ms):");
            sb.AppendLine();

            foreach (var file in result.Files)
            {
                sb.AppendLine($"  {file.Name} ({file.SizeFormatted}) - {file.Modified:yyyy-MM-dd HH:mm}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索文件失败: {RootPath} 模式: {Pattern}", rootPath, pattern);
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> GetDrivesAsync(CancellationToken ct = default)
    {
        try
        {
            var drives = _fileBrowser.GetAvailableDrives();

            if (drives.Count == 0)
                return "✗ 没有找到可用的驱动器";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("✓ 可用的驱动器:");

            foreach (var drive in drives)
            {
                sb.AppendLine($"  💾 {drive}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取驱动器列表失败");
            return $"✗ 错误：{ex.Message}";
        }
    }

    private async Task<string> OpenInExplorerAsync(
        string path,
        string mode = "open",
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return "✗ 错误：路径不能为空";

            var result = _fileBrowser.OpenInExplorer(path, mode);
            return result.StartsWith("错误", StringComparison.Ordinal)
                ? $"✗ {result}"
                : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "资源管理器操作失败: {Path} Mode: {Mode}", path, mode);
            return $"✗ 错误：{ex.Message}";
        }
    }
}