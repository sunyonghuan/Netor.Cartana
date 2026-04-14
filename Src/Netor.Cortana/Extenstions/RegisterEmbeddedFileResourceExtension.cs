// 这个文件是 WinFormedge 项目的一部分。
// 版权所有 (c) 2025 Xuanchen Lin 保留所有权利。
// 该项目基于 MIT 许可证授权。
// 有关更多信息，请参阅项目根目录中的 LICENSE 文件。

using System.Reflection;

namespace WinFormedge;

/// <summary>
/// 为 <see cref="Formedge"/> 实例提供注册嵌入式文件资源的扩展方法。
/// </summary>
public static class RegisterEmbeddedFileResourceExtension
{
    /// <summary>
    /// 使用指定的 <see cref="EmbeddedFileResourceOptions"/> 将虚拟主机名映射到嵌入式资源。
    /// 注册一个 Web 资源处理程序，用于在 WebView2 环境中提供嵌入式文件。
    /// </summary>
    /// <param name="formedge">要配置的 <see cref="Formedge"/> 实例。</param>
    /// <param name="options">指定嵌入式资源映射的选项。</param>
    public static void SetVirtualHostNameToEmbeddedResourcesMapping(this Formedge formedge, EmbeddedFileResourceOptions? options = null)
    {
        options ??= new EmbeddedFileResourceOptions()
        {
            Scheme = App.Scheme,
            HostName = App.Domain,
            ResourceAssembly = Assembly.GetExecutingAssembly(),
            DefaultFolderName = "wwwroot",
        };
        formedge.SetVirtualHostNameToEmbeddedResourcesMapping(options);
    }
}