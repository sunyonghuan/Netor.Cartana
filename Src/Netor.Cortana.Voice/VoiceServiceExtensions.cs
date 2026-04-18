using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Netor.Cortana.Entitys;

namespace Netor.Cortana.Voice;

/// <summary>
/// Voice 模块 DI 注册扩展方法。
/// </summary>
public static class VoiceServiceExtensions
{
    /// <summary>
    /// 注册 Voice 模块所有服务到 DI 容器。
    /// </summary>
    public static IServiceCollection AddCortanaVoice(this IServiceCollection services)
    {
        services.AddSingleton<WakeWordService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WakeWordService>());

        services.AddSingleton<TextToSpeechService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TextToSpeechService>());

        services.AddSingleton<SpeechRecognitionService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<SpeechRecognitionService>());

        services.AddSingleton<VoiceChatOutputChannel>();
        services.AddSingleton<IAiOutputChannel>(sp => sp.GetRequiredService<VoiceChatOutputChannel>());

        services.AddSingleton<VoiceInputChannel>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<VoiceInputChannel>());
        services.AddSingleton<IAiInputChannel>(sp => sp.GetRequiredService<VoiceInputChannel>());

        services.AddSingleton<IVoiceCoordinator, VoiceCoordinator>();

        return services;
    }
}
