using System.Text;
using System.Text.Json;
using MyProcessPlugin;

// 强制 stdout/stdin UTF-8、无 BOM
Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

InitConfig? _config = null;

string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

    HostResponse response;
    try
    {
        var req = JsonSerializer.Deserialize(line, AppJsonContext.Default.HostRequest)
                  ?? throw new Exception("请求反序列化为空");
        response = Dispatch(req);
    }
    catch (Exception ex)
    {
        response = new HostResponse { Success = false, Error = ex.Message };
    }

    Console.Out.WriteLine(JsonSerializer.Serialize(response, AppJsonContext.Default.HostResponse));
    Console.Out.Flush();
}

HostResponse Dispatch(HostRequest req) => req.Method switch
{
    "get_info" => HandleGetInfo(),
    "init"     => HandleInit(req.Args),
    "invoke"   => HandleInvoke(req.ToolName, req.Args),
    "destroy"  => new HostResponse { Success = true },
    _          => new HostResponse { Success = false, Error = $"unknown method: {req.Method}" }
};

HostResponse HandleGetInfo()
{
    var info = new PluginInfo
    {
        Id = "my_process_plugin",
        Name = "我的子进程插件",
        Version = "1.0.0",
        Description = "C# AOT EXE 插件模板",
        Instructions = "当用户需要 echo 文本时调用 my_process_echo。",
        Tags = new[] { "template", "process" },
        Tools = new[]
        {
            new ToolSpec
            {
                Name = "my_process_echo",
                Description = "回显输入文本",
                Parameters = new[]
                {
                    new ParamSpec { Name = "text", Type = "string", Description = "要回显的文本", Required = true }
                }
            }
        }
    };
    return new HostResponse { Success = true, Data = JsonSerializer.Serialize(info, AppJsonContext.Default.PluginInfo) };
}

HostResponse HandleInit(string? argsJson)
{
    if (string.IsNullOrWhiteSpace(argsJson))
        return new HostResponse { Success = false, Error = "init args 为空" };
    _config = JsonSerializer.Deserialize(argsJson, AppJsonContext.Default.InitConfig);
    Directory.CreateDirectory(_config?.DataDirectory ?? ".");
    return new HostResponse { Success = true };
}

HostResponse HandleInvoke(string? tool, string? argsJson)
{
    switch (tool)
    {
        case "my_process_echo":
        {
            var args = JsonSerializer.Deserialize(argsJson ?? "{}", AppJsonContext.Default.EchoArgs);
            return new HostResponse { Success = true, Data = args?.Text ?? "" };
        }
        default:
            return new HostResponse { Success = false, Error = $"unknown tool: {tool}" };
    }
}
