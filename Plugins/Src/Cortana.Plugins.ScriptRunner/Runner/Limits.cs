namespace Cortana.Plugins.ScriptRunner.Runner;

/// <summary>
/// 传输/执行尺寸上限。超过即走 Fail 分支，防止"蠢 AI"把巨型负载塞进 args 导致插件 OOM。
/// 大脚本请用 sys_csx_run_file；大参数请先写到文件再把路径传入。
/// </summary>
internal static class Limits
{
    /// <summary>单行协议帧（整个 HostRequest JSON）字节上限：4 MB。</summary>
    public const int MaxRequestLineBytes = 4 * 1024 * 1024;

    /// <summary>单段脚本源码字符数上限：256 K 字符（≈256 KB UTF-8 英文）。</summary>
    public const int MaxCodeChars = 256 * 1024;

    /// <summary>脚本 Console.Write* 捕获的 stdout 字符数上限：1 M 字符。</summary>
    public const int MaxStdoutChars = 1024 * 1024;

    /// <summary>脚本 Console.Error.Write* 捕获的 stderr 字符数上限：256 K 字符。</summary>
    public const int MaxStderrChars = 256 * 1024;

    /// <summary>ReturnValue.ToString() 长度上限：256 K 字符。</summary>
    public const int MaxReturnValueChars = 256 * 1024;

    /// <summary>超限截断提示后缀（中文避免歧义，中英都写）。</summary>
    public const string TruncatedMark = "\n...[truncated by ScriptRunner: output exceeded limit]";
}
