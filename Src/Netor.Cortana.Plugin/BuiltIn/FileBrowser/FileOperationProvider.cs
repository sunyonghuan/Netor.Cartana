using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Netor.Cortana.Plugin.BuiltIn.FileBrowser;

/// <summary>
/// 文件操作 AI 工具提供者 — 提供文件的创建、修改、删除、移动等能力。
/// 所有操作严格限制在工作目录范围内。
/// </summary>
public sealed class FileOperationProvider : AIContextProvider
{
    private readonly ILogger<FileOperationProvider> _logger;
    private readonly FileOperator _fileOperator;
    private readonly List<AITool> _tools = [];

    public FileOperationProvider(ILogger<FileOperationProvider> logger, FileOperator fileOperator)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(fileOperator);

        _logger = logger;
        _fileOperator = fileOperator;
    }

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

    private void RegisterTools()
    {
        _tools.Add(AIFunctionFactory.Create(
            name: "sys_create_file",
            description: "在工作目录内创建新文件。如果文件已存在则失败。参数：path（文件路径，相对或绝对）、content（文件内容）。",
            method: (string path, string content) => _fileOperator.CreateFile(path, content)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_write_file",
            description: "修改/覆盖工作目录内的文件内容。参数：path（文件路径）、content（新内容）、backup（是否备份原文件，默认 true）。",
            method: (string path, string content, bool backup) => _fileOperator.WriteFile(path, content, backup)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_delete_file",
            description: "删除工作目录内的文件。参数：path（文件路径）、backup（是否备份再删除，默认 true）。",
            method: (string path, bool backup) => _fileOperator.DeleteFile(path, backup)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_move_file",
            description: "移动或重命名工作目录内的文件。参数：sourcePath（源路径）、destPath（目标路径）。",
            method: (string sourcePath, string destPath) => _fileOperator.MoveFile(sourcePath, destPath)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_create_directory",
            description: "在工作目录内创建文件夹（含递归创建父目录）。参数：path（文件夹路径）。",
            method: (string path) => _fileOperator.CreateDirectory(path)));

        _tools.Add(AIFunctionFactory.Create(
            name: "sys_delete_directory",
            description: "删除工作目录内的空文件夹（非空文件夹不允许删除）。参数：path（文件夹路径）。",
            method: (string path) => _fileOperator.DeleteDirectory(path)));
    }

    // ──────── 指令 ────────

    private static string BuildInstructions() => """
        ### 文件操作工具使用规范

        你拥有在工作目录范围内操作文件的能力。所有文件操作仅限工作目录内，超出范围会被拒绝。

        #### 安全约束
        - 所有路径必须在工作目录内，传相对路径或工作目录下的绝对路径均可
        - 不允许使用 .. 越界到工作目录之外
        - 操作结果会返回成功或失败信息

        #### 备份机制
        - sys_write_file 和 sys_delete_file 默认会先备份原文件
        - 备份保存在工作目录下 .cortana/backups/ 中
        - 除非用户明确要求不备份，否则不要关闭 backup 参数

        #### 操作建议
        - 创建文件前，先用 list_directory 或 get_file_info 确认路径不冲突
        - sys_create_file 不会覆盖已有文件，需要覆盖请用 sys_write_file
        - sys_delete_directory 只能删除空文件夹
        - 移动文件时，源和目标都必须在工作目录内
        """;
}
