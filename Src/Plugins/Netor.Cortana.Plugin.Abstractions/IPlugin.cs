using Microsoft.Extensions.AI;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netor.Cortana.Plugin.Abstractions;

/// <summary>
/// 插件接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 工具分类标签（可选）。
    /// </summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 插件唯一标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 插件名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 插件的工具
    /// </summary>
    IReadOnlyList<AITool> Tools { get; }

    /// <summary>
    /// 插件版本。
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 插件描述。
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 返回注入到 AI 的系统指令片段（可选）。
    /// 告诉 AI 什么时候使用这些工具、怎么用。
    /// </summary>
    string? Instructions { get; }

    /// <summary>
    /// 插件初始化。宿主在加载插件后调用，传入有限的宿主服务。
    /// </summary>
    Task InitializeAsync(IPluginContext context);
}