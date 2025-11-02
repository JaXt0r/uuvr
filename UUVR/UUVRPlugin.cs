using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
#if CPP
using Il2CppInterop.Runtime.Injection;
#endif

namespace UUVR;

// Non-plugin bootstrap entry point invoked by Uuvr.Loader
public static class UUVRPlugin
{
    public static string ModFolderPath { get; private set; }

    public static void Start(ConfigFile config)
    {
        ModFolderPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(UUVRPlugin)).Location);
        
        new ModConfiguration(config);
        
        Debug.LogWarning("Hooking HarmonyX " + Assembly.GetExecutingAssembly().FullName);
        
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

#if CPP
        ClassInjector.RegisterTypeInIl2Cpp<VrCamera.VrCamera>();
        ClassInjector.RegisterTypeInIl2Cpp<VrCameraOffset>();
        ClassInjector.RegisterTypeInIl2Cpp<CanvasRedirect>();
        ClassInjector.RegisterTypeInIl2Cpp<UiOverlayRenderMode>();
        ClassInjector.RegisterTypeInIl2Cpp<VrUiCursor>();
        ClassInjector.RegisterTypeInIl2Cpp<VrUiManager>();
        ClassInjector.RegisterTypeInIl2Cpp<FollowTarget>();
        // ClassInjector.RegisterTypeInIl2Cpp<UuvrInput>();
        ClassInjector.RegisterTypeInIl2Cpp<UuvrPoseDriver>();
        ClassInjector.RegisterTypeInIl2Cpp<UuvrBehaviour>();
        ClassInjector.RegisterTypeInIl2Cpp<UuvrCore>();
        // ClassInjector.RegisterTypeInIl2Cpp<AdditionalCameraData>();
        ClassInjector.RegisterTypeInIl2Cpp<VrCameraManager>();
        ClassInjector.RegisterTypeInIl2Cpp<CanvasRedirectPatchMode>();
        ClassInjector.RegisterTypeInIl2Cpp<ScreenMirrorPatchMode>();
#endif

        UuvrCore.Create();
    }
}
