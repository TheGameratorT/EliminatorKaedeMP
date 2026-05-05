using K_PlayerControl;
using K_PlayerControl.UI;
using RG_GameCamera.CharacterController;
using System.Collections.Generic;
using UnityEngine;
using static EliminatorKaedeMP.PatchAttr;

namespace EliminatorKaedeMP
{
	public class Patches
	{
		// GameManager ----------------------------------------------------------------

		[PatchAttr(typeof(GameManager), "Start", EPatchType.Postfix)]
		static void GameManager_Start_Postfix(GameManager __instance)
		{
			// This function is called after the game starts
			GameNet.OnGameStart();
		}

		// UI_corutineTest / UI_pauseMenu ----------------------------------------------------------------

		// The name-entry overlay sets timeScale=0 in Start; prevent that in network play.
		[PatchAttr(typeof(UI_corutineTest), "Start", EPatchType.Postfix)]
		static void UI_corutineTest_Start_Postfix()
		{
			if (GameNet.IsNetGame())
				Time.timeScale = 1f;
		}

		// The pause menu sets timeScale=0 on open; prevent that in network play.
		[PatchAttr(typeof(UI_pauseMenu), "OpnePause", EPatchType.Postfix)]
		static void UI_pauseMenu_OpnePause_Postfix()
		{
			if (GameNet.IsNetGame())
				Time.timeScale = 1f;
		}

		// The pause menu restores timeScale from Player_Helth.TimeScale on close,
		// which can be < 1 under sick effects; keep it at 1 in network play.
		[PatchAttr(typeof(UI_pauseMenu), "ClosePause", EPatchType.Postfix)]
		static void UI_pauseMenu_ClosePause_Postfix()
		{
			if (GameNet.IsNetGame())
				Time.timeScale = 1f;
		}

		// SetPause disables PlayerControl/PlayerAct so Update/LateUpdate stop firing,
		// which halts state-send in network play.  Skip it entirely in net mode.
		[PatchAttr(typeof(GameManager), "SetPause", EPatchType.Prefix)]
		static bool GameManager_SetPause_Prefix()
		{
			return !GameNet.IsNetGame();
		}

		// IgnorPlayerControl also disables PlayerControl, which halts EKMPPlayer.Update/
		// LateUpdate during toilet events.  Let the original run (it sets Rigidbody
		// kinematic, disables IK, etc.) but then re-enable PlayerControl so state packets
		// keep firing.  PlayerAct_00/01 stay disabled — no weapon input during events.
		[PatchAttr(typeof(GameManager), "IgnorPlayerControl", EPatchType.Postfix)]
		static void GameManager_IgnorPlayerControl_Postfix(GameManager __instance)
		{
			if (!GameNet.IsNetGame()) return;
			__instance.Perf.PlayerIncetance.GetComponent<PlayerControl>().enabled = true;
		}

		// PlayerPref ----------------------------------------------------------------

		[PatchAttr(typeof(PlayerPref), "Awake", EPatchType.Prefix)]
		static bool PlayerPref_Awake_Prefix(PlayerPref __instance)
		{
			// Only run if we are the local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(PlayerPref), "changePlayableCharacter", EPatchType.Prefix)]
		static bool PlayerPref_changePlayableCharacter_Prefix(PlayerPref __instance, int Num)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance.PlayerIncetance?.GetComponent<PlayerControl>());
			if (player == null)
				return true;
			player.SetPlayerCharacter(Num);
			return false;
		}

		// UI_weponIcon ----------------------------------------------------------------

		[PatchAttr(typeof(UI_weponIcon), "ChangeWepon", EPatchType.Prefix)]
		[PatchAttr(typeof(UI_weponIcon), "UseGranade", EPatchType.Prefix)]
		static bool UI_weponIcon_Prefix(UI_weponIcon __instance)
		{
			// Only run if we are the local player
			return __instance.GetComponent<PlayerPref>() == PlayerPref.instance;
		}

		// PlayerControl ----------------------------------------------------------------

		[PatchAttr(typeof(PlayerControl), "Update", EPatchType.Postfix)]
		static void PlayerControl_Update_Postfix(PlayerControl __instance)
		{
			GameNet.GetPlayer(__instance)?.Update();
		}

		[PatchAttr(typeof(PlayerControl), "JumpManagement", EPatchType.Prefix)]
		static bool PlayerControl_JumpManagement_Prefix(PlayerControl __instance)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance);
			if (player == null)
				return true;
			player.JumpManagement();
			return false;
		}

		[PatchAttr(typeof(PlayerControl), "LateUpdate", EPatchType.Prefix)]
		static bool PlayerControl_LateUpdate_Prefix(PlayerControl __instance)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance);
			if (player == null)
				return true;
			player.LateUpdate();
			return false;
		}

		// PlayerAct_00 ----------------------------------------------------------------

		[PatchAttr(typeof(PlayerAct_00), "Awake", EPatchType.Prefix)]
		static bool PlayerAct_00_Awake_Prefix(PlayerAct_00 __instance)
		{
			// Only run if we are the local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(PlayerAct_00), "Initialize", EPatchType.Prefix)]
		static bool PlayerAct_00_Initialize_Prefix(PlayerAct_00 __instance)
		{
			PlayerControl player = __instance.GetComponent<PlayerControl>();
			if (player != GameNet.GetLocalPlayer())
			{
				EKMPPlayer.IsNetPlayerCtx = true;
				EKMPPlayer.InitializePlayerAct00(player);
				EKMPPlayer.IsNetPlayerCtx = false;
				return false;
			}
			return true;
		}

		// Skip Update for remote players; aiming direction is calculated from network data.
		[PatchAttr(typeof(PlayerAct_00), "Update", EPatchType.Prefix)]
		static bool PlayerAct_00_Update_Prefix(PlayerAct_00 __instance)
		{
			PlayerControl player = __instance.GetComponent<PlayerControl>();
			return player == GameNet.GetLocalPlayer();
		}

		// PlayerAct_01 ----------------------------------------------------------------

		[PatchAttr(typeof(PlayerAct_01), "Update", EPatchType.Prefix)]
		static bool PlayerAct_01_Update_Prefix(PlayerAct_01 __instance)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance.playerControl);
			if (player == null)
				return true;
			player.Act01Update(__instance);
			return false;
		}

		// sh_001_UI_gun ----------------------------------------------------------------



		// PlayerControl combat events ----------------------------------------------------------------

		// Mirror DamageExploFront to remote players.  We reproduce the front/back
		// calculation here rather than in EKMPPlayer so we can pass a simple bool
		// over the network instead of sending the raw InputVector.
		[PatchAttr(typeof(PlayerControl), "DamageExploFront", EPatchType.Prefix)]
		static void PlayerControl_DamageExploFront_Prefix(PlayerControl __instance, Vector3 InputVector)
		{
			if (__instance.PlayerState != PlayerControl.State.Playable) return;
			EKMPPlayer player = GameNet.GetPlayer(__instance);
			if (player == null || __instance != GameNet.GetLocalPlayer()) return;

			Vector3 pos = __instance.transform.position;
			pos.y = 0f;
			Vector3 vec = InputVector;
			vec.y = 0f;
			Vector3 fwd = __instance.transform.TransformDirection(Vector3.forward);
			float angle = Vector3.Angle(fwd, pos - vec);
			if (Vector3.Cross(fwd, pos - vec).y < 0f)
				angle *= -1f;
			player.SendDamageExploFront(angle < 90f && angle > -90f);
		}

		[PatchAttr(typeof(PlayerControl), "Vomit", EPatchType.Prefix)]
		static void PlayerControl_Vomit_Prefix(PlayerControl __instance)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance);
			if (player == null || __instance != GameNet.GetLocalPlayer()) return;
			if (__instance.IsGrounded() && __instance.PlayerState == PlayerControl.State.Playable && !__instance.IsAiming())
				player.SendVomitEvent();
		}

		// shotAct is private — Harmony can still patch it by name via AccessTools.
		[PatchAttr(typeof(PlayerAct_00), "shotAct", EPatchType.Postfix)]
		static void PlayerAct_00_shotAct_Postfix(PlayerAct_00 __instance)
		{
			EKMPPlayer player = GameNet.GetPlayer(__instance.PlayerControl);
			if (player == null || __instance.PlayerControl != GameNet.GetLocalPlayer()) return;
			player.SendGunFire();
		}

		// ToiletEventManager ----------------------------------------------------------------

		[PatchAttr(typeof(ToiletEventManager), "Start", EPatchType.Prefix)]
		static bool ToiletEventManager_Start_Prefix(ToiletEventManager __instance)
		{
			var localPlayer = GameNet.GetLocalPlayer();

			// Only run if we are the local player.
			// Let EKMPPlayer.InitializeToiletEventManager initialize.
			return localPlayer.Perf.GetComponent<ToiletEventManager>() == __instance;
		}

		// Set network context flag for remote players during FixedUpdate
		[PatchAttr(typeof(ToiletEventManager), "FixedUpdate", EPatchType.Prefix)]
		static void ToiletEventManager_FixedUpdate_Prefix(ToiletEventManager __instance)
		{
			var localPlayer = GameNet.GetLocalPlayer();
			if (__instance.Player != localPlayer.gameObject)
			{
				// Remote player toilet manager: set flag so UI systems skip local-only operations
				EKMPPlayer.IsNetPlayerCtx = true;
			}
		}

		// Broadcast toilet state changes to other players when a key state is reached.
		// Also unset network context flag after FixedUpdate completes.
		[PatchAttr(typeof(ToiletEventManager), "FixedUpdate", EPatchType.Postfix)]
		static void ToiletEventManager_FixedUpdate_Postfix(ToiletEventManager __instance)
		{
			// Always unset network context flag
			EKMPPlayer.IsNetPlayerCtx = false;

			var localPlayer = GameNet.GetLocalPlayer();

			// Only send broadcasts from the local player, not from remote puppets.
			if (__instance.Player != localPlayer.gameObject)
				return;

			ToiletEventManager.State curState = __instance.ToiletState;

			// Only broadcast on state change.
			if (curState == s_toiletPrevState)
				return;

			s_toiletPrevState = curState;

			// Determine if this is a key state that should be broadcast to remote players.
			bool shouldBroadcast = IsKeyToiletState(curState, s_toiletPrevState);

			if (!shouldBroadcast)
			{
				Plugin.Log($"[TOILET] State changed {s_toiletPrevState} -> {curState} but not a key state, not broadcasting");
				return;
			}

			Plugin.Log($"[TOILET] Key state detected: {s_toiletPrevState} -> {curState}");

			// Get the EKMPPlayer for this player and broadcast the state change
			EKMPPlayer mpPlayer = GameNet.GetPlayer(localPlayer);
			if (mpPlayer != null)
			{
				// Build paths to toilet-related objects relative to scene root
				string toiletObjectPath = GetGameObjectPath(__instance.ToiletObject, null);
				string startPointPath = GetGameObjectPath(__instance.StartPoint, null);
				string wayPointPath = GetGameObjectPath(__instance.WayPoint, null);
				string cameraPositionPath = GetGameObjectPath(__instance.cameraPosition, null);

				mpPlayer.SendToiletStateData(__instance.toilet, curState, toiletObjectPath, startPointPath, wayPointPath, cameraPositionPath);
			}
			else
			{
				Plugin.Log($"[TOILET] ERROR: Could not find EKMPPlayer for toilet state change");
			}
		}

		// Get the relative path of a GameObject from a root transform
		static string GetGameObjectPath(GameObject obj, Transform root)
		{
			if (obj == null)
				return "";

			string path = obj.name;
			Transform current = obj.transform.parent;
			while (current != null && current != root)
			{
				path = current.name + "/" + path;
				current = current.parent;
			}
			return path;
		}

		// // Determine which states are critical for synchronization across the network.
		// // Entry/exit points, toilet type selection, major action starts, and pose/animation changes should be broadcast.
		// static bool IsKeyToiletState(ToiletEventManager.State curState, ToiletEventManager.State prevState)
		// {
		// 	// Entry and initialization states: must broadcast entire initialization sequence
		// 	// so remote players' state machine progresses through all setup steps
		// 	if (curState == ToiletEventManager.State.INI_RemoveWepon_s ||
		// 		curState == ToiletEventManager.State.INI_NoArms)
		// 		return true;

		// 	// Everything else is not a key state
		// 	return false;
		// }

		// Determine which states are critical for synchronization across the network.
		// Entry/exit points, toilet type selection, major action starts, and pose/animation changes should be broadcast.
		static bool IsKeyToiletState(ToiletEventManager.State curState, ToiletEventManager.State prevState)
		{
			// Entry and initialization states: must broadcast entire initialization sequence
			// so remote players' state machine progresses through all setup steps
			if (curState == ToiletEventManager.State.INI_RemoveWepon_s ||
				curState == ToiletEventManager.State.INI_RemoveWepon ||
				curState == ToiletEventManager.State.INI_NoArms ||
				curState == ToiletEventManager.State.INI_Replace)
				return true;


			// Exit point: always broadcast when exiting toilet
			if (curState == ToiletEventManager.State.Toilet_End)
				return true;

			// Appearance-changing states: these affect what remote players see
			if (curState == ToiletEventManager.State.outPantu_start ||
				curState == ToiletEventManager.State.outPantu ||
				curState == ToiletEventManager.State.setPantu_start ||
				curState == ToiletEventManager.State.setPantu)
				return true;

			// Character-related states: Character_Switch and Character_ChangeCloth affect appearance
			if (curState == ToiletEventManager.State.Character_Switch ||
				curState == ToiletEventManager.State.Character_ChangeCloth)
				return true;

			string stateName = curState.ToString();

			// Major action starts: broadcast the "_s" states that trigger major animations/effects
			if (stateName.EndsWith("_s"))
			{
				// Broadcast action start states that have visible/gameplay effects
				// (piss, scat, ona, penetration, toy use, etc.)
				// Skip purely transitional animation states like normal_to_Mpose_s
				if (stateName.Contains("piss") || stateName.Contains("scat") ||
					stateName.Contains("ona") || stateName.Contains("toy") ||
					stateName.Contains("hipup") || stateName.Contains("aomuke") ||
					stateName.Contains("utubuse") || stateName.Contains("mass") ||
					stateName.Contains("Character") || stateName.Contains("Denial"))
					return true;
			}

			// Main action states (non-_s versions that contain action keywords): these are the actual
			// animations that follow action starts. Remote players need to see what animation is playing.
			if (!stateName.EndsWith("_s") && !stateName.Contains("_ini"))
			{
				if (stateName.Contains("piss") || stateName.Contains("scat") ||
					stateName.Contains("ona") || stateName.Contains("toy") ||
					stateName.Contains("hipup") || stateName.Contains("aomuke") ||
					stateName.Contains("utubuse") || stateName.Contains("mass") ||
					stateName.Contains("denial") || stateName.Contains("Closet") ||
					stateName == "W_shitDown" || stateName == "Y_shitDown" ||
					stateName == "D_piss_A_ini" || stateName == "D_scat_ini" ||
					stateName == "B_Character_start")
					return true;
			}

			// Pose transition states: these change character appearance and need to be synchronized
			if (stateName.Contains("reset_to_") || stateName.Contains("_to_Mpose") ||
				stateName.Contains("_to_mpose") || stateName.Contains("_to_normal") ||
				stateName.Contains("_to_standup"))
				return true;

			// Action finalization states: remote players need to know when an action ends
			if (stateName.Contains("_end") || stateName.Contains("_end_s") ||
				stateName.Contains("_fi") || stateName.Contains("_fi_s"))
				return true;

			// Movement states: BackStep and ForwardStep affect player position
			if (curState == ToiletEventManager.State.BackStep_start ||
				curState == ToiletEventManager.State.BackStep ||
				curState == ToiletEventManager.State.ForwardStep_start ||
				curState == ToiletEventManager.State.ForwardStep)
				return true;

			// Everything else is not a key state
			return false;
		}

		private static ToiletEventManager.State s_toiletPrevState = ToiletEventManager.State.NULL;

		/*[PatchAttr(typeof(ToiletEventManager), "Show_IgnorUI", EPatchType.Prefix)]
		static bool ToiletEventManager_Show_IgnorUI_Prefix(ToiletEventManager __instance)
		{
			return true;
		}*/

		// UI_ClothSystem ----------------------------------------------------------------

		[PatchAttr(typeof(UI_ClothSystem), "Start", EPatchType.Prefix)]
		static bool UI_ClothSystem_Start_Prefix(UI_ClothSystem __instance)
		{
			// Only run if we are the local player
			return EKMPPlayer.ClothSystem_GetPlayer(__instance) == GameNet.GetLocalPlayer();
		}

		[PatchAttr(typeof(UI_ClothSystem), "ChangeHairStyle", EPatchType.Prefix)]
		static bool UI_ClothSystem_ChangeHairStyle_Prefix(UI_ClothSystem __instance, int input)
		{
			EKMPPlayer player = GameNet.GetPlayer(EKMPPlayer.ClothSystem_GetPlayer(__instance));
			if (player == null)
				return true;
			player.ClothSystem_ChangeHairStyle(__instance, input);
			return false;
		}

		[PatchAttr(typeof(UI_ClothSystem), "SaveColors", EPatchType.Postfix)]
		static void UI_ClothSystem_SaveColors_Postfix(UI_ClothSystem __instance)
		{
			// This function is only ran by the local player
			GameNet.Player?.SendClothInfoData();
		}

		// UI_cloth_Purchase ----------------------------------------------------------------

		[PatchAttr(typeof(UI_cloth_Purchase), "Start", EPatchType.Prefix)]
		static bool UI_cloth_Purchase_Start_Prefix(UI_cloth_Purchase __instance)
		{
			// Only run if we are the local player
			return EKMPPlayer.ClothPurchase_GetPlayer(__instance) == GameNet.GetLocalPlayer();
		}

		[PatchAttr(typeof(UI_cloth_Purchase), "OnClothSelect", EPatchType.Prefix)]
		static bool UI_cloth_Purchase_OnClothSelect_Prefix(UI_cloth_Purchase __instance, int inputID)
		{
			EKMPPlayer player = GameNet.GetPlayer(EKMPPlayer.ClothPurchase_GetPlayer(__instance));
			if (player == null)
				return true;
			player.ClothPurchase_SelectCloth(__instance, inputID);
			return false;
		}

		// UI_behaviorPanelManager ----------------------------------------------------------------

		[PatchAttr(typeof(UI_behaviorPanelManager), "Start", EPatchType.Prefix)]
		static bool UI_behaviorPanelManager_Start_Prefix()
		{
			// Only run for local player; remote players don't need UI initialization
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(UI_behaviorPanelManager), "CreateBehaviorLists", EPatchType.Prefix)]
		static bool UI_behaviorPanelManager_CreateBehaviorLists_Prefix()
		{
			// Only create UI lists for local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(UI_behaviorPanelManager), "DeleteBehaviroLists", EPatchType.Prefix)]
		static bool UI_behaviorPanelManager_DeleteBehaviroLists_Prefix()
		{
			// Only delete UI lists for local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(UI_behaviorPanelManager), "MovePanel", EPatchType.Prefix)]
		static bool UI_behaviorPanelManager_MovePanel_Prefix()
		{
			// Only move panel for local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		// UI_ShortMessage ----------------------------------------------------------------
		// Skip message display for remote players during toilet events

		[PatchAttr(typeof(UI_ShortMessage), "CallShortMessage", EPatchType.Prefix)]
		static bool UI_ShortMessage_CallShortMessage_Prefix()
		{
			// Only show messages for local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		// sh_001_UI_gun ----------------------------------------------------------------
		// Skip gun UI for remote players during toilet events

		[PatchAttr(typeof(sh_001_UI_gun), "Initialize", EPatchType.Prefix)]
		static bool sh_001_UI_gun_Initialize_Prefix()
		{
			// Only initialize gun UI for local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		// ToiletEventManager - To_toiletType ----------------------------------------------------------------
		// Deactivate behavior panel for remote players (state transitions still happen)

		[PatchAttr(typeof(ToiletEventManager), "To_toiletType", EPatchType.Postfix)]
		static void ToiletEventManager_To_toiletType_Postfix(ToiletEventManager __instance)
		{
			// Let state transitions happen but deactivate the UI panel for remote players
			if (EKMPPlayer.IsNetPlayerCtx)
			{
				__instance.BehaviorPanel.SetActive(false);
			}
		}
	}
}
