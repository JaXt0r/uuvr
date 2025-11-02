using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private void Bootstrap()
    {
        try
        {
            Logger.LogInfo("==========");
            LogInitialInformation();

            var allImplementations = GetAllImplementationVersions();
            var implementationName = GetMatchingImplementation(allImplementations);
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

    private string GetUnityVersion()
    {
        var appType = Type.GetType("UnityEngine.Application, UnityEngine");
        var prop = appType?.GetProperty("unityVersion", BindingFlags.Public | BindingFlags.Static);
        return prop?.GetValue(null, null) as string;
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
    
    private Dictionary<string, string> GetAllImplementationVersions()
    {
        var implementations = new Dictionary<string, string>();
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var implDir = Path.Combine(pluginDir, "implementation");
        
        if (!Directory.Exists(implDir))
        {
            throw new DirectoryNotFoundException($"Implementation directory not found: {implDir}");
        }

        Logger.LogInfo($"Searching for implementations in: {implDir}");
        var files = Directory.GetFiles(implDir, "UUVR.Implementation.*.dll");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var version = fileName.Replace("UUVR.Implementation.", "");
            implementations[version] = file;
            Logger.LogInfo($"Found implementation: {fileName} ({version})");
        }
        
        return implementations;
    }


    // FIXME - Test and improve
    private string GetMatchingImplementation(Dictionary<string, string> allImplementations)
    {
        var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var implDir = Path.Combine(Path.Combine(pluginDir, "UUVR"), "implementation");

        if (!Directory.Exists(implDir))
        {
            throw new DirectoryNotFoundException($"Implementation directory not found. Can't load UUVR: {implDir}");
        }

        var backend = IsIl2CppRuntime() ? "Il2cpp" : "Mono";
        var generation = IsModernUnity() ? "Modern" : "Legacy";
        var fileName = $"UUVR.{backend}.{generation}.dll";
        var implPath = Directory.GetFiles(implDir, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            
        if (implPath == null)
            throw new FileNotFoundException($"UUVR implementation not found: {fileName} in {implDir}");

        Logger.LogInfo($"Loading implementation: {implPath}");

        var asm = Assembly.LoadFile(implPath);
        var bootstrapType = asm.GetType("Uuvr.UuvrBootstrap", throwOnError: true)!;
        var startMethod = bootstrapType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);

        return "";
    }

    private static bool IsModernUnity()
    {
        // Read UnityEngine.Application.unityVersion via reflection to avoid compile-time UnityEngine ref
        try
        {
            var appType = Type.GetType("UnityEngine.Application, UnityEngine");
            if (appType == null) return true; // default modern if unknown
            var prop = appType.GetProperty("unityVersion", BindingFlags.Public | BindingFlags.Static);
            var versionStr = prop?.GetValue(null, null) as string;
            if (string.IsNullOrEmpty(versionStr)) return true;

            // Unity version format: "2020.3.47f1", "2019.4.40f1", etc.
            var major = 0;
            var firstDot = versionStr!.IndexOf('.');
            if (firstDot > 0 && int.TryParse(versionStr.Substring(0, firstDot), out major))
            {
                return major >= 2020;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }
}
