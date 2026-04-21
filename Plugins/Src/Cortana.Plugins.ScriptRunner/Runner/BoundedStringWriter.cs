using System.Text;

namespace Cortana.Plugins.ScriptRunner.Runner;

/// <summary>
/// 包装 StringBuilder 的 TextWriter，达到上限后静默丢弃（记录 truncated 状态）。
/// 防止脚本内无限 Console.WriteLine 把插件 OOM 掉。
/// </summary>
internal sealed class BoundedStringWriter : TextWriter
{
    private readonly StringBuilder _sb;
    private readonly int _maxChars;
    private bool _truncated;

    public BoundedStringWriter(StringBuilder sb, int maxChars)
    {
        _sb = sb;
        _maxChars = maxChars;
    }

    public bool Truncated => _truncated;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (_sb.Length >= _maxChars) { _truncated = true; return; }
        _sb.Append(value);
    }

    public override void Write(string? value)
    {
        if (value is null) return;
        var remain = _maxChars - _sb.Length;
        if (remain <= 0) { _truncated = true; return; }
        if (value.Length <= remain) { _sb.Append(value); return; }
        _sb.Append(value, 0, remain);
        _truncated = true;
    }

    public override void Write(char[] buffer, int index, int count)
    {
        var remain = _maxChars - _sb.Length;
        if (remain <= 0) { _truncated = true; return; }
        if (count <= remain) { _sb.Append(buffer, index, count); return; }
        _sb.Append(buffer, index, remain);
        _truncated = true;
    }
}
