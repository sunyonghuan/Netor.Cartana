using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin.BuiltIn.FileBrowser;

/// <summary>
/// 文件操作 AI 工具提供者，提供文件创建、写入、删除、移动等能力。
/// 所有操作严格限制在当前工作目录范围内。
/// </summary>
public sealed class FileOperationProvider : AIContextProvider
{
    private readonly ILogger<FileOperationProvider> _logger;
    private readonly FileOperator _fileOperator;
    private readonly List<AITool> _tools = [];

    /// <summary>
    /// 初始化文件操作工具提供者。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="fileOperator">文件操作服务。</param>
    public FileOperationProvider(ILogger<FileOperationProvider> logger, FileOperator fileOperator)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileOperator);

        _logger = logger;
        _fileOperator = fileOperator;
    }

    /// <summary>
    /// 提供当前会话可用的文件操作工具上下文。
    /// </summary>
    /// <param name="context">调用上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含工具集合与使用说明的 AI 上下文。</returns>
    protected override ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (_tools.Count == 0)
            RegisterTools();

        return new ValueTask<AIContext>(new AIContext
        {
            Instructions = BuildInstructions(),
            Tools = _tools
        });
    }

    // ──────── 工具注册 ────────

    /// <summary>
    /// 注册文件与目录操作工具。
    /// </summary>
    private void RegisterTools()
    {
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_create_file",
            description: "Create a new file in the current workspace directory. Fails if the file already exists.",
            method: (string path, string content) => _fileOperator.CreateFile(path, content)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_write_file",
            description: "Write a file in the current workspace directory. Creates the file if it does not exist, or replaces it if it already exists. backup defaults to true for existing files.",
            method: (string path, string content, bool backup) => _fileOperator.WriteFile(path, content, backup)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_edit_file",
            description: "Edit an existing text file by 1-based line numbers. operation supports replace, insert, and delete. Use sys_read_file first to get exact line numbers and hash. backup defaults to true.",
            method: EditFileAsync));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_delete_file",
            description: "Delete a file in the current workspace directory. backup defaults to true.",
            method: (string path, bool backup) => _fileOperator.DeleteFile(path, backup)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_move_file",
            description: "Move or rename a file within the current workspace directory.",
            method: (string sourcePath, string destPath) => _fileOperator.MoveFile(sourcePath, destPath)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_create_directory",
            description: "Create a directory in the current workspace directory, including parent directories when needed.",
            method: (string path) => _fileOperator.CreateDirectory(path)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_delete_directory",
            description: "Delete an empty directory in the current workspace directory. Non-empty directories are not allowed.",
            method: (string path) => _fileOperator.DeleteDirectory(path)));
    }

    // ──────── 使用说明 ────────

    /// <summary>
    /// 构建面向 AI 的文件操作规则说明。
    /// </summary>
    /// <returns>文件操作工具使用说明。</returns>
    private static string BuildInstructions() => """
        ### File Operation Rules

        - Scope: use these tools only inside the current workspace directory.
        - Never create, write, delete, move, or rename paths outside the workspace directory.
        - If the user wants to operate on a path outside the workspace, do not use these tools yet. First get explicit user consent to change the workspace directory, then change the workspace directory, then continue.
        - Prefer relative paths when working inside the workspace.
        - Do not use .. to escape the workspace boundary.
        - sys_write_file and sys_delete_file back up existing files by default.
        - sys_edit_file backs up the original file by default before applying line-based edits.
        - Backups are stored under .cortana/backups inside the workspace.
        - Keep backup enabled unless the user explicitly asks to disable it.
        - sys_create_file does not overwrite existing files. Use it when creation must fail if the file already exists.
        - sys_write_file creates a new file when missing, or replaces the existing file when present.
        - Before sys_edit_file, call sys_read_file to get exact 1-based line numbers and the latest hash.
        - sys_edit_file operations: replace and delete require startLine/endLine; insert inserts before startLine, and startLine can be totalLines + 1 to append.
        - Pass expectedHash from sys_read_file whenever possible to avoid editing stale content.
        - sys_delete_directory only works for empty directories.
        - For move operations, both source and destination must stay inside the workspace.
        """;

    private Task<string> EditFileAsync(
        string path,
        string operation,
        int startLine,
        int? endLine = null,
        string? content = null,
        bool backup = true,
        string? expectedHash = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Task.FromResult("✗ 错误：路径不能为空");

            if (string.IsNullOrWhiteSpace(operation))
                return Task.FromResult("✗ 错误：operation 不能为空");

            var result = _fileOperator.EditFile(path, operation, startLine, endLine, content, backup, expectedHash);
            if (!result.IsSuccess)
                return Task.FromResult($"✗ 错误：{result.ErrorMessage}");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✓ 文件已编辑: {result.Path}");
            sb.AppendLine($"操作: {result.Operation}");
            sb.AppendLine($"改动范围: {result.StartLine}-{result.EndLine}");
            sb.AppendLine($"改动行数: {result.ChangedLineCount}");

            if (!string.IsNullOrWhiteSpace(result.BackupPath))
                sb.AppendLine($"备份: {result.BackupPath}");

            return Task.FromResult(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按行编辑文件失败: {Path} Operation: {Operation}", path, operation);
            return Task.FromResult($"✗ 错误：{ex.Message}");
        }
    }
}
