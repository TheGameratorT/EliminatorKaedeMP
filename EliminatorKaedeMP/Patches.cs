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

		// Broadcast toilet state changes to other players when a key state is reached.
		[PatchAttr(typeof(ToiletEventManager), "FixedUpdate", EPatchType.Postfix)]
		static void ToiletEventManager_FixedUpdate_Postfix(ToiletEventManager __instance)
		{
			var localPlayer = GameNet.GetLocalPlayer();

			// Only send broadcasts from the local player, not from remote puppets.
			if (__instance.Player != localPlayer.gameObject)
				return;

			// Track the previous state to detect changes.
			if (!s_toiletStateLookup.TryGetValue(__instance, out var prevState))
				prevState = ToiletEventManager.State.NULL;

			ToiletEventManager.State curState = __instance.ToiletState;
			s_toiletStateLookup[__instance] = curState;

			// Only broadcast on state change and only for "key states" that trigger effects.
			if (curState == prevState)
				return;

			string stateName = curState.ToString();
			bool isKeyState = stateName.EndsWith("_s") ||
							  curState == ToiletEventManager.State.Toilet_End ||
							  curState == ToiletEventManager.State.Idle;
			if (!isKeyState)
			{
				Plugin.Log($"[TOILET] State changed {prevState} -> {curState} but not a key state");
				return;
			}

			Plugin.Log($"[TOILET] Key state detected: {prevState} -> {curState}");

			// Get the EKMPPlayer for this player and broadcast the state change
			EKMPPlayer mpPlayer = GameNet.GetPlayer(localPlayer);
			if (mpPlayer != null)
			{
				Plugin.Log($"[TOILET] Broadcasting toilet state change: type={__instance.toilet}, state={curState}");
				mpPlayer.BroadcastToiletStateChange(__instance.toilet, curState);
			}
			else
			{
				Plugin.Log($"[TOILET] ERROR: Could not find EKMPPlayer for toilet state change");
			}
		}

		private static Dictionary<ToiletEventManager, ToiletEventManager.State> s_toiletStateLookup =
			new Dictionary<ToiletEventManager, ToiletEventManager.State>();

		// [PatchAttr(typeof(ToiletEventManager), "isOutPantu", EPatchType.Prefix)]
		// static bool ToiletEventManager_isOutPantu_Prefix(ToiletEventManager __instance, ref bool __result)
		// {
		// 	if (__instance.CS == null)
		// 	{
		// 		__result = false;
		// 		return false;
		// 	}
		// 	return true;
		// }

		// [PatchAttr(typeof(ToiletEventManager), "isSetPantu", EPatchType.Prefix)]
		// static bool ToiletEventManager_isSetPantu_Prefix(ToiletEventManager __instance, ref bool __result)
		// {
		// 	if (__instance.CS == null)
		// 	{
		// 		__result = true;
		// 		return false;
		// 	}
		// 	return true;
		// }

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
	}
}
