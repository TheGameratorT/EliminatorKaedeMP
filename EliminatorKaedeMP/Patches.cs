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
			/*if (!GameNet.IsNetGame())
				return;*/
			GameNet.GetPlayer(__instance)?.OnUpdate();
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
				player.AFGet<EKMPPlayerPref>("Perf").MPPlayer.InitializeNetPlayer(player);
				return false;
			}
			return true;
		}

		// sh_001_UI_gun ----------------------------------------------------------------
		


		// ToiletEventManager ----------------------------------------------------------------

		/*[PatchAttr(typeof(ToiletEventManager), "Show_IgnorUI", EPatchType.Prefix)]
		static bool ToiletEventManager_Show_IgnorUI_Prefix(ToiletEventManager __instance)
		{
			return true;
		}*/
    }
}
