using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Dotnet.Script.DependencyModel.Context;
using Dotnet.Script.DependencyModel.Logging;
using Dotnet.Script.DependencyModel.Runtime;

using Microsoft.CodeAnalysis;

namespace Cortana.Plugins.ScriptRunner.Runner;

/// <summary>
/// 基于 dotnet-script 的 DependencyModel 对 <c>#r "nuget:PackageId, Version"</c> 做还原与引用注入。
/// 内部会调用 <c>dotnet restore</c>（子进程），因此宿主机必须安装 .NET SDK（不止 Runtime）。
/// </summary>
internal sealed class NuGetResolver
{
    // 匹配 #r "nuget:Xxx, 1.2.3" / #r "nuget:Xxx/1.2.3" / #r "nuget:Xxx" 等形式的行。
    // 不消费尾部多余内容，只判断是否命中，具体解析交给 dotnet-script 的 ScriptParser。
    private static readonly Regex NuGetDirectiveRegex =
        new(@"^\s*#r\s+""\s*nuget\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly string _cacheRoot;
    private readonly Lazy<RuntimeDependencyResolver> _resolver;
    private readonly ConcurrentDictionary<string, ImmutableArray<MetadataReference>> _resolveCache
        = new(StringComparer.Ordinal);
    // AssemblyName.Name -> dll 绝对路径。用于懒加载（Resolving 事件），避免 eager load 触发
    // Humanizer 的传递 AssemblyRef（如某个未安装版本的 System.Private.CoreLib.dll）被按绝对路径查找而失败。
    private readonly ConcurrentDictionary<string, string> _assemblyByName =
        new(StringComparer.OrdinalIgnoreCase);
    private int _resolvingHookInstalled;

    public NuGetResolver(string cacheRoot)
    {
        _cacheRoot = cacheRoot;
        Directory.CreateDirectory(_cacheRoot);
        _resolver = new Lazy<RuntimeDependencyResolver>(() =>
        {
            LogFactory silent = _ => (_, _, _) => { };
            // useRestoreCache=true：同一 (packageId,version) 的 restore 结果会落到磁盘缓存，下次直接读取。
            return new RuntimeDependencyResolver(silent, _cacheRoot, useRestoreCache: true);
        });
    }

    private void EnsureResolvingHook()
    {
        if (Interlocked.Exchange(ref _resolvingHookInstalled, 1) != 0) return;
        AssemblyLoadContext.Default.Resolving += (alc, name) =>
        {
            if (name.Name is null) return null;
            if (_assemblyByName.TryGetValue(name.Name, out var path) && File.Exists(path))
            {
                try { return alc.LoadFromAssemblyPath(path); }
                catch (FileLoadException) { return null; }
                catch (BadImageFormatException) { return null; }
            }
            return null;
        };
    }

    /// <summary>脚本文本里是否出现了 <c>#r "nuget:..."</c> 指令。</summary>
    public static bool ContainsNuGetDirective(string code) => NuGetDirectiveRegex.IsMatch(code);

    /// <summary>
    /// 对给定代码解析 NuGet 依赖，返回应附加到 <see cref="Microsoft.CodeAnalysis.Scripting.ScriptOptions"/> 的引用列表。
    /// 解析结果按脚本中 nuget 指令行的内容做缓存；相同指令集不会重复还原。
    /// 同时把运行时 dll load 到默认 ALC，保证 <c>using XXX;</c> 能在执行期拿到类型。
    /// </summary>
    public Task<ImmutableArray<MetadataReference>> ResolveAsync(string code, CancellationToken ct)
    {
        var key = ComputeNuGetFingerprint(code);
        if (_resolveCache.TryGetValue(key, out var cached))
            return Task.FromResult(cached);

        // dotnet-script 的 RestoreDependencies 是同步阻塞（子进程 dotnet restore），放到线程池跑。
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            // 每个指纹分配一个独立的工作目录。dotnet-script 会把 workDir 的绝对路径"展平"
            // 拼进它自己的 cache（例如 dotnet-script\E\Netor.me\...），因此 workDir 本身必须很短，
            // 否则最终 restore 出的 .csproj 路径会超过 Windows MAX_PATH。这里用系统 temp + 短 hash。
            var workDir = Path.Combine(Path.GetTempPath(), "csx", key.Substring(0, 12));
            Directory.CreateDirectory(workDir);

            var deps = _resolver.Value.GetDependenciesForCode(
                workDir,
                ScriptMode.REPL,
                packageSources: Array.Empty<string>(),
                code: code);

            var refs = ImmutableArray.CreateBuilder<MetadataReference>();
            EnsureResolvingHook();
            foreach (var dep in deps)
            {
                foreach (var asm in dep.Assemblies)
                {
                    // dotnet-script 会把 Microsoft.NETCore.App 的 shared framework dll 也列进来，
                    // 但路径里的 runtime 版本可能本机没装；这些运行时程序集当前进程早已加载，跳过。
                    if (!File.Exists(asm.Path)) continue;
                    refs.Add(MetadataReference.CreateFromFile(asm.Path));
                    // 登记 name->path，真正的加载在 AssemblyLoadContext.Resolving 事件里按需完成，
                    // 避免 eager load 把不兼容的传递依赖（如某 runtime 版本的 CoreLib）也一并解析。
                    _assemblyByName[asm.Name.Name ?? Path.GetFileNameWithoutExtension(asm.Path)] = asm.Path;
                }
            }

            var result = refs.ToImmutable();
            _resolveCache[key] = result;
            return result;
        }, ct);
    }

    private static string ComputeNuGetFingerprint(string code)
    {
        // 只把 nuget 相关指令行纳入指纹，普通代码差异不触发重新解析。
        var sb = new StringBuilder();
        foreach (Match m in NuGetDirectiveRegex.Matches(code))
        {
            // 取整行；Regex 上面只匹配到前缀，这里扩到行尾。
            int lineStart = code.LastIndexOf('\n', m.Index) + 1;
            int lineEnd = code.IndexOf('\n', m.Index);
            if (lineEnd < 0) lineEnd = code.Length;
            sb.Append(code, lineStart, lineEnd - lineStart).Append('\n');
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
