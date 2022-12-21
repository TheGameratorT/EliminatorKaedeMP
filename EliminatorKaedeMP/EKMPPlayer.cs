using K_PlayerControl;
using K_PlayerControl.UI;
using RG_GameCamera.Config;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace EliminatorKaedeMP
{
	// This is our multiplayer handle for the player
	public class EKMPPlayer
    {
		public enum CtrlKey
		{
			Aim,
			Crouch
		}

		public static bool IsNetPlayerCtx = false;
		
        public uint ID;
        public NetClient Client = null;
        public string Name;
        public PlayerControl PlayerCtrl = null;

		private RectTransform nicknameCanvasRect = null; // This will only exist for other players, not for ours
		private bool netCtrl_isAiming = false; // For the local player this acts as a last state, like wasAimingInLastFrame
		private bool netCtrl_isCrouching = false; // For the local player this acts as a last state, like wasCrouchingInLastFrame

        // Server - This function is called on the player that sent the packet
        public void OnPacketReceived(byte[] bytes)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(bytes);
                using BinaryReader reader = new BinaryReader(stream);

                C2SPacketID packetID = (C2SPacketID) reader.ReadInt32();
                switch (packetID)
                {
                case C2SPacketID.PlayerMove:
                {
					stream.Position = 4;
					PlayerMoveData playerMoveData = (PlayerMoveData) Utils.Deserialize(stream);
					BroadcastMoveData(playerMoveData);
                    Plugin.CallOnMainThread(() => OnMoveData(playerMoveData));
                    break;
                }
                case C2SPacketID.PlayerJump:
                {
					int jumpType = reader.ReadInt32();
					BroadcastJumpData(jumpType);
                    Plugin.CallOnMainThread(() => OnJumpData(jumpType));
                    break;
                }
                case C2SPacketID.PlayerCtrlKey:
                {
					CtrlKey key = (CtrlKey) reader.ReadInt32();
					bool isDown = reader.ReadInt32() != 0 ? true : false;
					BroadcastControlData(key, isDown);
                    Plugin.CallOnMainThread(() => OnControlData(key, isDown));
                    break;
                }
                /*case C2SPacketID.PlayerCrouch:
                {
					bool crouching = reader.ReadBoolean();
                    Plugin.CallOnMainThread(() => SetCrouching(crouching));
                    break;
                }*/
                default:
                    break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log(ex);
            }
        }
		
		// Server + Client
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
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream))
					{
						writer.Write((int) S2CPacketID.GameJoinInfo);
						Utils.Serialize(writer, joinData);
					}
					bytes = stream.ToArray();
				}
				Client.SendPacket(bytes);
            }
        }

		// Server + Client
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
		
        // Server - Similar to GameServer.BroadcastPacket but doesn't send the packet to this player instance's client
		public void BroadcastPacketExcludingSelf(byte[] bytes)
        {
            foreach (EKMPPlayer player in GameNet.Players)
            {
                if (player.ID != GameNet.Player.ID && player != this)
                    player.Client.SendPacket(bytes);
            }
        }
		
		// Server + Client - Runs when movement data is received
        public void OnMoveData(PlayerMoveData moveData)
        {
			PlayerCtrl.transform.position = moveData.GetPlayerPos();
			PlayerCtrl.transform.rotation = moveData.GetPlayerRot();
        }

		// Server - Sends the movement data of a player to the other clients
		private void BroadcastMoveData(PlayerMoveData moveData)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((int) S2CPacketID.PlayerMove);
					writer.Write(ID);
					Utils.Serialize(writer, moveData);
				}
				bytes = stream.ToArray();
			}
			BroadcastPacketExcludingSelf(bytes);
		}

		// Client - Sends the movement data to the server (only the local player should run this)
        private void SendMoveData()
        {
			PlayerMoveData moveData = new PlayerMoveData();
			moveData.SetPlayerPos(PlayerCtrl.transform.position);
			moveData.SetPlayerRot(PlayerCtrl.transform.rotation);

			if (GameNet.IsServer)
			{
				// If we are a server, there is not point in sending the data to ourselves, just send it to the other clients
				BroadcastMoveData(moveData);
			}
			else
			{
				// But if we are a client, we must send it to the server so that it will broadcast it to other clients
				using (MemoryStream stream = new MemoryStream())
				{
					using (BinaryWriter writer = new BinaryWriter(stream))
					{
						writer.Write((int) C2SPacketID.PlayerMove);
						Utils.Serialize(writer, moveData);
					}
					Client.SendPacket(stream.ToArray());
				}
			}
        }

		// Client - Extension of PlayerControl.Update, runs after
		public void Update()
		{
			if (PlayerCtrl == null)
				return;

			if (GameNet.GetLocalPlayer() == PlayerCtrl)
			{
				SendMoveData();

				bool isAiming = PlayerCtrl.IsAiming();
				if (netCtrl_isAiming != isAiming)
				{
					SendControlData(CtrlKey.Aim, isAiming);
					netCtrl_isAiming = isAiming;
				}

				bool isCrouching = PlayerCtrl.IsCrouch();
				if (netCtrl_isCrouching != isCrouching)
				{
					SendControlData(CtrlKey.Crouch, isCrouching);
					netCtrl_isCrouching = isCrouching;
				}
			}
			else
			{
				Vector3 nametagDirection = (PlayerPref.instance.MainCamera.transform.position - nicknameCanvasRect.position).normalized;
				nicknameCanvasRect.rotation = Quaternion.LookRotation(nametagDirection);
			}
		}

        // Server + Client - Tries to create a player if in game
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

        // Server + Client - Initializes the player in a way that allows for it to be controlled by our network manager
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
            InitializePlayerAct00(player);
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

			Transform playerHeadTransform = player.gameObject.transform.Find("Root/DEMO_Pelvis/DEMO_Spine/DEMO_Spine1/Spine_1_5/DEMO_Spine2/DEMO_Spine3/DEMO_Neck/DEMO_Neck2/DEMO_Head");

			GameObject nicknameCanvasObj = new GameObject("NicknameCanvas");
			Canvas nicknameCanvas = nicknameCanvasObj.AddComponent<Canvas>();
			nicknameCanvasRect = (RectTransform) nicknameCanvasObj.transform;
			nicknameCanvas.renderMode = RenderMode.WorldSpace;
			nicknameCanvasRect.SetParent(playerHeadTransform);
			nicknameCanvasRect.localPosition = new Vector3(0.0f, 0.28f, 0.0f);
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

		// Server + Client
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
		
		// Server + Client
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
		
		// Server + Client
        public static void InitializePlayerAct00(PlayerControl player)
        {
			PlayerAct_00 playerAct00 = player.GetComponent<PlayerAct_00>();
			PlayerPref playerPref = player.AFGet<PlayerPref>("Perf");

			PlayerAct_00 ogPlayerAct00 = GameNet.GetLocalPlayer().GetComponent<PlayerAct_00>();
			playerAct00.AFSet("config", ogPlayerAct00.AFGet<ThirdPersonConfig>("config"));
			playerAct00.AFSet("fvp_config", ogPlayerAct00.AFGet<FPSConfig>("fvp_config"));
			playerAct00.AFSet("config_col", ogPlayerAct00.AFGet<CollisionConfig>("config_col"));
			playerAct00.AFSet("wepon_prefab", ogPlayerAct00.AFGet<GameObject>("wepon_prefab"));

			playerAct00.AFSet("KeyInput", player.AFGet<Player_Config_manager>("KeyInput"));
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
		
		// Server + Client
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
		
		// Server + Client
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

		private static void BeginJumpRoll(PlayerAct_00 playerAct00, PlayerPref pref, PlayerSound_Manager sound, Animator anim, string stateName)
		{
			sound.SetSound_FootStep(null, 14, 1f);
			anim.CrossFadeInFixedTime(stateName, 0.05f);
			for (int i = 0; i < pref.SyncAnimator.Length; i++)
				pref.SyncAnimator[i].CrossFadeInFixedTime(stateName, 0.05f);
			playerAct00.AimControl_cancel();
		}

		private static void BeginJumpType4(PlayerControl player)
		{
			Vector3 storeVector = new Vector3(0f, player.jumpHeight * 1.5f, 0f);
			player.AFSet("m_storeVector", storeVector);
			player.AFGet<Rigidbody>("m_Rigidbody").velocity = storeVector;
		}

		private static void BeginJumpType5(PlayerControl player, PlayerPref pref, Animator anim)
		{
			player.AFSet("m_storeVector", new Vector3(0f, player.jumpHeight, 0f));
			anim.CrossFadeInFixedTime("Kaede_motion.Jump", 0.05f);
			for (int n = 0; n < pref.SyncAnimator.Length; n++)
				pref.SyncAnimator[n].CrossFadeInFixedTime("Kaede_motion.Jump", 0.05f);
		}

		private static void EndCrouchAnim(PlayerControl player, PlayerPref pref, Animator anim)
		{
			player.AFSet("crouch", false);
			anim.SetFloat("CrouchFloat", 0f);
			for (int m = 0; m < pref.SyncAnimator.Length; m++)
				pref.SyncAnimator[m].SetFloat("CrouchFloat", 0f);
		}

		// Server + Client - Runs when jump data is received
        public void OnJumpData(int jumpType)
        {
			PlayerControl player = PlayerCtrl;

			if (jumpType == 0)
				return;
			
			PlayerPref pref = player.AFGet<PlayerPref>("Perf");
			PlayerAct_00 playerAct00 = player.AFGet<PlayerAct_00>("PlayerAct");
			Animator anim = player.AFGet<Animator>("anim");
			PlayerSound_Manager sound = player.AFGet<PlayerSound_Manager>("Sound");

			if (jumpType == 1)
			{
				BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_forward");
			}
			else if (jumpType == 2)
			{
				BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_back");
			}
			else if (jumpType >= 3 && jumpType <= 5)
			{
				EndCrouchAnim(player, pref, anim);
				if (jumpType == 4)
				{
					BeginJumpType4(player);
				}
				else if (jumpType == 5)
				{
					BeginJumpType5(player, pref, anim);
				}
			}
        }

		// Server - Sends the jump data of a player to the other clients
		private void BroadcastJumpData(int jumpType)
		{
			byte[] jumpData = new byte[sizeof(int) * 3];
			Utils.WriteInt(jumpData, 0, (int) S2CPacketID.PlayerJump);
			Utils.WriteInt(jumpData, 4, (int) ID);
			Utils.WriteInt(jumpData, 8, jumpType);
			BroadcastPacketExcludingSelf(jumpData);
		}

		// Client - Sends the jump data to the server (only the local player should run this)
        private void SendJumpData(int jumpType)
        {
			if (GameNet.IsServer)
			{
				// If we are a server, there is not point in sending the data to ourselves, just send it to the other clients
				BroadcastJumpData(jumpType);
			}
			else
			{
				// But if we are a client, we must send it to the server so that it will broadcast it to other clients
				byte[] jumpData = new byte[sizeof(int) * 2];
				Utils.WriteInt(jumpData, 0, (int) C2SPacketID.PlayerJump);
				Utils.WriteInt(jumpData, 4, jumpType);
				Client.SendPacket(jumpData);
			}
        }

		// Server + Client - Replacement for PlayerControl.JumpManagement
		public void JumpManagement()
		{
			PlayerControl player = PlayerCtrl;

			if (player != GameNet.GetLocalPlayer() || player.FlyState != PlayerControl.Fly.none)
				return;
			
			PlayerPref pref = player.AFGet<PlayerPref>("Perf");
			Player_Helth health = player.AFGet<Player_Helth>("Helth");
			Player_Config_manager keyInput = player.AFGet<Player_Config_manager>("KeyInput");
			PlayerAct_00 playerAct00 = player.AFGet<PlayerAct_00>("PlayerAct");
			Animator anim = player.AFGet<Animator>("anim");
			PlayerSound_Manager sound = player.AFGet<PlayerSound_Manager>("Sound");

			AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
			player.AFSet("stateInfo", stateInfo);

			if (player.timeToNextJump > 0f)
				player.timeToNextJump -= Time.deltaTime;

			if (Input.GetKeyDown(keyInput.Jump) && health.Sick < player.Sick_thredhold)
			{
				if (!player.IsGrounded() || player.PlayerState != PlayerControl.State.Playable)
					return;
				if (player.IsAiming())
				{
					if (stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.roll_back") &&
						stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.roll_left") &&
						stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.roll_right") &&
						stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.roll_forward"))
					{
						float float_h = player.AFGet<float>("float_h");
						float float_v = player.AFGet<float>("float_v");
						if (float_h >= 0.5f)
						{
							BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_h <= -0.5f)
						{
							BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_v >= 0.5f)
						{
							BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_v <= -0.5f)
						{
							BeginJumpRoll(playerAct00, pref, sound, anim, "Kaede_motion.roll_back");
							SendJumpData(2);
							return;
						}
					}
				}
				else
				{
					if (stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.Idle_00") &&
						stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.Locomotion") &&
						stateInfo.fullPathHash != Animator.StringToHash("Kaede_motion.crouch"))
						return;

					EndCrouchAnim(player, pref, anim);
					if ((bool) player.AMCall("Checkobstacle").Invoke(player, null))
					{
						player.PlayerState = PlayerControl.State.Obstacle_s;
					}
					else if (!player.AFGet<bool>("aim"))
					{
						if (player.IsMoveing() && player.IsGrounded())
						{
							BeginJumpType4(player);
							SendJumpData(4);
						}
						else
						{
							BeginJumpType5(player, pref, anim);
							SendJumpData(5);
						}
					}
					else
					{
						SendJumpData(3);
					}
				}
			}
		}

		// Server + Client - Runs when control data is received
        public void OnControlData(CtrlKey key, bool isDown)
		{
			switch (key)
			{
			case CtrlKey.Aim:
				netCtrl_isAiming = isDown;
				break;
			case CtrlKey.Crouch:
				netCtrl_isCrouching = isDown;
				break;
			}
		}

		// Server - Sends the key data of a player to the other clients
		private void BroadcastControlData(CtrlKey key, bool isDown)
		{
			byte[] jumpData = new byte[sizeof(int) * 4];
			Utils.WriteInt(jumpData, 0, (int) S2CPacketID.PlayerCtrlKey);
			Utils.WriteInt(jumpData, 4, (int) ID);
			Utils.WriteInt(jumpData, 8, (int) key);
			Utils.WriteInt(jumpData, 12, isDown ? 1 : 0);
			BroadcastPacketExcludingSelf(jumpData);
		}

		// Client - Sends the key data to the server (only the local player should run this)
        private void SendControlData(CtrlKey key, bool isDown)
        {
			if (GameNet.IsServer)
			{
				// If we are a server, there is not point in sending the data to ourselves, just send it to the other clients
				BroadcastControlData(key, isDown);
			}
			else
			{
				// But if we are a client, we must send it to the server so that it will broadcast it to other clients
				byte[] jumpData = new byte[sizeof(int) * 3];
				Utils.WriteInt(jumpData, 0, (int) C2SPacketID.PlayerCtrlKey);
				Utils.WriteInt(jumpData, 4, (int) key);
				Utils.WriteInt(jumpData, 8, isDown ? 1 : 0);
				Client.SendPacket(jumpData);
			}
        }

		// Server + Client - Replacement for PlayerControl.LateUpdate
		public void LateUpdate()
		{
			PlayerControl player = PlayerCtrl;

			if (!player.AFGet<bool>("player_ini") || Time.timeScale <= 0f)
				return;

			bool isLocalPlayer = player == GameNet.GetLocalPlayer();
			
			PlayerPref pref = player.AFGet<PlayerPref>("Perf");
			Player_Helth health = player.AFGet<Player_Helth>("Helth");
			Player_Equipment equipment = player.AFGet<Player_Equipment>("Player_Equipment");
			Player_Config_manager keyInput = player.AFGet<Player_Config_manager>("KeyInput");
			PlayerAct_00 playerAct00 = player.AFGet<PlayerAct_00>("PlayerAct");
			Animator anim = player.AFGet<Animator>("anim");

			if (player.PlayerState == PlayerControl.State.Playable)
			{
				if (isLocalPlayer)
				{
					if (pref.isMain || pref.isSub)
					{
						bool aim = false;
						if (equipment.E_state == Player_Equipment.State.None)
						{
							if (health.Sick < player.Sick_thredhold)
								aim = Input.GetKey(keyInput.WeponHold);
						}
						player.AFSet("aim", aim);
					}
				}
				else
				{
					player.AFSet("aim", netCtrl_isAiming);
				}
				
				bool input_h, input_v;
				float float_h, float_v;
				if (health.Sick >= player.Sick_thredhold && player.IsCrouch())
				{
					input_h = false;
					input_v = false;
					float_h = 0f;
					float_v = 0f;
				}
				else
				{
					float slopeSpeed = (float) player.AMCall("SlopeSpeed").Invoke(player, null);
					if (isLocalPlayer)
					{
						input_h = Input.GetButton("Horizontal");
						input_v = Input.GetButton("Vertical");
						float_h = Input.GetAxis("Horizontal") * slopeSpeed;
						float_v = Input.GetAxis("Vertical") * slopeSpeed;
					}
					else
					{
						input_h = false;
						input_v = false;
						float_h = 0f;
						float_v = 0f;
					}
				}
				player.AFSet("input_h", input_h);
				player.AFSet("input_v", input_v);
				player.AFSet("float_h", float_h);
				player.AFSet("float_v", float_v);

				if (player.IsAiming())
				{
					if (Input.GetKeyDown(keyInput.FirestPersonView))
						player.AMCall(player.AFGet<bool>("FPV") ? "SetTPS" : "SetFPV").Invoke(player, null);
				}
				else
				{
					player.AMCall("SetTPS").Invoke(player, null);
				}
				if (player.AFGet<bool>("FPV") && anim.GetBool(Animator.StringToHash("NearWall")) &&
					(bool) player.AMCall("IsSniper").Invoke(player, null) && pref.isMain)
				{
					player.AMCall("SetTPS").Invoke(player, null);
				}

				if (playerAct00.RELOAD_STEP == PlayerAct_00.ReloadState.None &&
					health.Sick < player.Sick_thredhold &&
					player.PlayerState == PlayerControl.State.Playable)
				{
					float mouseWheel = Input.GetAxis("Mouse ScrollWheel");
					player.AFSet("MouseWheel", mouseWheel);
					if (!player.IsAiming())
					{
						if (mouseWheel < 0f || Input.GetKeyDown(keyInput.ChangeSubWepon))
						{
							if (pref.isMain)
							{
								equipment.switchWepon = true;
								player.AMCall("ChangeEquipment").Invoke(player, new object[]{ 0 });
							}
							else
							{
								player.AMCall("ChangeEquipment").Invoke(player, new object[]{ 1 });
							}
						}
						if (mouseWheel > 0f || Input.GetKeyDown(keyInput.ChangeMainWepon))
						{
							if (pref.isSub)
							{
								equipment.switchWepon = true;
								player.AMCall("ChangeEquipment").Invoke(player, new object[]{ 1 });
							}
							else
							{
								player.AMCall("ChangeEquipment").Invoke(player, new object[]{ 0 });
							}
						}
					}
				}

				bool crouchHandler = isLocalPlayer ? Input.GetKeyDown(keyInput.Crouch) : false;
				player.AFSet("crouchHandler", crouchHandler);
				anim.SetBool("Aim", player.IsAiming());
				anim.SetBool("Crouch", player.IsCrouch());
				anim.SetFloat("H", float_h);
				anim.SetFloat("V", float_v);
				for (int i = 0; i < pref.SyncAnimator.Length; i++)
				{
					pref.SyncAnimator[i].SetBool("Aim", player.IsAiming());
					pref.SyncAnimator[i].SetBool("Crouch", player.IsCrouch());
					pref.SyncAnimator[i].SetFloat("H", float_h);
					pref.SyncAnimator[i].SetFloat("V", float_v);
				}

				if (player.IsCrouch())
				{
					anim.SetFloat("CrouchFloat", 1f, 0.1f, Time.deltaTime);
					for (int j = 0; j < pref.SyncAnimator.Length; j++)
						pref.SyncAnimator[j].SetFloat("CrouchFloat", 1f, 0.1f, Time.deltaTime);
				}
				else
				{
					anim.SetFloat("CrouchFloat", 0f, 0.1f, Time.deltaTime);
					for (int k = 0; k < pref.SyncAnimator.Length; k++)
						pref.SyncAnimator[k].SetFloat("CrouchFloat", 0f, 0.1f, Time.deltaTime);
				}

				if (crouchHandler)
				{
					if (player.IsGrounded())
						player.AFSet("crouch", !player.IsCrouch());
					player.AFSet("crouchHandler", false);
				}
				else if (!isLocalPlayer)
				{
					player.AFSet("crouch", netCtrl_isCrouching);
				}

				player.AMCall("MovementManagement", new Type[]{ typeof(float), typeof(float), typeof(bool), typeof(bool) })
					.Invoke(player, new object[]{ float_h, float_v, player.AFGet<bool>("run"), player.AFGet<bool>("sprint") });

				JumpManagement();

				anim.SetFloat("input_sum", Mathf.Clamp01(Mathf.Abs(float_h) + Mathf.Abs(float_v)));
				for (int l = 0; l < pref.SyncAnimator.Length; l++)
					pref.SyncAnimator[l].SetFloat("input_sum", Mathf.Clamp01(Mathf.Abs(float_h) + Mathf.Abs(float_v)));
			}
			else
			{
				player.AFSet("aim", false);
			}
			player.AMCall("CheckGroundStatus").Invoke(player, null);
		}
    }
}
