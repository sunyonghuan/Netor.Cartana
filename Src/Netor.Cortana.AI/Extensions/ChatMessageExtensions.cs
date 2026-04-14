using Microsoft.Extensions.AI;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.AI;

/// <summary>
/// ChatMessageEntity 的扩展方法。
/// </summary>
public static class ChatMessageExtensions
{
    /// <summary>
    /// 将消息实体的 Role 字符串转换为 <see cref="ChatRole"/>。
    /// </summary>
    public static ChatRole ToChatRole(this ChatMessageEntity message)
    {
        return new ChatRole(message.Role);
    }
}
