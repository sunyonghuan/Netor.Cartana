using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cortana.Plugins.ScriptRunner.Protocol;

[JsonSerializable(typeof(HostRequest))]
[JsonSerializable(typeof(HostResponse))]
[JsonSerializable(typeof(PluginInfo))]
[JsonSerializable(typeof(InitConfig))]
[JsonSerializable(typeof(Tools.RunStrArgs))]
[JsonSerializable(typeof(Tools.RunFileArgs))]
[JsonSerializable(typeof(Tools.EvalArgs))]
[JsonSerializable(typeof(Tools.CodeArgs))]
[JsonSerializable(typeof(Tools.RunResult))]
[JsonSerializable(typeof(Tools.CheckResult))]
[JsonSerializable(typeof(Tools.FormatResult))]
[JsonSerializable(typeof(Tools.DiagnosticInfo))]
[JsonSerializable(typeof(Tools.SessionCreateResult))]
[JsonSerializable(typeof(Tools.SessionExecArgs))]
[JsonSerializable(typeof(Tools.SessionIdArgs))]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "DictionaryStringString")]
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class ProtocolJsonContext : JsonSerializerContext;
