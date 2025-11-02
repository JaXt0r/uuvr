using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
#if CPP
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
#endif

namespace Uuvr.Loader;

#if MONO
[BepInPlugin("raicuparta.uuvr.loader-mono", "UUVR.Loader.Mono", "0.4.0")]
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
                    Patch = "23";
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

    private void Bootstrap()
    {
        try
        {
            Logger.LogInfo("==========");
            LogInitialInformation();

            Logger.LogInfo("===Load implementations.===");
            var allImplementations = GetAllImplementationVersions();
            Logger.LogInfo("===Calculate best fitting implementation.===");
            var implementationName = GetMatchingImplementation(allImplementations);
            Logger.LogInfo("===Last log from Loader. Loading implementation/UUVR dll now. See you in Unity Logs. ^_^===");
            LoadImplementation(implementationName);
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
        var implDir = Path.Combine(pluginDir, "implementation");

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

    private string GetMatchingImplementation(Dictionary<string, ImplementationInfo> allImplementations)
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
        ImplementationInfo bestMatch = null;
        string bestMatchPath = null;
        int bestScore = int.MinValue;

        foreach (var implDll in matchingBackendDlls)
        {
            var score = CalculateVersionMatchScore(gameUnityVersion, implDll.Value.Version);
            Logger.LogInfo($"Implementation {implDll.Value.Backend}.{implDll.Value.Version} score: {score}");

            if (score > bestScore)
            {
                bestScore = score;
                bestMatchPath = implDll.Key;
                bestMatch = implDll.Value;
            }
        }

        if (bestMatch == null)
        {
            throw new FileNotFoundException($"No valid implementation found for {currentBackend} backend");
        }

        Logger.LogInfo($"Selected implementation: {bestMatch.Backend}.{bestMatch.Version}");
        return bestMatchPath;
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
            var bootstrapType = asm.GetType("Uuvr.UUVRPlugin", throwOnError: true)!;
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
}
