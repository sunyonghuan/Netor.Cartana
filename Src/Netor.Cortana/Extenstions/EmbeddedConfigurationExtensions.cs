using System.Reflection;

namespace Netor.Cortana.Extenstions;

/// <summary>
/// 从程序集嵌入资源读取 JSON 配置的扩展方法。
/// </summary>
internal static class EmbeddedConfigurationExtensions
{
    /// <summary>
    /// 从当前程序集的嵌入资源加载指定的 JSON 配置文件并反序列化。
    /// </summary>
    /// <typeparam name="T">目标配置类型。</typeparam>
    /// <param name="resourceName">嵌入资源的完整名称（如 "Netor.Cortana.appsettings.json"）。</param>
    /// <returns>反序列化后的配置实例。</returns>
    /// <exception cref="InvalidOperationException">嵌入资源不存在或反序列化失败时抛出。</exception>
    internal static T LoadEmbeddedJson<T>(string resourceName) where T : class, new()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入资源 '{resourceName}' 未找到。可用资源: {string.Join(", ", assembly.GetManifestResourceNames())}");

        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"反序列化嵌入资源 '{resourceName}' 失败。");
    }

    /// <summary>
    /// 从程序集嵌入资源提取二进制内容并写入磁盘文件，返回文件路径。
    /// Sherpa-ONNX 引擎需要磁盘文件路径，无法直接读取嵌入资源流。
    /// </summary>
    /// <param name="resourceName">嵌入资源的完整名称。</param>
    /// <param name="fileName">目标文件名。</param>
    /// <param name="subDirectory">子目录名称（如 "KWS" 或 "STT"）。</param>
    /// <returns>提取后的文件绝对路径。</returns>
    internal static string ExtractEmbeddedResourceToFile(string resourceName, string fileName, string subDirectory = "")
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入资源 '{resourceName}' 未找到。可用资源: {string.Join(", ", assembly.GetManifestResourceNames())}");

        var directory = Path.Combine(App.UserDataDirectory, "sherpa_models", subDirectory);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, fileName);

        // 仅在文件不存在时写入，避免重复 I/O
        if (!File.Exists(filePath))
        {
            using var fileStream = File.Create(filePath);
            stream.CopyTo(fileStream);
        }

        return filePath;
    }
}
