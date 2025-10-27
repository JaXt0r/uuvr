using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
#if CPP
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
#endif

namespace Uuvr.Loader;

#if CPP
[BepInPlugin("raicuparta.uuvr.loader-il2cpp", "UUVR Loader (IL2CPP)", "0.4.0")]
public partial class LoaderPlugin : BasePlugin
{
    private ManualLogSource Logger => Log;
    
    public override void Load()
    {
        Bootstrap();
    }
}
#elif MONO
[BepInPlugin("raicuparta.uuvr.loader-mono", "UUVR Loader (Mono)", "0.4.0")]
public partial class LoaderPlugin : BaseUnityPlugin
{
    private void Awake()
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
#if MONO
            Logger.LogDebug("[UUVR.Loader] Loading mono implementation");
#else
            Logger.LogDebug("[UUVR.Loader] Loading il2cpp implementation");
#endif
            
            var pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            var implDir = Path.Combine(Path.Combine(pluginDir, "Uuvr"), "implementation");
            
            if (!Directory.Exists(implDir))
                implDir = Path.Combine(pluginDir, "implementation"); // fallback if user moved dlls

            var backend = IsIl2CppRuntime() ? "Il2cpp" : "Mono";
            var generation = IsModernUnity() ? "Modern" : "Legacy";
            var fileName = $"UUVR.{backend}.{generation}.dll";
            var implPath = Directory.GetFiles(implDir, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
            
            if (implPath == null)
                throw new FileNotFoundException($"UUVR implementation not found: {fileName} in {implDir}");

            var asm = Assembly.LoadFile(implPath);
            var bootstrapType = asm.GetType("Uuvr.UuvrBootstrap", throwOnError: true)!;
            var startMethod = bootstrapType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            
            if (startMethod == null)
                throw new MissingMethodException("Uuvr.UuvrBootstrap.Start(ConfigFile)");

            // Pass BepInEx ConfigFile from this plugin
            startMethod.Invoke(null, new object?[] { GetConfigFile() });
        }
        catch (Exception e)
        {
            Logger?.LogError($"Failed to bootstrap UUVR: {e}");
        }
    }

    private object? GetConfigFile()
    {
        // Both BaseUnityPlugin and BasePlugin expose Config property
        var prop = GetType().GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(this, null);
    }

    private static bool IsIl2CppRuntime()
    {
#if CPP
        return true;
#else
        return false;
#endif
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
