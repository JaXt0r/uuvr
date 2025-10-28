using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace Uuvr;

[HarmonyPatch]
public static class InputPatches
{
    private static readonly HashSet<KeyCode> LoggedGetKeys = new();
    private static readonly HashSet<KeyCode> LoggedGetKeysDown = new();
    private static readonly HashSet<KeyCode> LoggedGetKeysUp = new();
    private static readonly HashSet<string> LoggedGetButtons = new();
    private static readonly HashSet<string> LoggedGetButtonsDown = new();
    private static readonly HashSet<string> LoggedGetButtonsUp = new();

    [HarmonyPatch(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })]
    private static class GetKey_Patch
    {
        static void Postfix(KeyCode key)
        {
            if (LoggedGetKeys.Add(key))
            {
                // Debug.Log($"First GetKey check for: {key}");
            }
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })]
    private static class GetKeyDown_Patch
    {
        static void Postfix(KeyCode key)
        {
            if (LoggedGetKeysDown.Add(key))
            {
                // Debug.Log($"First GetKeyDown check for: {key}");
            }
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), new[] { typeof(KeyCode) })]
    private static class GetKeyUp_Patch
    {
        static void Postfix(KeyCode key)
        {
            if (LoggedGetKeysUp.Add(key))
            {
                // Debug.Log($"First GetKeyUp check for: {key}");
            }
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButton))]
    private static class GetButton_Patch
    {
        static void Postfix(string buttonName)
        {
            if (LoggedGetButtons.Add(buttonName))
            {
                Debug.Log($"First GetButton check for: {buttonName}");
            }
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonDown))]
    private static class GetButtonDown_Patch
    {
        static void Postfix(string buttonName)
        {
            if (LoggedGetButtonsDown.Add(buttonName))
            {
                Debug.Log($"First GetButtonDown check for: {buttonName}");
            }
        }
    }

    [HarmonyPatch(typeof(Input), nameof(Input.GetButtonUp))]
    private static class GetButtonUp_Patch
    {
        static void Postfix(string buttonName)
        {
            if (LoggedGetButtonsUp.Add(buttonName))
            {
                Debug.Log($"First GetButtonUp check for: {buttonName}");
            }
        }
    }
}
