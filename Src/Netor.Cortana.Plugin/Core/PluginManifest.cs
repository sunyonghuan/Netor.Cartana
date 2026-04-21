using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Netor.Cortana.Plugin;

/// <summary>
/// 插件运行时模式。
/// </summary>
public enum PluginRuntime
{
    /// <summary>
    /// .NET 托管插件（AssemblyLoadContext 加载）。
    /// </summary>
    [Display(Name = "dotnet")]
    Dotnet,

    /// <summary>
    /// 原生 DLL 插件（NativeLibrary 加载）。
    /// </summary>
    [Display(Name = "native")]
    Native,

    /// <summary>
    /// 子进程插件（stdin/stdout JSON-RPC）。
    /// </summary>
    [Display(Name = "process")]
    Process
}

/// <summary>
/// plugin.json 清单文件的反序列化模型。
/// 包含通用字段和三种运行时模式的扩展字段。
/// </summary>
public sealed record PluginManifest
{
    // ──────── 通用字段 ────────

    /// <summary>插件唯一标识（建议 reverse-domain 格式）。</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>插件显示名称。</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>语义版本号。</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>插件描述。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>运行时模式：dotnet / native / process。</summary>
    [JsonPropertyName("runtime")]
    public PluginRuntime Runtime { get; init; }

    /// <summary>要求的最低宿主版本。</summary>
    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; init; }

    /// <summary>插件引用的 Abstractions 版本。</summary>
    [JsonPropertyName("abstractionsVersion")]
    public string? AbstractionsVersion { get; init; }

    // ──────── dotnet 模式扩展字段 ────────

    /// <summary>入口程序集文件名（如 WeatherPlugin.dll）。</summary>
    [JsonPropertyName("assemblyName")]
    public string? AssemblyName { get; init; }

    /// <summary>插件目标框架（如 net10.0）。</summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; init; }

    // ──────── native 模式扩展字段 ────────

    /// <summary>原生 DLL 文件名。</summary>
    [JsonPropertyName("libraryName")]
    public string? LibraryName { get; init; }

    // ──────── process 模式扩展字段 ────────

    /// <summary>启动命令（相对路径基于插件目录）。</summary>
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    // ──────── 校验 ────────

    /// <summary>
    /// 校验清单文件的必填字段是否完整。
    /// </summary>
    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            error = "缺少必填字段 'id'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "缺少必填字段 'name'";
            return false;
        }

        switch (Runtime)
        {
            case PluginRuntime.Dotnet when string.IsNullOrWhiteSpace(AssemblyName):
                error = "dotnet 模式缺少 'assemblyName' 字段";
                return false;

            case PluginRuntime.Native when string.IsNullOrWhiteSpace(LibraryName):
                error = "native 模式缺少 'libraryName' 字段";
                return false;

            case PluginRuntime.Process when string.IsNullOrWhiteSpace(Command):
                error = "process 模式缺少 'command' 字段";
                return false;
        }

        error = string.Empty;
        return true;
    }
}
