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
        public UnityVersion Version { get; }

        public struct UnityVersion
        {
            public int Major { get; }
            public int Minor { get; }
            public string Patch { get; }

            public UnityVersion(string versionString)
            {
                // Unity version format: "2020.3.47f1", "2019.4.40", etc.
                var match = Regex.Match(versionString, @"^(\d+)\.(\d+)(?:\.(\d+[a-zA-Z]\d+))?");
                if (!match.Success)
                {
                    Major = 0;
                    Minor = 0;
                    Patch = "";
                    return;
                }

                Major = int.Parse(match.Groups[1].Value);
                Minor = int.Parse(match.Groups[2].Value);
                Patch = match.Groups[3].Success ? match.Groups[3].Value : "";
            }

            public override string ToString()
            {
                return $"{Major}.{Minor}.{Patch}";
            }
        }

        public ImplementationInfo(string backend, string versionString)
        {
            Backend = backend;
            Version = new UnityVersion(versionString);
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
        Logger.LogInfo(".NET Environment Version:" + Environment.Version);
        Logger.LogInfo($"Unity Version: {GetUnityVersion()}");
        Logger.LogInfo($"Game is IL2CPP: {IsIl2CppRuntime()}");
        Logger.LogInfo($"Game is x64: {Is64Bit()}");
        Logger.LogInfo($"Game Exe directory: {Directory.GetCurrentDirectory()}");
        Logger.LogInfo($"This Loader .dll path: {Assembly.GetExecutingAssembly().Location}");
        Logger.LogInfo($"This Loader Plugin Information: {Info.Metadata.Name} {Info.Metadata.Version}");
    }

    private ImplementationInfo.UnityVersion GetUnityVersion()
    {
        var appType = Type.GetType("UnityEngine.Application, UnityEngine");
        var prop = appType?.GetProperty("unityVersion", BindingFlags.Public | BindingFlags.Static);
        return new ImplementationInfo.UnityVersion(prop?.GetValue(null, null) as string);
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

        var files = Directory.GetFiles(implDir, "UUVR.*.dll");
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var match = Regex.Match(fileName, @"UUVR\.([^.]+)\.(.+?)\.dll$");
            if (!match.Success)
                continue;

            var backend = match.Groups[1].Value;
            var version = match.Groups[2].Value;
            
            var newImplementationInfo = new ImplementationInfo(backend, version);
            if (newImplementationInfo.Version.Major == 0)
                Logger.LogWarning($"DLL {fileName} couldn't be parsed for loading.");
            else
                implementations.Add(file, newImplementationInfo);
            
            Logger.LogInfo($"Found implementation: {fileName} (Backend: {newImplementationInfo.Backend}, Version: {newImplementationInfo.Version})");
        }

        return implementations;
    }

    private ImplementationMatch GetMatchingImplementation(Dictionary<string, ImplementationInfo> allImplementations)
    {
        var gameUnityVersion = GetUnityVersion();

        var currentBackend = IsIl2CppRuntime() ? "IL2CPP" : "Mono";
        Logger.LogInfo($"Looking for implementation matching: Backend={currentBackend}, UnityVersion={gameUnityVersion}");

        // Filter implementations by matching backend
        var matchingBackendDlls = allImplementations
            .Where(kvp => string.Equals(kvp.Value.Backend, currentBackend, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matchingBackendDlls.Any())
        {
            throw new FileNotFoundException($"No implementation found for backend: {currentBackend}");
        }

        // Find best matching version
        ImplementationMatch bestMatch = null;
        int bestScore = int.MinValue;

        foreach (var implDll in matchingBackendDlls)
        {
            var score = CalculateVersionMatchScore(gameUnityVersion, implDll.Value.Version);
            Logger.LogInfo($"Implementation {implDll.Value.Backend}.{implDll.Value.Version} score: {score}");

            if (score > bestScore)
            {
                bestScore = score;
                
                bestMatch = new ImplementationMatch(implDll.Key, implDll.Value);
            }
        }

        if (bestMatch == null)
        {
            throw new FileNotFoundException($"No valid implementation found for {currentBackend} backend");
        }

        Logger.LogInfo($"Selected implementation: {bestMatch.Info.Backend}.{bestMatch.Info.Version}");
        return bestMatch;
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
            throw new DirectoryNotFoundException($"Tools directory not found: {toolDir}");

        string steamVRDll;
        if (GetUnityVersion().Major == 5)
            steamVRDll = Path.Combine(toolDir, _steamVRUnity5FileName);
        else
            steamVRDll = Path.Combine(toolDir, _steamVRUnity2018FileName);

        if (!File.Exists(steamVRDll))
            throw new FileNotFoundException($"SteamVR DLL not found: {steamVRDll}");
        
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
