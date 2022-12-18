using BepInEx;
using HarmonyLib;
using K_PlayerControl;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using UnityEngine;
using K_PlayerControl.UI;
using RootMotion.FinalIK;

namespace EliminatorKaedeMP
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
		public delegate void MainThreadCallCallback();

		private static Plugin m_Instance;
		private static int mainThreadId;
		private static readonly List<object> logQueue = new List<object>();
		private static readonly List<MainThreadCallCallback> mainThreadCallCallbacks = new List<MainThreadCallCallback>();
		public static bool IsInstantiatingPlayer = false;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            MethodInfo gameStartMethod = AccessTools.Method(typeof(GameManager), "Start");
            MethodInfo gameStartPatchMethod = AccessTools.Method(typeof(Plugin), "GameManager_Start_Postfix");
            harmony.Patch(gameStartMethod, null, new HarmonyMethod(gameStartPatchMethod));

            MethodInfo playerAct00AwakeMethod = AccessTools.Method(typeof(PlayerAct_00), "Awake");
            MethodInfo playerAct00AwakePatchMethod = AccessTools.Method(typeof(Plugin), "PlayerAct_00_Awake_Prefix");
            harmony.Patch(playerAct00AwakeMethod, new HarmonyMethod(playerAct00AwakePatchMethod));

            MethodInfo playerPrefAwakeMethod = AccessTools.Method(typeof(PlayerPref), "Awake");
            MethodInfo playerPrefAwakePatchMethod = AccessTools.Method(typeof(Plugin), "PlayerPref_Awake_Prefix");
            harmony.Patch(playerPrefAwakeMethod, new HarmonyMethod(playerPrefAwakePatchMethod));

            MethodInfo uiWeponChangeWeponMethod = AccessTools.Method(typeof(UI_weponIcon), "ChangeWepon");
            MethodInfo uiWeponUseGranadeMethod = AccessTools.Method(typeof(UI_weponIcon), "UseGranade");
            MethodInfo uiWeponPrefixMethod = AccessTools.Method(typeof(Plugin), "UI_weponIcon_Prefix");
            harmony.Patch(uiWeponChangeWeponMethod, new HarmonyMethod(uiWeponPrefixMethod));
            harmony.Patch(uiWeponUseGranadeMethod, new HarmonyMethod(uiWeponPrefixMethod));

            MethodInfo testa = AccessTools.Method(typeof(PlayerAct_00), "Update");
            MethodInfo testb = AccessTools.Method(typeof(Plugin), "TESTB");
            harmony.Patch(testa, new HarmonyMethod(testb));

			m_Instance = this;
			mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

		public static void TESTB(PlayerAct_00 __instance)
		{
			Log(__instance.AFGet<AimIK>("_AimIK"));
		}

        private void Update()
        {
			lock (logQueue)
			{
				if (logQueue.Count != 0)
				{
					foreach (object data in logQueue)
						Debug.Log(data);
				}
				logQueue.Clear();
			}

			lock (mainThreadCallCallbacks)
			{
				foreach (MainThreadCallCallback callback in mainThreadCallCallbacks)
				{
					try
					{
						callback();
					}
					catch (Exception ex)
					{
						Log(ex);
					}
				}
				mainThreadCallCallbacks.Clear();
			}

			// MULTIPLAYER DEBUG
	        if (Input.GetKeyDown(KeyCode.Y))
	        {
				Log("Creating server...");
				GameNet.CreateServer(25565);
	        }
	        if (Input.GetKeyDown(KeyCode.H))
	        {
				Log("Joining server...");
				GameNet.ConnectToServer("127.0.0.1", 25565);
	        }
	        if (Input.GetKeyDown(KeyCode.N))
	        {
				Log("Disconnecting...");
				GameNet.Disconnect();
	        }
	        if (Input.GetKeyDown(KeyCode.M))
	        {
				Log("Stopping server...");
				GameNet.StopServer();
	        }
	        if (Input.GetKeyDown(KeyCode.K))
	        {
				Log("PLAYER CREATE");
				PlayerExtensions.TryInstantiatePlayer(null);
	        }
        }

		// This function is called when the game starts
		public static void GameManager_Start_Postfix(GameManager __instance)
		{
			GameNet.OnGameStart();
		}

		public static bool PlayerAct_00_Awake_Prefix(PlayerAct_00 __instance)
		{
			return !IsInstantiatingPlayer;
		}

		public static bool PlayerPref_Awake_Prefix(PlayerPref __instance)
		{
			return !IsInstantiatingPlayer;
		}

		public static bool UI_weponIcon_Prefix(UI_weponIcon __instance)
		{
			return __instance.GetComponent<PlayerPref>() == PlayerPref.instance;
		}

		public static void CallOnMainThread(MainThreadCallCallback callback)
		{
			if (IsMainThread)
			{
				callback?.Invoke();
			}
			else
			{
				if (callback != null)
				{
					lock (mainThreadCallCallbacks)
						mainThreadCallCallbacks.Add(callback);
				}
			}
		}

		public static void Log(object data)
		{
			// Logging must happen on the main thread
			if (IsMainThread)
			{
				Debug.Log(data);
			}
			else
			{
				lock (logQueue)
					logQueue.Add(data);
			}
		}

		public static bool IsMainThread
		{
		    get { return Thread.CurrentThread.ManagedThreadId == mainThreadId; }
		}

		public static Plugin Instance { get { return m_Instance; } }
    }
}
