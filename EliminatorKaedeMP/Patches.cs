using K_PlayerControl;
using K_PlayerControl.UI;
using RootMotion.FinalIK;
using static EliminatorKaedeMP.PatchAttr;

namespace EliminatorKaedeMP
{
    public class Patches
    {
        [PatchAttr(typeof(GameManager), "Start", EPatchType.Postfix)]
        static void GameManager_Start_Postfix(GameManager __instance)
        {
            // This function is called after the game starts
		    GameNet.OnGameStart();
        }
        
        [PatchAttr(typeof(PlayerPref), "Awake", EPatchType.Prefix)]
		static bool PlayerPref_Awake_Prefix(PlayerPref __instance)
		{
			return !PlayerExtras.IsInitializingNetPlayer;
		}
        
        [PatchAttr(typeof(UI_weponIcon), "ChangeWepon", EPatchType.Prefix)]
        [PatchAttr(typeof(UI_weponIcon), "UseGranade", EPatchType.Prefix)]
		static bool UI_weponIcon_Prefix(UI_weponIcon __instance)
		{
			// Only run if we are the local player
			return __instance.GetComponent<PlayerPref>() == PlayerPref.instance;
		}

		// PlayerAct_00 ----------------------------------------------------------------
		
        [PatchAttr(typeof(PlayerAct_00), "Awake", EPatchType.Prefix)]
        static bool PlayerAct_00_Awake_Prefix(PlayerAct_00 __instance)
        {
			return !PlayerExtras.IsInitializingNetPlayer;
        }
		
        [PatchAttr(typeof(PlayerAct_00), "Initialize", EPatchType.Prefix)]
		static bool PlayerAct_00_Initialize_Prefix(PlayerAct_00 __instance)
		{
			PlayerControl player = __instance.GetComponent<PlayerControl>();
			if (player != PlayerExtras.GetLocalPlayer())
			{
				PlayerExtras.InitializeNetPlayer(player, player.AFGet<EKMPPlayerPref>("Perf").MPPlayer);
				return false;
			}
			return true;
		}

		/*[PatchAttr(typeof(PlayerAct_00), "Update", EPatchType.Prefix)]
        static void PlayerAct_00_Update_Prefix(PlayerAct_00 __instance)
		{
			Plugin.Log(__instance.AFGet<AimIK>("_AimIK"));
		}*/

		// sh_001_UI_gun ----------------------------------------------------------------


    }
}
