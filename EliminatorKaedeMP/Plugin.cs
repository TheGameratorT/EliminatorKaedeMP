using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using UnityEngine;

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

			Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);

			MethodInfo[] methods = typeof(Patches).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo method in methods)
			{
				PatchAttr[] attributes = (PatchAttr[])method.GetCustomAttributes(typeof(PatchAttr), false);
				foreach (PatchAttr attribute in attributes)
				{
					MethodInfo targetMethod = AccessTools.Method(attribute.TargetClass, attribute.TargetMethod);
					if (attribute.PatchType == PatchAttr.EPatchType.Prefix)
						harmony.Patch(targetMethod, new HarmonyMethod(method));
					else if (attribute.PatchType == PatchAttr.EPatchType.Postfix)
						harmony.Patch(targetMethod, null, new HarmonyMethod(method));
					else
						throw new InvalidEnumArgumentException("PatchType", (int)attribute.PatchType, typeof(PatchAttr.EPatchType));
				}
			}

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
			if (Input.GetKeyDown(KeyCode.K))
			{
				Log("Plugin -> Create Test Player");
				if (GameNet.IsServer)
				{
					EKMPPlayerInfo playerInfo = new EKMPPlayerInfo();
					GameNet.InitLocalPlayerInfo(playerInfo);
					playerInfo.ID = 1;
					playerInfo.Name = "Test Player";
					playerInfo.CharacterID = 0;
					playerInfo.S_HairStyle = 2;
					playerInfo.S_MatColor[6] = Color.green;
					EKMPPlayer player = new EKMPPlayer();
					player.Initialize(null, playerInfo);
					Log("Test Player Instantiated");
				}
				else
				{
					Log("Test Player instantiation failed, please start the server first!");
				}
			}
			if (Input.GetKeyDown(KeyCode.I))
			{
				Log("Plugin -> Searching");
				foreach (GameObject obj in FindObjectsOfType<GameObject>())
				{
					SkinnedMeshRenderer mr = obj.GetComponent<SkinnedMeshRenderer>();
					if (mr != null)
					{
						foreach (Material material in mr.materials)
						{
							if (material.name.StartsWith("kaede_Skin_00_diff") ||
								material.name.StartsWith("m_Eyes") ||
								material.name.StartsWith("UnderhairMaterial") ||
								material.name.StartsWith("hair_mat") ||
								material.name.StartsWith("lambert1") ||
								material.name.StartsWith("eye_White"))
							{
								Log(material.name + ": " + Utils.GetGameObjectPath(obj));
							}
						}
					}
				}
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
