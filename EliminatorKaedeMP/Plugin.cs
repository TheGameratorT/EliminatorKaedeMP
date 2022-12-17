using BepInEx;
using HarmonyLib;
using K_PlayerControl;
using System;
using System.Collections.Generic;
using System.Threading;
//using System.Reflection;
using UnityEngine;

public static class PlayerControlExtensions
{
    public static Player_Helth GetHelth(this PlayerControl player)
    {
        return (Player_Helth)AccessTools.Field(typeof(PlayerControl), "Helth").GetValue(player);
    }
}

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

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            //Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            //MethodInfo ogMethod = AccessTools.Method(typeof(UI_DebugConsole), "Update");
            //MethodInfo ptMethod = AccessTools.Method(typeof(Plugin), "Update_Patch");
            //harmony.Patch(ogMethod, new HarmonyMethod(ptMethod));

			m_Instance = this;
			mainThreadId = Thread.CurrentThread.ManagedThreadId;
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
