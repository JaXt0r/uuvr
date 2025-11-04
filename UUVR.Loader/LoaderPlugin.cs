using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using UUVR.Loader;
#if CPP
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
#endif

namespace Uuvr.Loader;

#if MONO
[BepInPlugin(GeneratedPluginInfo.PLUGIN_GUID, GeneratedPluginInfo.PLUGIN_NAME, GeneratedPluginInfo.PLUGIN_VERSION)]
public partial class LoaderPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        Bootstrap();
    }
}
#else
[BepInPlugin(
#if LEGACY
    "raicuparta.uuvr.loader-il2cpp-legacy", "UUVR.Loader.I.L",
#else
    "raicuparta.uuvr.loader-il2cpp-modern", "UUVR.Loader.I.M",
#endif
    "0.4.0")]
public partial class LoaderPlugin : BasePlugin
{
    private ManualLogSource Logger => Log;
    
    public override void Load()
    {
        Bootstrap();
    }
}
#endif

public partial class LoaderPlugin
{
    private const string _implDir = "implementations";
    private const string _toolsDir = "tools";
    private const string _gamePluginsDir = "game-plugins";
    
    private const string _steamVRUnity5FileName = "UUVR.SteamVR.2.6.1.dll";
    private const string _steamVRUnity2018FileName = "UUVR.SteamVR.2.8.0.dll";
    
    
    private class ImplementationInfo
    {
        public string Backend { get; }
        public string Net { get; }
        public UnityVersion Version { get; }
        public int UnityMajor { get; }
        public string RawUnityVersion { get; }

        public struct UnityVersion
        {
            public int Major { get; }
            public int Minor { get; }
            public string Patch { get; }

            public UnityVersion(string? versionString)
            {
                if (string.IsNullOrEmpty(versionString))
                {
                    Major = 0;
                    Minor = 0;
                    Patch = "";
                    return;
                }

                // Parse underscore-separated Unity versions.
                // Examples:
                //  - 5_4_3p3   => Major=5,    Minor=4,  Patch=3p3
                //  - 2018_4_9p1 => Major=2018, Minor=4,  Patch=9p1
                //  - 2021_3     => Major=2021, Minor=3,  Patch=""
                var m = Regex.Match(versionString, @"^(\d+)(?:_(\d+))?(?:_(\d+[a-zA-Z]?\d*))?$");
                if (m.Success)
                {
                    Major = int.TryParse(m.Groups[1].Value, out var mj) ? mj : 0;
                    Minor = int.TryParse(m.Groups[2].Success ? m.Groups[2].Value : "0", out var mn) ? mn : 0;
                    Patch = m.Groups[3].Success ? m.Groups[3].Value : "";
                    return;
                }

                Major = 0;
                Minor = 0;
                Patch = "";
            }

            public override string ToString()
            {
                return $"{Major}.{Minor}.{Patch}";
            }
        }

        public ImplementationInfo(string backend, string net, string unityVersionString)
        {
            Backend = backend;
            Net = net;
            RawUnityVersion = unityVersionString;
            Version = new UnityVersion(unityVersionString);
            // Compute UnityMajor according to filename rules
            // If unity version starts with "20" => use first 4 digits; else first digit only
            if (!string.IsNullOrEmpty(unityVersionString))
            {
                var digits = new string(unityVersionString.TakeWhile(char.IsDigit).ToArray());
                if (digits.StartsWith("20") && digits.Length >= 4)
                {
                    UnityMajor = int.TryParse(digits.Substring(0, 4), out var mj) ? mj : 0;
                }
                else if (digits.Length >= 1)
                {
                    UnityMajor = int.TryParse(digits.Substring(0, 1), out var mj) ? mj : 0;
                }
                else
                {
                    UnityMajor = 0;
                }
            }
            else
            {
                UnityMajor = 0;
            }
        }
    }
    
    private class ImplementationMatch
    {
        public string Path { get; }
        public ImplementationInfo Info { get; }

        public ImplementationMatch(string path, ImplementationInfo info)
        {
            Path = path;
            Info = info;
        }
    }

    private void Bootstrap()
    {
        try
        {
            Logger.LogInfo("==========");
            LogInitialInformation();

            Logger.LogInfo("===Load implementations.===");
            var allImplementations = GetAllImplementationVersions();
            Logger.LogInfo("===Calculate best fitting implementation.===");
            var implementationDLL = GetMatchingImplementation(allImplementations);
            Logger.LogInfo("===Loading implementation/UUVR dll now.===");
            LoadImplementation(implementationDLL.Path);
            
            Logger.LogInfo("===Load additional tool dependencies.===");
            LoadToolDependencies();
            
            Logger.LogInfo("===Load game specific plugins.===");
            LoadGamePlugins();
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to bootstrap UUVR: {e}");
        }
    }

    private void LogInitialInformation()
    {
        Logger.LogInfo("===Log initial environment information. Useful for troubleshooting.===");
        Logger.LogInfo($"UUVR Loader Plugin Information: {Info.Metadata.Name} {Info.Metadata.Version}");
        Logger.LogInfo($"UUVR Loader .dll path: {Assembly.GetExecutingAssembly().Location}");

        // CLR 3.x (Unity 5) and CLR 2.x (Unity 2017+; yes, it's a lower number as it seems older versions were more creative in what they report)
        Logger.LogInfo(".NET Environment Version:" + Environment.Version);
        Logger.LogInfo($"Unity Version: {GetUnityVersion()}");
        Logger.LogInfo($"Game is IL2CPP: {IsIl2CppRuntime()}");
        Logger.LogInfo($"Game is x64: {Is64Bit()}");
        Logger.LogInfo($"Game Exe directory: {Directory.GetCurrentDirectory()}");
    }

    private ImplementationInfo.UnityVersion GetUnityVersion()
    {
        var appType = Type.GetType("UnityEngine.Application, UnityEngine");
        var prop = appType?.GetProperty("unityVersion", BindingFlags.Public | BindingFlags.Static);
        var raw = prop?.GetValue(null, null) as string;
        // Convert Unity's dotted version (e.g., 2019.1.7f1) to underscore format expected by our parser (e.g., 2019_1_7f1)
        var underscored = string.IsNullOrEmpty(raw) ? raw : raw.Replace('.', '_');
        return new ImplementationInfo.UnityVersion(underscored);
    }

    private bool Is64Bit()
    {
        // IntPtr size is 4 on x86, 8 on x64.
        return IntPtr.Size == 8;
    }

    private bool IsIl2CppRuntime()
    {
        try
        {
            var gameFolder = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(gameFolder))
                return false;

            return File.Exists(Path.Combine(gameFolder, "GameAssembly.dll")) ||
                   File.Exists(Path.Combine(gameFolder, "GameAssembly.so"));
        }
        catch
        {
            return false;
        }
    }

    private Dictionary<string, ImplementationInfo> GetAllImplementationVersions()
    {
        var implementations = new Dictionary<string, ImplementationInfo>();
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var implDir = Path.Combine(pluginDir, _implDir);

        if (!Directory.Exists(implDir))
            throw new DirectoryNotFoundException($"Implementation directory not found: {implDir}");

        Logger.LogInfo($"Searching for implementations in: {implDir}");

        var files = Directory.GetFiles(implDir, "UUVR-*.dll");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            // Expected: UUVR-Backend-NETXX-UnityVersion.dll
            var match = Regex.Match(fileName, @"^UUVR-([^-]+)-(NET\d+)-([^.]+)\.dll$");
            if (!match.Success)
                continue;

            var backend = match.Groups[1].Value;
            var netRaw = match.Groups[2].Value; // e.g., NET35
            var net = Regex.Replace(netRaw, @"^NET", "", RegexOptions.IgnoreCase); // e.g., 35
            var unityRaw = match.Groups[3].Value; // e.g., 543p3 or 201849p1
            
            var newImplementationInfo = new ImplementationInfo(backend, net, unityRaw);
            if (newImplementationInfo.UnityMajor == 0)
                Logger.LogWarning($"DLL {fileName} couldn't be parsed for loading.");
            else
                implementations.Add(file, newImplementationInfo);
            
            Logger.LogInfo($"Found implementation: {fileName} (Backend: {newImplementationInfo.Backend}, NET: {newImplementationInfo.Net}, UnityMajor: {newImplementationInfo.UnityMajor}, RawUnity: {newImplementationInfo.RawUnityVersion})");
        }

        return implementations;
    }

    // FIXME - Compile current version of .NET into this Loader and only if it matches, then load implementation. Basically Unity.NETxy --> UUVR.Loader.NETxy --> UUVR.NETxy.UNITYxy only (keep .net version among dlls)
    private ImplementationMatch GetMatchingImplementation(Dictionary<string, ImplementationInfo> allImplementations, string currentNet = "35")
    {
        var gameUnityVersion = GetUnityVersion();
        var currentBackend = IsIl2CppRuntime() ? "IL2CPP" : "Mono";

        // Determine current Unity major according to the file naming rule
        var currentUnityMajor = gameUnityVersion.Major; // e.g., 5, 6, 2018, 2019, 2021

        Logger.LogInfo($"Looking for implementation with: Backend={currentBackend}, NET={currentNet}, UnityMajor={currentUnityMajor}");

        // 1) Backend must match, 2) .NET runtime version must match
        var filtered = allImplementations
            .Where(kvp => string.Equals(kvp.Value.Backend, currentBackend, StringComparison.OrdinalIgnoreCase))
            .Where(kvp => string.Equals(kvp.Value.Net, currentNet, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!filtered.Any())
        {
            var availableNets = allImplementations
                .Where(kvp => string.Equals(kvp.Value.Backend, currentBackend, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value.Net)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();
            throw new FileNotFoundException($"No implementation found for backend '{currentBackend}' with NET='{currentNet}'. Available NETs: [{string.Join(", ", availableNets)}]");
        }

        // 3) Prefer exact Unity major match
        var exactMatches = filtered.Where(kvp => kvp.Value.UnityMajor == currentUnityMajor).ToList();
        if (exactMatches.Any())
        {
            if (exactMatches.Count > 1)
            {
                Logger.LogWarning($"Multiple implementations found for Backend={currentBackend}, NET={currentNet}, UnityMajor={currentUnityMajor}. Choosing the first one.");
            }
            var chosen = exactMatches.First();
            Logger.LogInfo($"Selected implementation: {Path.GetFileName(chosen.Key)} (Backend={chosen.Value.Backend}, NET={chosen.Value.Net}, UnityMajor={chosen.Value.UnityMajor})");
            return new ImplementationMatch(chosen.Key, chosen.Value);
        }

        // 4) Fallback to one major version below
        var fallbackMajor = currentUnityMajor - 1;
        var fallbackMatches = filtered.Where(kvp => kvp.Value.UnityMajor == fallbackMajor).ToList();
        if (fallbackMatches.Any())
        {
            if (fallbackMatches.Count > 1)
            {
                Logger.LogWarning($"Multiple implementations found for Backend={currentBackend}, NET={currentNet}, UnityMajor={fallbackMajor}. Choosing the first one.");
            }
            var chosen = fallbackMatches.First();
            Logger.LogInfo($"Selected fallback implementation: {Path.GetFileName(chosen.Key)} (Backend={chosen.Value.Backend}, NET={chosen.Value.Net}, UnityMajor={chosen.Value.UnityMajor})");
            return new ImplementationMatch(chosen.Key, chosen.Value);
        }

        // Nothing suitable found
        var availableMajors = filtered.Select(kvp => kvp.Value.UnityMajor).Distinct().OrderBy(x => x).Select(x => x.ToString()).ToArray();
        throw new FileNotFoundException($"No implementation found for Backend={currentBackend}, NET={currentNet}. Available Unity majors: [{string.Join(", ", availableMajors)}]");
    }
    
    /// <summary>
    /// Calculates a compatibility score between the current Unity version and an implementation version.
    /// Higher scores indicate better compatibility. Scoring system:
    /// - Exact version match: +1,000,000
    /// - Different major version (current > impl): -100,000 - (difference * 10,000)
    /// - Different major version (impl > current): -500,000 - (difference * 10,000)
    /// - Same major version base score: +100,000
    /// - Same minor version bonus: +10,000
    /// - Different minor version (impl < current): -(difference * 100)
    /// - Different minor version (impl > current): -(difference * 500)
    /// - Different patch version (impl < current): -10
    /// - Different patch version (impl > current): -50
    /// </summary>
    private int CalculateVersionMatchScore(ImplementationInfo.UnityVersion current, ImplementationInfo.UnityVersion impl)
    {
        // Exact match gets highest score
        if (current.Major == impl.Major && current.Minor == impl.Minor && current.Patch == impl.Patch)
        {
            return int.MaxValue;
        }

        // Different major version - heavily penalize, but prefer lower
        if (current.Major != impl.Major)
        {
            if (impl.Major < current.Major)
            {
                // Lower major version - prefer closer ones
                var majorDiff = current.Major - impl.Major;
                return -100000 - (majorDiff * 10000);
            }
            else
            {
                // Higher major version - last resort, heavily penalized
                var majorDiff = impl.Major - current.Major;
                return -500000 - (majorDiff * 10000);
            }
        }

        // Same major version
        int score = 100000;

        // Different minor version
        if (current.Minor != impl.Minor)
        {
            var minorDiff = Math.Abs(current.Minor - impl.Minor);
            if (impl.Minor < current.Minor)
            {
                // Lower minor - preferred, small penalty
                score -= minorDiff * 100;
            }
            else
            {
                // Higher minor - less preferred, larger penalty
                score -= minorDiff * 500;
            }
        }
        else
        {
            // Same minor version - bonus
            score += 10000;
        }

        // Different patch version
        if (current.Patch != impl.Patch)
        {
            // When patch versions differ, prefer lower patch versions with small penalty
            if (string.Compare(impl.Patch, current.Patch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Lower patch - preferred, minimal penalty
                score -= 10;
            }
            else
            {
                // Higher patch - slightly less preferred
                score -= 50;
            }
        }

        return score;
    }
    
    private void LoadImplementation(string implementationName)
    {
        try
        {
            var asm = Assembly.LoadFrom(implementationName);
            var bootstrapType = asm.GetType("UUVR.UUVRPlugin", throwOnError: true)!;
            var startMethod = bootstrapType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            
            if (startMethod == null)
                throw new MissingMethodException("UUVR.UUVRPlugin.Start(ConfigFile)");

            // Pass BepInEx ConfigFile from this plugin
            startMethod.Invoke(null, new [] { GetConfigFile() });
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to load implementation {implementationName}: {e}");
            throw;
        }
    }
    
    private object GetConfigFile()
    {
        // Both BaseUnityPlugin and BasePlugin expose Config property
        var prop = GetType().GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(this, null);
    }
    
    private void LoadToolDependencies()
    {
        LoadSteamVR();
    }

    private void LoadSteamVR()
    {
        var implementations = new Dictionary<string, ImplementationInfo>();
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var toolDir = Path.Combine(pluginDir, _toolsDir);

        if (!Directory.Exists(toolDir))
        {
            Logger.LogError($"Tools directory not found: {toolDir}");
            return;
        }

        string steamVRDll;
        if (GetUnityVersion().Major == 5)
            steamVRDll = Path.Combine(toolDir, _steamVRUnity5FileName);
        else
            steamVRDll = Path.Combine(toolDir, _steamVRUnity2018FileName);

        if (!File.Exists(steamVRDll))
        {
            Logger.LogError($"SteamVR DLL not found: {steamVRDll}");
            return;
        }
        
        Logger.LogInfo($"Loading SteamVR: {steamVRDll}");
        Assembly.LoadFile(steamVRDll);
    }
    
    private void LoadGamePlugins()
    {
        LoadRewired();
    }

    private void LoadRewired()
    {
        var implementations = new Dictionary<string, ImplementationInfo>();
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var gamePluginsDir = Path.Combine(pluginDir, _gamePluginsDir);

        if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Rewired_Core"))
            return;
        
        Logger.LogInfo("Found Rewired as Game dependency. Loading appropriate UUVR plugin.");
        Logger.LogWarning("TBD: Splitting Rewired logic into separate dll not yet done.");
    }
}
