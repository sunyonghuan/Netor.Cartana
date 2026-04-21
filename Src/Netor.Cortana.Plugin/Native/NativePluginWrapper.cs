using System.Text;

using Microsoft.Extensions.AI;

using Netor.Cortana.Plugin;

namespace Netor.Cortana.Plugin.Native;

/// <summary>
/// 将原生插件适配为 <see cref="IPlugin"/> 接口。
/// 每个工具调用通过 <see cref="NativePluginHost"/> 转发到子进程执行。
/// </summary>
public sealed class NativePluginWrapper : IPlugin
{
    private readonly ExternalProcessPluginHostBase _host;

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Version Version { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string? Instructions { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Tags { get; }

    /// <inheritdoc />
    public IReadOnlyList<AITool> Tools { get; }

    public NativePluginWrapper(ExternalProcessPluginHostBase host, NativePluginInfo info)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(info);

        _host = host;

        Id = info.Id;
        Name = info.Name;
        Version = Version.TryParse(info.Version, out var v) ? v : new Version(1, 0, 0);
        Description = info.Description ?? string.Empty;
        Instructions = info.Instructions;
        Tags = info.Tags?.AsReadOnly() ?? (IReadOnlyList<string>)[];

        Tools = BuildTools(info.Tools);
    }

    /// <inheritdoc />
    public Task InitializeAsync(IPluginContext context) => Task.CompletedTask;

    // ──────── 私有方法 ────────

    /// <summary>
    /// 根据工具描述列表动态构建 <see cref="AITool"/> 集合。
    /// 每个工具的执行逻辑通过子进程的 invoke 方法完成。
    /// </summary>
    private IReadOnlyList<AITool> BuildTools(List<NativeToolInfo>? toolInfos)
    {
        if (toolInfos is null or { Count: 0 })
            return [];

        List<AITool> tools = new(toolInfos.Count);

        foreach (var toolInfo in toolInfos)
        {
            if (string.IsNullOrWhiteSpace(toolInfo.Name))
                continue;

            var capturedName = toolInfo.Name;
            var description = BuildToolDescription(toolInfo);

            var tool = AIFunctionFactory.Create(
                method: (string argsJson) => InvokeToolAsync(capturedName, argsJson),
                name: capturedName,
                description: description);

            tools.Add(tool);
        }

        return tools.AsReadOnly();
    }

    /// <summary>
    /// 构建工具描述，包含参数信息。
    /// </summary>
    private static string BuildToolDescription(NativeToolInfo toolInfo)
    {
        if (toolInfo.Parameters is not { Count: > 0 })
            return toolInfo.Description;

        var sb = new StringBuilder(toolInfo.Description);
        sb.AppendLine();
        sb.AppendLine("参数（通过 JSON 对象传入 argsJson）：");

        foreach (var param in toolInfo.Parameters)
        {
            sb.Append($"- {param.Name} ({param.Type})");

            if (!string.IsNullOrWhiteSpace(param.Description))
                sb.Append($": {param.Description}");

            if (param.Required)
                sb.Append(" [必填]");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// 通过子进程调用指定工具。
    /// </summary>
    private async Task<string> InvokeToolAsync(string toolName, string argsJson)
    {
        var response = await _host.SendRequestAsync(new NativeHostRequest
        {
            Method = NativeHostMethods.Invoke,
            ToolName = toolName,
            Args = argsJson
        });

        if (!response.Success)
        {
            return $"[错误] 工具 {toolName} 执行失败：{response.Error}";
        }

        return response.Data ?? string.Empty;
    }
}
