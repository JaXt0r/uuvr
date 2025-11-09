using System;
using System.Reflection;
using UnityEngine;

namespace Uuvr.Core
{
    /// <summary>
    /// Snapshot of the game/runtime environment (Unity, SRP, platform, GPU, etc.)
    /// captured at startup and available for reuse by other components.
    /// Complements ModConfiguration (settings) by providing environment facts.
    /// </summary>
    public static class GameEnvironment
    {
        // Classification (converted to enums)
        public enum BackendType
        {
            Mono, IL2CPP
        }

        public enum GameType
        {
            Legacy, Modern
        }

        public enum RenderPipelineType
        {
            BuildIn, URP, HDRP
        }

        public static BackendType Backend;
        public static GameType Type;
        public static RenderPipelineType RenderPipeline;

        // Unity / App
        public static string UnityVersion;
        public static string ProductName;
        public static string CompanyName;
        public static string AppVersion;
        public static string Platform;

        // System / GPU
        public static string GraphicsApi;
        public static string GraphicsName;
        public static string GraphicsVersion;
        public static string OperatingSystem;
        public static string Cpu;

        // Paths
        public static string ModFolderPath;

        
        /// <summary>
        /// Initialize environment from the currently running Unity app.
        /// This is safe against older Unity versions (uses reflection where needed).
        /// </summary>
        public static void Init(string modFolderPath)
        {
            // Backend
#if CPP
            Backend = BackendKind.IL2CPP;
#elif MONO
            Backend = BackendType.Mono;
#endif

            // Type
#if LEGACY
            Type = GameType.Legacy;
#elif MODERN
            Type = GameType.Modern;
#endif

            // Render Pipeline (no hard refs to URP/HDRP types)
            RenderPipeline = GetRenderPipeline();

            // Unity/Application info
            UnityVersion = SafeGet(() => Application.unityVersion, "Unknown");
            ProductName  = SafeGet(() => Application.productName, "Unknown");
            CompanyName  = SafeGet(() => Application.companyName, "Unknown");
            AppVersion   = SafeGet(() => Application.version, "Unknown");
            Platform     = SafeGet(() => Application.platform.ToString(), "Unknown");

            // System/GPU info
            GraphicsApi     = SafeGet(() => SystemInfo.graphicsDeviceType.ToString(), "Unknown");
            GraphicsName    = SafeGet(() => SystemInfo.graphicsDeviceName, "Unknown");
            GraphicsVersion = SafeGet(() => SystemInfo.graphicsDeviceVersion, "Unknown");
            OperatingSystem = SafeGet(() => SystemInfo.operatingSystem, "Unknown");
            Cpu             = SafeGet(() => SystemInfo.processorType, "Unknown");

            // Paths
            ModFolderPath = modFolderPath ?? "";
        }

        public static void LogEnvironmentInformation()
        {
            Log.BInfo("==== UUVR Environment ====");
            Log.BInfo($"Unity Game: {ProductName} {AppVersion} (Company: {CompanyName})");
            Log.BInfo($"Unity Version: {UnityVersion} | Platform: {Platform}");
            Log.BInfo($"Backend: {Backend} | Type: {Type} | RenderPipeline: {RenderPipeline}");
            Log.BInfo($"Graphics: {GraphicsName} | API: {GraphicsApi} | Driver: {GraphicsVersion}");
            Log.BInfo($"OS: {OperatingSystem} | CPU: {Cpu}");
            Log.BInfo($"Mod Folder: {ModFolderPath}");
            Log.BInfo("===========================");
        }

        /// <summary>
        /// Try to get GraphicsSettings via reflection to avoid compile-time dependencies
        /// </summary>
        private static RenderPipelineType GetRenderPipeline()
        {
            try
            {
                var unityAsm = typeof(Shader).Assembly; // UnityEngine

                var gsType = unityAsm.GetType("UnityEngine.Rendering.GraphicsSettings");
                if (gsType == null)
                    return RenderPipelineType.BuildIn;

                var prop = gsType.GetProperty("currentRenderPipeline", BindingFlags.Public | BindingFlags.Static);
                if (prop == null)
                    return RenderPipelineType.BuildIn;
                
                var asset = prop.GetValue(null, null);
                if (asset == null)
                    return RenderPipelineType.BuildIn;
                
                var typeName = asset.GetType().Name;
                if (typeName.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return RenderPipelineType.URP;
                if (typeName.IndexOf("HD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("HighDefinition", StringComparison.OrdinalIgnoreCase) >= 0)
                    return RenderPipelineType.HDRP;

                // For any other/custom SRP, default to Built-in per spec limitation
                return RenderPipelineType.BuildIn;
            }
            catch (Exception e)
            {
                Log.Exception("Fetching RenderPipeline caused and exception", e);
                return RenderPipelineType.BuildIn;
            }
        }

        private static string SafeGet(Func<string> getter, string fallback)
        {
            try
            {
                return getter();
            }
            catch
            {
                return fallback;
            }
        }
    }
}
