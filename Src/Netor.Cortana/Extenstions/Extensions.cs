namespace Netor.Cortana.Extenstions;

/// <summary>
/// 通用扩展方法集合。
/// </summary>
internal static class Extensions
{
    /// <summary>
    /// 将字符串截断到指定的最大长度。
    /// </summary>
    internal static string Truncate(this string str, int maxLength)
    {
        return str.Length <= maxLength ? str : str[..maxLength];
    }

    /// <summary>
    /// 计算字符串的 MD5 哈希值并返回 32 位小写十六进制字符串。
    /// </summary>
    internal static string Md5Encrypt(this string str)
    {
        byte[] hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(str));
        return Convert.ToHexStringLower(hash);
    }
}

/// <summary>
/// ChatMessageEntity 的扩展方法。
/// </summary>
internal static class ChatMessageExtensions
{
    /// <summary>
    /// 将消息实体的 Role 字符串转换为 <see cref="ChatRole"/>。
    /// </summary>
    internal static Microsoft.Extensions.AI.ChatRole ToChatRole(this ChatMessageEntity message)
    {
        return new Microsoft.Extensions.AI.ChatRole(message.Role);
    }
}