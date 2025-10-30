using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Rewired;
using Rewired.Data;
using UnityEngine;
using Valve.VR;

namespace Uuvr.Debugging;

public class Debugger : UuvrBehaviour
{
    private void Start()
    {
        Debug.Log("===All Assemblies===");
        foreach (var assembly in AccessTools.AllAssemblies())
        {
            Debug.Log(">" + assembly.GetName().Name + "<");
        }
        Debug.Log("====================");

        StartCoroutine(LogRewiredActions());

        StartCoroutine(RegisterSteamVRController());
    }

    private IEnumerator RegisterSteamVRController()
    {
        yield return new WaitForSeconds(1f);

        Debug.Log("===SteamVR register controller===");

        SteamVR_Actions.PreInitialize();
        SteamVR.Initialize();
        // SteamVR_Settings.instance.pauseGameWhenDashboardVisible = true;

        // var vrActions = SteamVR_Actions.Xbox;

        // vrActions.A.AddOnChangeListener(FunctionToCall, SteamVR_Input_Sources.Any);
        SteamVR_Actions.xbox_A.AddOnChangeListener(FunctionToCall, SteamVR_Input_Sources.Any);

        Debug.Log("VR Listener registered");
        Debug.Log("====================");

        void FunctionToCall(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource, bool newState)
        {
            Debug.Log("A changed " + newState);
        }
    }

    private void Update()
    {

    }


    // For printing the flatscreen game binds
    private IEnumerator LogRewiredActions()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("===Rewired Actions===");
        Debug.Log("Rewired version: " + ReInput.programVersion);

        foreach (var player in ReInput.players.AllPlayers)
        {
            if (player == null)
                continue;

            Debug.Log(player.name);

            Debug.Log("LogAllGameActions started");
            // All elements mapped to all joysticks in the player
            foreach (var j in player.controllers.Joysticks)
            {
                // Loop over all Joystick Maps in the Player for this Joystick
                foreach (var map in player.controllers.maps.GetMaps<JoystickMap>(j.id))
                {

                    // Loop over all button maps
                    foreach (var aem in map.ButtonMaps)
                    {
                        Debug.Log(aem.elementIdentifierName + " is assigned to Button " + aem.elementIndex +
                                  " with the Action " + ReInput.mapping.GetAction(aem.actionId).name +
                                  " with actionId " + aem.actionId);
                    }

                    // Loop over all axis maps
                    foreach (var aem in map.AxisMaps)
                    {
                        Debug.Log(aem.elementIdentifierName + " is assigned to Axis " + aem.elementIndex +
                                  " with the Action " + ReInput.mapping.GetAction(aem.actionId).name +
                                  " with actionId " + aem.actionId);
                    }

                    // Loop over all element maps of any type
                    foreach (var aem in map.AllMaps)
                    {
                        if (aem.elementType == ControllerElementType.Axis)
                        {
                            Debug.Log(aem.elementIdentifierName + " is assigned to Axis " + aem.elementIndex +
                                      " with the Action " + ReInput.mapping.GetAction(aem.actionId).name +
                                      " with actionId " + aem.actionId);
                        }
                        else if (aem.elementType == ControllerElementType.Button)
                        {
                            Debug.Log(aem.elementIdentifierName + " is assigned to Button " + aem.elementIndex +
                                      " with the Action " + ReInput.mapping.GetAction(aem.actionId).name +
                                      " with actionId " + aem.actionId);
                        }
                    }
                }
            }

            // The actual "set actions" method is obfuscated. Let's not set it via cryptic name - which changes with each Rewired version - but via reflection to be more flexible.
            var method = typeof(UserData).GetMethods()
                .FirstOrDefault(m => m.ReturnType == typeof(Rewired.CustomControllerMap) 
                    && m.GetParameters().Length == 3 
                    && m.GetParameters().All(p => p.ParameterType == typeof(int)));
            method?.Invoke(ReInput.UserData, new object[] { 0, 0, 0 });

            Debug.Log("LogAllGameActions ended");
            Debug.Log("====================");
        }
    }
}
