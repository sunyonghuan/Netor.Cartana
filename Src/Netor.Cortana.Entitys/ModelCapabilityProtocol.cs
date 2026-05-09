namespace Netor.Cortana.Entitys;

/// <summary>
/// Cortana 宿主模型能力控制面常量。
/// </summary>
public static class ModelCapabilityProtocol
{
    public const string Path = "/internal/model-capability/";
    public const string Protocol = "model-capability";
    public const string Version = "1.0.0";
    public const string LlmInvokeOperation = "llm.invoke";

    public static string BuildEndpoint(int port) => $"ws://localhost:{port}{Path}";
}
