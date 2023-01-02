using K_PlayerControl;
using K_PlayerControl.UI;
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



		// ToiletEventManager ----------------------------------------------------------------

		[PatchAttr(typeof(ToiletEventManager), "Start", EPatchType.Prefix)]
		static bool ToiletEventManager_Start_Prefix(ToiletEventManager __instance)
		{
			// Only run if we are the local player
			return !EKMPPlayer.IsNetPlayerCtx;
		}

		[PatchAttr(typeof(ToiletEventManager), "isOutPantu", EPatchType.Prefix)]
		static bool ToiletEventManager_isOutPantu_Prefix(ToiletEventManager __instance, ref bool __result)
		{
			if (__instance.CS == null)
			{
				__result = false;
				return false;
			}
			return true;
		}

		[PatchAttr(typeof(ToiletEventManager), "isSetPantu", EPatchType.Prefix)]
		static bool ToiletEventManager_isSetPantu_Prefix(ToiletEventManager __instance, ref bool __result)
		{
			if (__instance.CS == null)
			{
				__result = true;
				return false;
			}
			return true;
		}

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
