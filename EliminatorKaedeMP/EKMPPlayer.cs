using K_PlayerControl;
using K_PlayerControl.UI;
using RG_GameCamera.Config;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EliminatorKaedeMP
{
    // This is our multiplayer handle for the player
    public class EKMPPlayer
    {
		public static bool IsNetPlayerCtx = false;

        public NetClient Client = null;
        public PlayerControl PlayerCtrl = null;
        public string Name;
        public uint ID;

		private RectTransform nicknameCanvasRect = null; // This will only exist for other players, not for ours

        // Server - This function is called on the player that sent the packet
        public void OnPacketReceived(byte[] bytes)
        {

        }

        public void OnJoin()
        {
            GameNet.Players.Add(this);

            Plugin.Log(Name + " has joined!");
            
            if (GameNet.IsServer)
            {
                GameNet.Server.NotifyPlayerJoined(this); // This is sent to everyone, except the player who joined

                // This is sent to the player who joined, unless that player is also the server
                GameJoinInfoData joinData;
                joinData.PlayerID = ID;
                joinData.PlayerInfos = new List<PlayerInfoData>();
                foreach (EKMPPlayer mpPlayer in GameNet.Players)
                {
                    PlayerInfoData playerInfo;
                    playerInfo.ID = mpPlayer.ID;
                    playerInfo.Name = mpPlayer.Name;
                    joinData.PlayerInfos.Add(playerInfo);
                }
                Client.SendPacket(Utils.SerializePacket(S2CPacketID.GameJoinInfo, joinData));
            }
        }

        public void OnDisconnect()
        {
            GameNet.Players.Remove(this);
            if (PlayerCtrl != null)
                UnityEngine.Object.Destroy(PlayerCtrl.gameObject);

            Plugin.Log(Name + " has left!");

            if (GameNet.IsServer)
            {
                byte[] bytes = new byte[8];
                Utils.WriteInt(bytes, 0, (int) S2CPacketID.PlayerLeave);
                Utils.WriteInt(bytes, 4, (int) ID);
                GameNet.Server.BroadcastPacket(bytes);
            }
        }

        public void OnMove()
        {

        }

		// Extension of PlayerControl.OnUpdate, runs after
		public void OnUpdate()
		{
			if (PlayerCtrl == null || GameNet.GetLocalPlayer() == PlayerCtrl)
				return;

			Vector3 nametagDirection = (PlayerPref.instance.MainCamera.transform.position - nicknameCanvasRect.position).normalized;
			nicknameCanvasRect.rotation = Quaternion.LookRotation(nametagDirection);
		}

        // Tries to create a player if in game
        public PlayerControl TryInstantiateNetPlayer()
        {
            if (!Utils.IsInGame())
                return null;
            PlayerControl player = GameNet.GetLocalPlayer();
            if (player == null)
                return null;

			IsNetPlayerCtx = true;
            PlayerControl newPlayer = UnityEngine.Object.Instantiate(player.gameObject).GetComponent<PlayerControl>();
            try
            {
                InitializeNetPlayer(newPlayer);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex);
            }
			IsNetPlayerCtx = false;

			PlayerCtrl = newPlayer;
            return newPlayer;
        }

        // Initializes the player in a way that allows for it to be controlled by our network manager
        public void InitializeNetPlayer(PlayerControl player)
        {
            // Custom recreation of PlayerControl.InitializePlayerControl

			Player_Equipment playerEquipment = player.GetComponent<Player_Equipment>();
			PlayerAct_00 playerAct00 = player.GetComponent<PlayerAct_00>();
			PlayerAct_01 playerAct01 = player.GetComponent<PlayerAct_01>();
            Rigidbody rigidbody = player.GetComponent<Rigidbody>();
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();

			// Because the player had this on, we must disable it on the clone before re-initializing otherwise
			// creating a PlayerPref will fail because SetPlayerCharacter will try to call changeWeponID before it can.
			playerEquipment.initialize = false;

			PlayerPref playerPref = CreateNetPlayerPref(player);
            Player_DecalManager decal = Player_DecalManager.Instance;
            Player_Config_manager keyInput = Player_Config_manager.Instance;
            PlayerSound_Manager sound = PlayerSound_Manager.Instance;
            Player_EffectManager effect = Player_EffectManager.Instance;
            
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidbody.isKinematic = false;
            capsule.enabled = true;

			PhysicMaterial zeroFrictionMaterial = new PhysicMaterial();
			zeroFrictionMaterial.dynamicFriction = 0f;
			zeroFrictionMaterial.staticFriction = 0f;
			zeroFrictionMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
			zeroFrictionMaterial.bounciness = 0f;
			zeroFrictionMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
            
			PhysicMaterial highFrictionMaterial = new PhysicMaterial();
			highFrictionMaterial.dynamicFriction = 0f;
			highFrictionMaterial.staticFriction = 1f;
			highFrictionMaterial.bounciness = 0f;

            GrounderFBBIK[] GroundIK_list = new GrounderFBBIK[playerPref.SyncAnimator.Length];
			for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
				GroundIK_list[i] = playerPref.SyncAnimator[i].gameObject.GetComponent<GrounderFBBIK>();

            player.AFSet("Perf", playerPref);
			player.AFSet("Decal", decal);
			player.AFSet("KeyInput", keyInput);
			player.AFSet("Sound", sound);
			player.AFSet("Effect", effect);
			player.AFSet("anim", player.GetComponent<Animator>());
			player.AFSet("Helth", player.GetComponent<Player_Helth>());
			player.AFSet("Player_Equipment", playerEquipment);
			player.AFSet("PlayerAct", playerAct00);
			player.AFSet("PlayerAct01", playerAct01);
			player.AFSet("cameraTransform", playerPref.MainCamera.transform);
			player.AFSet("Buffer_FOV", playerPref.MainCamera.GetComponent<Camera>().fieldOfView);
			player.AFSet("m_Rigidbody", rigidbody);
			player.AFSet("Capsule", capsule);
			player.AFSet("zeroFrictionMaterial", zeroFrictionMaterial);
			player.AFSet("highFrictionMaterial", highFrictionMaterial);
			player.PlayerState = PlayerControl.State.Playable;
			player.FlyState = PlayerControl.Fly.none;
			player.Gimic = PlayerControl.StageGimic.NULL;
			player.FPV_target = null;
			player.AFSet("GroundIK_list", GroundIK_list);
            InitializePlayerEquipment(playerEquipment, playerPref);
            InitializePlayerAct00(playerAct00, playerPref, keyInput);
			InitializePlayerAct01(playerAct01, playerPref, keyInput);
			sound.PlayerSound_ini("AudioPosition");
			player.AFSet("player_ini", true);
			player.AFSet("downforce_store", player.DownForce);
			player.obstacleRaycastStart = player.gameObject.transform.FindDeep("DEMO_Pelvis", false);
			player.AFSet("FPS_CAM_Target", GameObject.Find("FPS_cam_target"));
			
			Player_FootSoundControler playerFootSoundControler = player.GetComponent<Player_FootSoundControler>();
			playerFootSoundControler.AFSet("Pref", playerPref);
			playerFootSoundControler.AFSet("Sound", PlayerSound_Manager.Instance);
			playerFootSoundControler.AFSet("anim", player.GetComponent<Animator>());

			Player_FootSound[] playerFootSounds = player.GetComponentsInChildren<Player_FootSound>();
			foreach (Player_FootSound playerFootSound in playerFootSounds)
			{
				playerFootSound.AFSet("Pref", playerPref);
				playerFootSound.AFSet("PlayerObject", player.gameObject);
				playerFootSound.AFSet("FootSoundCtrl", playerFootSoundControler);
			}

			GameObject nicknameCanvasObj = new GameObject("NicknameCanvas");
			Canvas nicknameCanvas = nicknameCanvasObj.AddComponent<Canvas>();
			nicknameCanvasRect = (RectTransform) nicknameCanvasObj.transform;
			nicknameCanvas.renderMode = RenderMode.WorldSpace;
			nicknameCanvasRect.SetParent(player.gameObject.transform);
			nicknameCanvasRect.localPosition = new Vector3(0.0f, 1.667f, 0.0f);
			nicknameCanvasRect.localRotation = Quaternion.identity;
			nicknameCanvasRect.localScale = new Vector3(0.0025f, 0.0025f, 1.0f);
			nicknameCanvasRect.pivot = new Vector2(0.5f, 0.5f);
			nicknameCanvasRect.sizeDelta = new Vector2(350.0f, 40.0f);

			GameObject nicknameTextObj = new GameObject("NicknameText");
			Text nicknameText = nicknameTextObj.AddComponent<Text>();
			nicknameTextObj.AddComponent<Outline>();
			RectTransform nicknameTextRect = (RectTransform) nicknameTextObj.transform;
			nicknameText.text = Name;
			nicknameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
			nicknameText.fontSize = 30;
			nicknameText.alignment = TextAnchor.MiddleCenter;
			nicknameTextRect.SetParent(nicknameCanvasRect);
			nicknameTextRect.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
			nicknameTextRect.localRotation = Quaternion.identity;
			nicknameTextRect.localScale = new Vector3(-1.0f, 1.0f, 1.0f);
			nicknameTextRect.pivot = new Vector2(0.5f, 0.5f);
			nicknameTextRect.sizeDelta = new Vector2(350.0f, 40.0f);
        }

		private PlayerPref CreateNetPlayerPref(PlayerControl player)
		{
			GameObject playerPrefObj = new GameObject("PlayerPref");
			EKMPPlayerPref playerPref = playerPrefObj.AddComponent<EKMPPlayerPref>();
			playerPref.MPPlayer = this;
			playerPref.PlayerIncetance = player.gameObject;
			playerPref.PerfIncetance = playerPrefObj;
			playerPref.SyncAnimator = PlayerPref.Instance.SyncAnimator;
			playerPref.GameCamera = PlayerPref.Instance.GameCamera; // might need custom dummy
			playerPref.MainCamera = PlayerPref.Instance.MainCamera; // might need custom dummy
			playerPref.DummyCamera = PlayerPref.Instance.DummyCamera; // might need custom dummy
			playerPref.weponList = PlayerPref.Instance.weponList;
			playerPref.sub_weponList = PlayerPref.Instance.sub_weponList;
			playerPref.wepon_static_List = PlayerPref.Instance.wepon_static_List; // might need custom dummy
			playerPref.PlayerData = PlayerPref.Instance.PlayerData; // might need custom dummy
			playerPref.LayerMaskInfo = PlayerPref.Instance.LayerMaskInfo;
			SetPlayerCharacter(playerPref, 0);
			playerPrefObj.AddComponent<UI_weponIcon>();
			return playerPref;
		}

        private static void InitializePlayerEquipment(Player_Equipment playerEquipment, PlayerPref playerPref)
        {
            playerEquipment.AFSet("Perf", playerPref);
			playerEquipment.AFSet("Sound", PlayerSound_Manager.Instance);
			playerEquipment.AFSet("anim", playerEquipment.GetComponent<Animator>());
			playerEquipment.AFSet("_LimbIK", playerEquipment.GetComponent<LimbIK>());
			playerEquipment.AFSet("Act", playerEquipment.GetComponent<PlayerAct_00>());
			playerPref.isMain = true;
			playerEquipment.setWepon(0);
			playerEquipment.setWepon(1);
            playerEquipment.AFSet("weponIcon", playerPref.gameObject.GetComponent<UI_weponIcon>());
			playerEquipment.initialize = true;
        }

        private static void InitializePlayerAct00(PlayerAct_00 playerAct00, PlayerPref playerPref, Player_Config_manager keyInput)
        {
			PlayerAct_00 ogPlayerAct00 = GameNet.GetLocalPlayer().GetComponent<PlayerAct_00>();
			playerAct00.AFSet("config", ogPlayerAct00.AFGet<ThirdPersonConfig>("config"));
			playerAct00.AFSet("fvp_config", ogPlayerAct00.AFGet<FPSConfig>("fvp_config"));
			playerAct00.AFSet("config_col", ogPlayerAct00.AFGet<CollisionConfig>("config_col"));
			playerAct00.AFSet("wepon_prefab", ogPlayerAct00.AFGet<GameObject>("wepon_prefab"));

			playerAct00.AFSet("KeyInput", keyInput);
			playerAct00.AFSet("Sound", PlayerSound_Manager.Instance);
			playerAct00.AFSet("Perf", playerPref);
			playerAct00.AFSet("UI", UI_Interactive.Instance);
			playerAct00.AFSet("PE", playerAct00.GetComponent<Player_Equipment>());
			playerAct00.AFSet("p_Granade", playerAct00.GetComponent<Player_Granade>());
			playerAct00.AFSet("weponIcon", playerPref.GetComponent<UI_weponIcon>());
			playerAct00.AFSet("_AimIK", playerAct00.GetComponent<AimIK>());
			playerAct00.AFSet("m_rigidBody", playerAct00.GetComponent<Rigidbody>());
			playerAct00.player_ini = false;
			playerAct00.AFSet("weponID", playerPref.Main_weponID);
			playerAct00.AFSet("SubID", playerPref.Sub_weponID);
			playerAct00.AFSet("wepon_prefab", playerPref.E_mainWepon);
			playerAct00.AFSet("Sub_prefab", playerPref.E_SubWepon);
			playerAct00.AFSet("a_hash_AngleV", Animator.StringToHash("angleV"));
			playerAct00.AFSet("a_hash_AngleH", Animator.StringToHash("angleH"));
			playerAct00.AFSet("a_shotFloat", Animator.StringToHash("ShotFloat"));
			playerAct00.AFSet("a_SHOT", Animator.StringToHash("Shot"));
			playerAct00.AFSet("a_RELOAD", Animator.StringToHash("Reload"));
			playerAct00.AFSet("a_NearWall", Animator.StringToHash("NearWall"));
			playerAct00.AFSet("a_Aim", Animator.StringToHash("Aim"));
			playerAct00.AFSet("a_TurnFloat", Animator.StringToHash("TurnFloat"));
			playerAct00.AFSet("a_weponid", Animator.StringToHash("WeponID"));
			playerAct00.AFSet("a_subid", Animator.StringToHash("SubID"));

			AimIK[] AimIKs = new AimIK[playerPref.SyncAnimator.Length];
			playerAct00.AFSet("AimIKs", AimIKs);
			for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
			{
				AimIKs[i] = playerPref.SyncAnimator[i].gameObject.GetComponent<AimIK>();
			}

			if (playerPref.isMain)
			{
				GameObject weponPrefab = playerAct00.AFGet<GameObject>("wepon_prefab");
				if (weponPrefab != null)
				{
					playerAct00.LayerName = "Gun_act_" + playerAct00.AFGet<int>("weponID");
					playerAct00.ReloadComp_wep();
				}
				else
				{
					playerAct00.AFSet("Kaede_mag", null);
					playerAct00.g_anim = null;
					Debug.Log(weponPrefab + "::: null");
				}
			}
			else if (playerPref.isSub)
			{
				if (playerAct00.AFGet<GameObject>("Sub_prefab") != null)
				{
					playerAct00.LayerName = "Sub_act_" + playerAct00.AFGet<int>("SubID");
					playerAct00.ReloadComp_wep();
				}
				else
				{
					playerAct00.AFSet("Kaede_mag", null);
					playerAct00.g_anim = null;
				}
			}
			else
			{
				playerAct00.AFSet("Kaede_mag", null);
				playerAct00.g_anim = null;
			}

			playerAct00.AFSet("PlayerControl", playerAct00.GetComponent<PlayerControl>());
			playerAct00.anim = playerAct00.GetComponent<Animator>();
			playerAct00.AFSet("cameraTransform", playerPref.MainCamera.transform);
			playerAct00.player_ini = true;
			playerAct00.RELOAD_STEP = PlayerAct_00.ReloadState.None;
        }

		private static void InitializePlayerAct01(PlayerAct_01 playerAct01, PlayerPref playerPref, Player_Config_manager keyInput)
		{
			playerAct01.AFSet("Perf", playerPref);
			playerAct01.AFSet("KeyInput", keyInput);
			playerAct01.AFSet("Sound", PlayerSound_Manager.Instance);
			playerAct01.AFSet("playerControl", playerAct01.GetComponent<PlayerControl>());
			playerAct01.AFSet("act", playerAct01.GetComponent<PlayerAct_00>());
			Animator anim = playerAct01.GetComponent<Animator>();
			playerAct01.AFSet("anim", anim);
			int cqb_0 = anim.GetLayerIndex("CQB_0");
			playerAct01.AFSet("cqb_0", cqb_0);
			playerAct01.AFSet("cqb_1", anim.GetLayerIndex("Kaede_motion"));
			anim.SetLayerWeight(cqb_0, 0f);
			for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
				playerPref.SyncAnimator[i].SetLayerWeight(cqb_0, 0f);
			playerAct01.AFSet("player_ini", true);
		}

		private static void SetPlayerCharacter(PlayerPref playerPref, int characterID)
		{
			for (int i = 0; i < playerPref.PlayerData.Length; i++)
            {
                for (int j = 0; j < playerPref.PlayerData[i].PlayerData.Length; j++)
                {
                    playerPref.PlayerData[i].PlayerData[j].SetActive(value: false);
                }
            }

            for (int k = 0; k < playerPref.PlayerData[characterID].PlayerData.Length; k++)
            {
                playerPref.PlayerData[characterID].PlayerData[k].SetActive(value: true);
            }

            if (playerPref.PlayerIncetance.GetComponent<Player_Equipment>().initialize)
            {
                playerPref.changeWeponID(playerPref.Main_weponID);
            }

            playerPref.PlayerCharacterID = characterID;
		}
    }
}
