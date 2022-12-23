using K_PlayerControl;
using K_PlayerControl.UI;
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

		//private const float MovePacketSendInterval = 0.25f;
		public static bool IsNetPlayerCtx = false;

		public uint ID;
		public NetClient Client = null;
		public string Name;
		public PlayerControl PlayerCtrl = null;

		private RectTransform nicknameCanvasRect = null; // This will only exist for other players, not for ours
		private bool netCtrl_isAiming = false; // For the local player this acts as a last state, like wasAimingInLastFrame
		private bool netCtrl_isCrouching = false; // For the local player this acts as a last state, like wasCrouchingInLastFrame
		//private float netCtrl_lastMovePktTime = 0.0f;

		// Server - This function is called on the player that sent the packet
		public void OnPacketReceived(byte[] bytes)
		{
			try
			{
				using MemoryStream stream = new MemoryStream(bytes);
				using BinaryReader reader = new BinaryReader(stream);

				C2SPacketID packetID = (C2SPacketID)reader.ReadInt32();
				switch (packetID)
				{
				case C2SPacketID.PlayerMove:
				{
					stream.Position = 4;
					PlayerMoveData playerMoveData = (PlayerMoveData)Utils.Deserialize(stream);
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
					CtrlKey key = (CtrlKey)reader.ReadInt32();
					bool isDown = reader.ReadInt32() != 0 ? true : false;
					BroadcastControlData(key, isDown);
					Plugin.CallOnMainThread(() => OnControlData(key, isDown));
					break;
				}
				case C2SPacketID.PlayerKnifeUse:
				{
					int step = reader.ReadInt32();
					BroadcastKnifeUseData(step);
					Plugin.CallOnMainThread(() => OnKnifeUseData(step));
					break;
				}
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
						writer.Write((int)S2CPacketID.GameJoinInfo);
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
				Utils.WriteInt(bytes, 0, (int)S2CPacketID.PlayerLeave);
				Utils.WriteInt(bytes, 4, (int)ID);
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
			PlayerCtrl.input_h = moveData.InputH;
			PlayerCtrl.input_v = moveData.InputV;
			PlayerCtrl.float_h = moveData.FloatH;
			PlayerCtrl.float_v = moveData.FloatV;
		}

		// Server - Sends the movement data of a player to the other clients
		private void BroadcastMoveData(PlayerMoveData moveData)
		{
			byte[] bytes;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					writer.Write((int)S2CPacketID.PlayerMove);
					writer.Write(ID);
					Utils.Serialize(writer, moveData);
				}
				bytes = stream.ToArray();
			}
			BroadcastPacketExcludingSelf(bytes);
		}

		// Server + Client - Sends the movement data to the server (only the local player should run this)
		private void SendMoveData()
		{
			PlayerMoveData moveData = new PlayerMoveData();
			moveData.SetPlayerPos(PlayerCtrl.transform.position);
			moveData.SetPlayerRot(PlayerCtrl.transform.rotation);
			moveData.InputH = PlayerCtrl.input_h;
			moveData.InputV = PlayerCtrl.input_v;
			moveData.FloatH = PlayerCtrl.float_h;
			moveData.FloatV = PlayerCtrl.float_v;

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
						writer.Write((int)C2SPacketID.PlayerMove);
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
				/*netCtrl_lastMovePktTime += Time.deltaTime;
				if (netCtrl_lastMovePktTime >= MovePacketSendInterval)
				{*/
					SendMoveData();
				/*	netCtrl_lastMovePktTime = 0;
				}*/

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

			player.Perf = playerPref;
			player.Decal = decal;
			player.KeyInput = keyInput;
			player.Sound = sound;
			player.Effect = effect;
			player.anim = player.GetComponent<Animator>();
			player.Helth = player.GetComponent<Player_Helth>();
			player.Player_Equipment = playerEquipment;
			player.PlayerAct = playerAct00;
			player.PlayerAct01 = playerAct01;
			player.cameraTransform = playerPref.MainCamera.transform;
			player.Buffer_FOV = playerPref.MainCamera.GetComponent<Camera>().fieldOfView;
			player.m_Rigidbody = rigidbody;
			player.Capsule = capsule;
			player.zeroFrictionMaterial = zeroFrictionMaterial;
			player.highFrictionMaterial = highFrictionMaterial;
			player.PlayerState = PlayerControl.State.Playable;
			player.FlyState = PlayerControl.Fly.none;
			player.Gimic = PlayerControl.StageGimic.NULL;
			player.FPV_target = null;
			player.GroundIK_list = GroundIK_list;
			InitializePlayerEquipment(playerEquipment, playerPref);
			InitializePlayerAct00(player);
			InitializePlayerAct01(playerAct01, playerPref, keyInput);
			sound.PlayerSound_ini("AudioPosition");
			player.player_ini = true;
			player.downforce_store = player.DownForce;
			player.obstacleRaycastStart = player.gameObject.transform.FindDeep("DEMO_Pelvis", false);
			player.FPS_CAM_Target = GameObject.Find("FPS_cam_target");

			Player_FootSoundControler playerFootSoundControler = player.GetComponent<Player_FootSoundControler>();
			playerFootSoundControler.Pref = playerPref;
			playerFootSoundControler.Sound = PlayerSound_Manager.Instance;
			playerFootSoundControler.anim = player.GetComponent<Animator>();

			Player_FootSound[] playerFootSounds = player.GetComponentsInChildren<Player_FootSound>();
			foreach (Player_FootSound playerFootSound in playerFootSounds)
			{
				playerFootSound.Pref = playerPref;
				playerFootSound.PlayerObject = player.gameObject;
				playerFootSound.FootSoundCtrl = playerFootSoundControler;
			}

			Transform playerHeadTransform = player.gameObject.transform.Find("Root/DEMO_Pelvis/DEMO_Spine/DEMO_Spine1/Spine_1_5/DEMO_Spine2/DEMO_Spine3/DEMO_Neck/DEMO_Neck2/DEMO_Head");

			GameObject nicknameCanvasObj = new GameObject("NicknameCanvas");
			Canvas nicknameCanvas = nicknameCanvasObj.AddComponent<Canvas>();
			nicknameCanvasRect = (RectTransform)nicknameCanvasObj.transform;
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
			RectTransform nicknameTextRect = (RectTransform)nicknameTextObj.transform;
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
			playerEquipment.Perf = playerPref;
			playerEquipment.Sound = PlayerSound_Manager.Instance;
			playerEquipment.anim = playerEquipment.GetComponent<Animator>();
			playerEquipment._LimbIK = playerEquipment.GetComponent<LimbIK>();
			playerEquipment.Act = playerEquipment.GetComponent<PlayerAct_00>();
			playerPref.isMain = true;
			playerEquipment.setWepon(0);
			playerEquipment.setWepon(1);
			playerEquipment.weponIcon = playerPref.gameObject.GetComponent<UI_weponIcon>();
			playerEquipment.initialize = true;
		}

		// Server + Client
		public static void InitializePlayerAct00(PlayerControl player)
		{
			PlayerAct_00 playerAct00 = player.GetComponent<PlayerAct_00>();
			PlayerPref playerPref = player.Perf;

			PlayerAct_00 ogPlayerAct00 = GameNet.GetLocalPlayer().GetComponent<PlayerAct_00>();
			playerAct00.config = ogPlayerAct00.config;
			playerAct00.fvp_config = ogPlayerAct00.fvp_config;
			playerAct00.config_col = ogPlayerAct00.config_col;
			playerAct00.wepon_prefab = ogPlayerAct00.wepon_prefab;

			playerAct00.KeyInput = player.KeyInput;
			playerAct00.Sound = PlayerSound_Manager.Instance;
			playerAct00.Perf = playerPref;
			playerAct00.UI = UI_Interactive.Instance;
			playerAct00.PE = playerAct00.GetComponent<Player_Equipment>();
			playerAct00.p_Granade = playerAct00.GetComponent<Player_Granade>();
			playerAct00.weponIcon = playerPref.GetComponent<UI_weponIcon>();
			playerAct00._AimIK = playerAct00.GetComponent<AimIK>();
			playerAct00.m_rigidBody = playerAct00.GetComponent<Rigidbody>();
			playerAct00.player_ini = false;
			playerAct00.weponID = playerPref.Main_weponID;
			playerAct00.SubID = playerPref.Sub_weponID;
			playerAct00.wepon_prefab = playerPref.E_mainWepon;
			playerAct00.Sub_prefab = playerPref.E_SubWepon;
			playerAct00.a_hash_AngleV = Animator.StringToHash("angleV");
			playerAct00.a_hash_AngleH = Animator.StringToHash("angleH");
			playerAct00.a_shotFloat = Animator.StringToHash("ShotFloat");
			playerAct00.a_SHOT = Animator.StringToHash("Shot");
			playerAct00.a_RELOAD = Animator.StringToHash("Reload");
			playerAct00.a_NearWall = Animator.StringToHash("NearWall");
			playerAct00.a_Aim = Animator.StringToHash("Aim");
			playerAct00.a_TurnFloat = Animator.StringToHash("TurnFloat");
			playerAct00.a_weponid = Animator.StringToHash("WeponID");
			playerAct00.a_subid = Animator.StringToHash("SubID");

			AimIK[] AimIKs = new AimIK[playerPref.SyncAnimator.Length];
			playerAct00.AimIKs = AimIKs;
			for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
				AimIKs[i] = playerPref.SyncAnimator[i].gameObject.GetComponent<AimIK>();

			if (playerPref.isMain)
			{
				if (playerAct00.wepon_prefab != null)
				{
					playerAct00.LayerName = "Gun_act_" + playerAct00.weponID;
					playerAct00.ReloadComp_wep();
				}
				else
				{
					playerAct00.Kaede_mag = null;
					playerAct00.g_anim = null;
					Debug.Log(playerAct00.wepon_prefab + "::: null");
				}
			}
			else if (playerPref.isSub)
			{
				if (playerAct00.Sub_prefab != null)
				{
					playerAct00.LayerName = "Sub_act_" + playerAct00.SubID;
					playerAct00.ReloadComp_wep();
				}
				else
				{
					playerAct00.Kaede_mag = null;
					playerAct00.g_anim = null;
				}
			}
			else
			{
				playerAct00.Kaede_mag = null;
				playerAct00.g_anim = null;
			}

			playerAct00.PlayerControl = playerAct00.GetComponent<PlayerControl>();
			playerAct00.anim = playerAct00.GetComponent<Animator>();
			playerAct00.cameraTransform = playerPref.MainCamera.transform;
			playerAct00.player_ini = true;
			playerAct00.RELOAD_STEP = PlayerAct_00.ReloadState.None;
		}

		// Server + Client
		private static void InitializePlayerAct01(PlayerAct_01 playerAct01, PlayerPref playerPref, Player_Config_manager keyInput)
		{
			playerAct01.Perf = playerPref;
			playerAct01.KeyInput = keyInput;
			playerAct01.Sound = PlayerSound_Manager.Instance;
			playerAct01.playerControl = playerAct01.GetComponent<PlayerControl>();
			playerAct01.act = playerAct01.GetComponent<PlayerAct_00>();
			Animator anim = playerAct01.GetComponent<Animator>();
			playerAct01.anim = anim;
			int cqb_0 = anim.GetLayerIndex("CQB_0");
			playerAct01.cqb_0 = cqb_0;
			playerAct01.cqb_1 = anim.GetLayerIndex("Kaede_motion");
			anim.SetLayerWeight(cqb_0, 0f);
			for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
				playerPref.SyncAnimator[i].SetLayerWeight(cqb_0, 0f);
			playerAct01.player_ini = true;
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

		private void BeginJumpRoll(string stateName)
		{
			PlayerPref pref = PlayerCtrl.Perf;
			PlayerCtrl.Sound.SetSound_FootStep(null, 14, 1f);
			PlayerCtrl.anim.CrossFadeInFixedTime(stateName, 0.05f);
			for (int i = 0; i < pref.SyncAnimator.Length; i++)
				pref.SyncAnimator[i].CrossFadeInFixedTime(stateName, 0.05f);
			PlayerCtrl.PlayerAct.AimControl_cancel();
		}

		private void BeginJumpType4()
		{
			Vector3 storeVector = new Vector3(0f, PlayerCtrl.jumpHeight * 1.5f, 0f);
			PlayerCtrl.m_storeVector = storeVector;
			PlayerCtrl.m_Rigidbody.velocity = storeVector;
		}

		private void BeginJumpType5()
		{
			PlayerPref pref = PlayerCtrl.Perf;
			PlayerCtrl.m_storeVector = new Vector3(0f, PlayerCtrl.jumpHeight, 0f);
			PlayerCtrl.anim.CrossFadeInFixedTime("Kaede_motion.Jump", 0.05f);
			for (int n = 0; n < pref.SyncAnimator.Length; n++)
				pref.SyncAnimator[n].CrossFadeInFixedTime("Kaede_motion.Jump", 0.05f);
		}

		private void EndCrouchAnim()
		{
			PlayerPref pref = PlayerCtrl.Perf;
			PlayerCtrl.crouch = false;
			PlayerCtrl.anim.SetFloat("CrouchFloat", 0f);
			for (int m = 0; m < pref.SyncAnimator.Length; m++)
				pref.SyncAnimator[m].SetFloat("CrouchFloat", 0f);
		}

		// Server + Client - Runs when jump data is received
		public void OnJumpData(int jumpType)
		{
			PlayerControl player = PlayerCtrl;

			if (jumpType == 0)
				return;

			if (jumpType == 1)
			{
				BeginJumpRoll("Kaede_motion.roll_forward");
			}
			else if (jumpType == 2)
			{
				BeginJumpRoll("Kaede_motion.roll_back");
			}
			else if (jumpType >= 3 && jumpType <= 5)
			{
				EndCrouchAnim();
				if (jumpType == 4)
				{
					BeginJumpType4();
				}
				else if (jumpType == 5)
				{
					BeginJumpType5();
				}
			}
		}

		// Server - Sends the jump data of a player to the other clients
		private void BroadcastJumpData(int jumpType)
		{
			byte[] jumpData = new byte[sizeof(int) * 3];
			Utils.WriteInt(jumpData, 0, (int)S2CPacketID.PlayerJump);
			Utils.WriteInt(jumpData, 4, (int)ID);
			Utils.WriteInt(jumpData, 8, jumpType);
			BroadcastPacketExcludingSelf(jumpData);
		}

		// Server + Client - Sends the jump data to the server (only the local player should run this)
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
				Utils.WriteInt(jumpData, 0, (int)C2SPacketID.PlayerJump);
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

			AnimatorStateInfo stateInfo = player.anim.GetCurrentAnimatorStateInfo(0);
			player.stateInfo = stateInfo;

			if (player.timeToNextJump > 0f)
				player.timeToNextJump -= Time.deltaTime;

			if (Input.GetKeyDown(player.KeyInput.Jump) && player.Helth.Sick < player.Sick_thredhold)
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
						float float_h = player.float_h;
						float float_v = player.float_v;
						if (float_h >= 0.5f)
						{
							BeginJumpRoll("Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_h <= -0.5f)
						{
							BeginJumpRoll("Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_v >= 0.5f)
						{
							BeginJumpRoll("Kaede_motion.roll_forward");
							SendJumpData(1);
							return;
						}
						if (float_v <= -0.5f)
						{
							BeginJumpRoll("Kaede_motion.roll_back");
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

					EndCrouchAnim();
					if (player.Checkobstacle())
					{
						player.PlayerState = PlayerControl.State.Obstacle_s;
					}
					else if (!player.aim)
					{
						if (player.IsMoveing() && player.IsGrounded())
						{
							BeginJumpType4();
							SendJumpData(4);
						}
						else
						{
							BeginJumpType5();
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
			Utils.WriteInt(jumpData, 0, (int)S2CPacketID.PlayerCtrlKey);
			Utils.WriteInt(jumpData, 4, (int)ID);
			Utils.WriteInt(jumpData, 8, (int)key);
			Utils.WriteInt(jumpData, 12, isDown ? 1 : 0);
			BroadcastPacketExcludingSelf(jumpData);
		}

		// Server + Client - Sends the key data to the server (only the local player should run this)
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
				Utils.WriteInt(jumpData, 0, (int)C2SPacketID.PlayerCtrlKey);
				Utils.WriteInt(jumpData, 4, (int)key);
				Utils.WriteInt(jumpData, 8, isDown ? 1 : 0);
				Client.SendPacket(jumpData);
			}
		}

		// Server + Client - Replacement for PlayerControl.LateUpdate
		public void LateUpdate()
		{
			PlayerControl player = PlayerCtrl;

			if (!player.player_ini || Time.timeScale <= 0f)
				return;

			bool isLocalPlayer = player == GameNet.GetLocalPlayer();

			PlayerPref pref = player.Perf;
			Player_Helth health = player.Helth;
			Player_Equipment equipment = player.Player_Equipment;
			Player_Config_manager keyInput = player.KeyInput;
			PlayerAct_00 playerAct00 = player.PlayerAct;
			Animator anim = player.anim;

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
						player.aim = aim;
					}
				}
				else
				{
					player.aim = netCtrl_isAiming;
				}
				
				if (isLocalPlayer)
				{
					if (health.Sick >= player.Sick_thredhold && player.IsCrouch())
					{
						player.input_h = false;
						player.input_v = false;
						player.float_h = 0f;
						player.float_v = 0f;
					}
					else
					{
						float slopeSpeed = player.SlopeSpeed();
						player.input_h = Input.GetButton("Horizontal");
						player.input_v = Input.GetButton("Vertical");
						player.float_h = Input.GetAxis("Horizontal") * slopeSpeed;
						player.float_v = Input.GetAxis("Vertical") * slopeSpeed;
					}
				}

				if (player.IsAiming())
				{
					if (Input.GetKeyDown(keyInput.FirestPersonView))
					{
						if (player.FPV)
							player.SetTPS();
						else
							player.SetFPV();
					}
				}
				else
				{
					player.SetTPS();
				}
				if (player.FPV && anim.GetBool(Animator.StringToHash("NearWall")) && player.IsSniper() && pref.isMain)
				{
					player.SetTPS();
				}

				if (playerAct00.RELOAD_STEP == PlayerAct_00.ReloadState.None &&
					health.Sick < player.Sick_thredhold &&
					player.PlayerState == PlayerControl.State.Playable)
				{
					float mouseWheel = Input.GetAxis("Mouse ScrollWheel");
					player.MouseWheel = mouseWheel;
					if (!player.IsAiming())
					{
						if (mouseWheel < 0f || Input.GetKeyDown(keyInput.ChangeSubWepon))
						{
							if (pref.isMain)
							{
								equipment.switchWepon = true;
								player.ChangeEquipment(0);
							}
							else
							{
								player.ChangeEquipment(1);
							}
						}
						if (mouseWheel > 0f || Input.GetKeyDown(keyInput.ChangeMainWepon))
						{
							if (pref.isSub)
							{
								equipment.switchWepon = true;
								player.ChangeEquipment(1);
							}
							else
							{
								player.ChangeEquipment(0);
							}
						}
					}
				}

				player.crouchHandler = isLocalPlayer ? Input.GetKeyDown(keyInput.Crouch) : false;
				anim.SetBool("Aim", player.IsAiming());
				anim.SetBool("Crouch", player.IsCrouch());
				anim.SetFloat("H", player.float_h);
				anim.SetFloat("V", player.float_v);
				for (int i = 0; i < pref.SyncAnimator.Length; i++)
				{
					pref.SyncAnimator[i].SetBool("Aim", player.IsAiming());
					pref.SyncAnimator[i].SetBool("Crouch", player.IsCrouch());
					pref.SyncAnimator[i].SetFloat("H", player.float_h);
					pref.SyncAnimator[i].SetFloat("V", player.float_v);
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

				if (player.crouchHandler)
				{
					if (player.IsGrounded())
						player.crouch = !player.IsCrouch();
					player.crouchHandler = false;
				}
				else if (!isLocalPlayer)
				{
					player.crouch = netCtrl_isCrouching;
				}

				player.MovementManagement(player.float_h, player.float_v, player.run, player.sprint);

				JumpManagement();

				anim.SetFloat("input_sum", Mathf.Clamp01(Mathf.Abs(player.float_h) + Mathf.Abs(player.float_v)));
				for (int l = 0; l < pref.SyncAnimator.Length; l++)
					pref.SyncAnimator[l].SetFloat("input_sum", Mathf.Clamp01(Mathf.Abs(player.float_h) + Mathf.Abs(player.float_v)));
			}
			else
			{
				player.aim = false;
			}
			player.CheckGroundStatus();
		}

		// Server + Client - Runs when knife use data is received
		public void OnKnifeUseData(int step)
		{
			PlayerAct_01 act01 = PlayerCtrl.PlayerAct01;
			if (step == 0)
			{
				act01.OnClick = 1;
				act01.STEP = 1;
			}
			else if (step == 1)
			{
				act01.OnClick = 2;
				if (act01.STEP == 0 || act01.STEP > 3)
				{
					// If for some reason the previous steps aren't executing we must force the step
					ForceBeginKnifeStep(4, ".CQB_01");
				}
			}
			else if (step == 2)
			{
				act01.OnClick = 3;
				if (act01.STEP == 0 || act01.STEP > 4)
				{
					// If for some reason the previous steps aren't executing we must force the step
					act01.ShowCollsion(0);
					ForceBeginKnifeStep(5, ".CQB_02");
				}
			}
		}

		// Server + Client - This function is only meant to be used for forcing steps 4 and 5
		// This function is used when the player initiates a knife combo while the initial knife
		// animation has ended on the receiver's side.
		private void ForceBeginKnifeStep(int step, string stateName)
		{
			PlayerAct_01 act01 = PlayerCtrl.PlayerAct01;
			PlayerAct_00 act00 = PlayerCtrl.PlayerAct;

			act01.IsMove = false;
			act00.enabled = false;
			act01.OnfKnife();
			PlayerCtrl.PlayerState = PlayerControl.State.OnKnifeAct;

			BeginKnifeAnim(stateName, 0f);
			act01.STEP = step;
		}

		// Server - Sends the knife use data of a player to the other clients
		private void BroadcastKnifeUseData(int step)
		{
			byte[] data = new byte[sizeof(int) * 3];
			Utils.WriteInt(data, 0, (int)S2CPacketID.PlayerKnifeUse);
			Utils.WriteInt(data, 4, (int)ID);
			Utils.WriteInt(data, 8, step);
			BroadcastPacketExcludingSelf(data);
		}

		// Server + Client - Sends the knife use data to the server (only the local player should run this)
		private void SendKnifeUseData(int step)
		{
			if (GameNet.IsServer)
			{
				// If we are a server, there is not point in sending the data to ourselves, just send it to the other clients
				BroadcastKnifeUseData(step);
			}
			else
			{
				// But if we are a client, we must send it to the server so that it will broadcast it to other clients
				byte[] data = new byte[sizeof(int) * 2];
				Utils.WriteInt(data, 0, (int)C2SPacketID.PlayerKnifeUse);
				Utils.WriteInt(data, 4, step);
				Client.SendPacket(data);
			}
		}

		private void BeginKnifeAnim(string animPrefix, float fixedTransitionDuration)
		{
			PlayerAct_01 act01 = PlayerCtrl.PlayerAct01;
			Animator anim = PlayerCtrl.anim;
			PlayerPref pref = PlayerCtrl.Perf;

			string layerName0 = act01.LayerName[0] + animPrefix;
			string layerName1 = act01.LayerName[1] + animPrefix;
			anim.CrossFadeInFixedTime(layerName0, fixedTransitionDuration);
			anim.CrossFadeInFixedTime(layerName1, fixedTransitionDuration);
			for (int m = 0; m < pref.SyncAnimator.Length; m++)
			{
				pref.SyncAnimator[m].CrossFadeInFixedTime(layerName0, fixedTransitionDuration);
				pref.SyncAnimator[m].CrossFadeInFixedTime(layerName1, fixedTransitionDuration);
			}
		}

		// Server + Client - Replacement for PlayerAct_01.Update
		public void Act01Update(PlayerAct_01 act01)
		{
			// TODO: BETTER KNIFE

			if (!act01.player_ini)
				return;

			PlayerControl player = act01.playerControl;
			PlayerPref pref = act01.Perf;
			Animator anim = act01.anim;
			PlayerAct_00 act00 = act01.act;

			if (player.PlayerState != PlayerControl.State.Playable && player.PlayerState != PlayerControl.State.OnKnifeAct)
				return;

			act01.stateInfo = anim.GetCurrentAnimatorStateInfo(act01.cqb_0);

			bool isLocalPlayer = player == GameNet.GetLocalPlayer();
			if (isLocalPlayer)
			{
				if (Input.GetKeyDown(act01.KeyInput.WeponUse) && !player.IsAiming() && !player.IsCrouch())
				{
					if (act00.Granade != PlayerAct_00.GranadeState.None || DoorActManager.Instance.DoorState != DoorActManager.State.Door_Idle)
						return;

					if (act00.RELOAD_STEP != PlayerAct_00.ReloadState.None)
					{
						act00.CancelReload();
						act00.RELOAD_STEP = PlayerAct_00.ReloadState.None;
					}
					if (act01.STEP == 0)
					{
						act01.OnClick = 1;
						act01.STEP = 1;
						SendKnifeUseData(0);
					}
					else if (act01.STEP == 3)
					{
						act01.OnClick = 2;
						SendKnifeUseData(1);
					}
					else if (act01.STEP == 4)
					{
						act01.OnClick = 3;
						SendKnifeUseData(2);
					}
				}
			}

			if (!player.IsGrounded())
				act01.CancelKnife();

			if (act01.STEP > 2 && act01.STEP < 10)
			{
				Vector3 vector = pref.MainCamera.transform.TransformDirection(Vector3.forward);
				vector.y = 0f;
				Quaternion quaternion = Quaternion.LookRotation(vector, Vector3.up);
				Quaternion quaternion2 = Quaternion.Slerp(act01.GetComponent<Rigidbody>().rotation, quaternion, 0.8f);
				act01.GetComponent<Rigidbody>().MoveRotation(quaternion2);
				if (PlayerPrefs.GetFloat("Key_ActDirection", 0f) == 1f)
					act01.transform.rotation = quaternion2;
			}

			if (act01.STEP == 0)
				return;

			if (act01.STEP == 1)
			{
				act01.IsMove = false;
				anim.CrossFadeInFixedTime(act01.LayerName[0] + ".OnKnife", 0.1f);
				for (int j = 0; j < pref.SyncAnimator.Length; j++)
					pref.SyncAnimator[j].CrossFadeInFixedTime(act01.LayerName[0] + ".OnKnife", 0.1f);
				act00.enabled = false;
				act01.STEP = 2;
			}
			else if (act01.STEP == 2)
			{
				anim.SetLayerWeight(act01.cqb_0, Mathf.Lerp(anim.GetLayerWeight(act01.cqb_0), 1f, 0.2f));
				for (int k = 0; k < pref.SyncAnimator.Length; k++)
					pref.SyncAnimator[k].SetLayerWeight(act01.cqb_0, Mathf.Lerp(pref.SyncAnimator[k].GetLayerWeight(act01.cqb_0), 1f, 0.2f));
				if (act01.stateInfo.normalizedTime > 0.95f)
				{
					act01.OnfKnife();
					anim.CrossFadeInFixedTime(act01.LayerName[1] + ".CQB_00", 0.1f);
					anim.CrossFadeInFixedTime(act01.LayerName[0] + ".CQB_00", 0.1f);
					anim.SetLayerWeight(act01.cqb_0, 1f);
					for (int l = 0; l < pref.SyncAnimator.Length; l++)
					{
						pref.SyncAnimator[l].CrossFadeInFixedTime(act01.LayerName[1] + ".CQB_00", 0.1f);
						pref.SyncAnimator[l].CrossFadeInFixedTime(act01.LayerName[0] + ".CQB_00", 0.1f);
						pref.SyncAnimator[l].SetLayerWeight(act01.cqb_0, 1f);
					}
					player.PlayerState = PlayerControl.State.OnKnifeAct;
					act01.STEP = 3;
				}
			}
			else if (act01.STEP == 3)
			{
				act01.test = act01.stateInfo.normalizedTime;
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".CQB_00"))
				{
					// Added "(act01.OnClick < 2 || isLocalPlayer)" check because the packet
					// might arrive after the animation has ended and we still want to play it
					if ((act01.OnClick < 2 || isLocalPlayer) && act01.stateInfo.normalizedTime > 0.9f)
					{
						act01.ShowCollsion(0);
						BeginKnifeAnim(".CQB_03", 0.3f);
						player.PlayerState = PlayerControl.State.Playable;
						act01.STEP = 10;
					}
					else if (act01.stateInfo.normalizedTime > 0.6f && act01.OnClick >= 2)
					{
						// Starts step 4
						BeginKnifeAnim(".CQB_01", 0f);
						act01.STEP = 4;
					}
				}
			}
			else if (act01.STEP == 4)
			{
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".CQB_01"))
				{
					// Added "(act01.OnClick < 3 || isLocalPlayer)" check because the packet
					// might arrive after the animation has ended and we still want to play it
					if ((act01.OnClick < 3 || isLocalPlayer) && act01.stateInfo.normalizedTime > 0.9f)
					{
						act01.ShowCollsion(0);
						BeginKnifeAnim(".CQB_04", 0.3f);
						player.PlayerState = PlayerControl.State.Playable;
						act01.STEP = 10;
					}
					else if (act01.stateInfo.normalizedTime > 0.65f && act01.OnClick >= 3)
					{
						// Starts step 5
						act01.ShowCollsion(0);
						BeginKnifeAnim(".CQB_02", 0f);
						act01.STEP = 5;
					}
				}
			}
			else if (act01.STEP == 5)
			{
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".OffKnife"))
				{
					player.PlayerState = PlayerControl.State.Playable;
					act01.STEP = 10;
				}
			}
			else if (act01.STEP == 10)
			{
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".OffKnife") && act01.stateInfo.normalizedTime > 0.9f)
				{
					act01.Knife_Hand.SetActive(false);
					act01.Knife_Holder.SetActive(true);
					anim.SetLayerWeight(act01.cqb_0, Mathf.Lerp(anim.GetLayerWeight(act01.cqb_0), 0f, 0.3f));
					for (int num3 = 0; num3 < pref.SyncAnimator.Length; num3++)
						pref.SyncAnimator[num3].SetLayerWeight(act01.cqb_0, Mathf.Lerp(pref.SyncAnimator[num3].GetLayerWeight(act01.cqb_0), 0f, 0.3f));
					if (anim.GetLayerWeight(act01.cqb_0) < 0.1f)
					{
						act01.STEP = 0;
						act00.enabled = true;
						anim.CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
						anim.SetLayerWeight(act01.cqb_0, 0f);
						for (int num4 = 0; num4 < pref.SyncAnimator.Length; num4++)
						{
							pref.SyncAnimator[num4].CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
							pref.SyncAnimator[num4].SetLayerWeight(act01.cqb_0, 0f);
						}
					}
				}
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".Idle_00"))
				{
					anim.SetLayerWeight(act01.cqb_0, Mathf.Lerp(anim.GetLayerWeight(act01.cqb_0), 0f, 0.3f));
					for (int num6 = 0; num6 < pref.SyncAnimator.Length; num6++)
						pref.SyncAnimator[num6].SetLayerWeight(act01.cqb_0, Mathf.Lerp(pref.SyncAnimator[num6].GetLayerWeight(act01.cqb_0), 0f, 0.3f));
					if (anim.GetLayerWeight(act01.cqb_0) < 0.1f)
					{
						act01.STEP = 0;
						act00.enabled = true;
						anim.CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
						anim.SetLayerWeight(act01.cqb_0, 0f);
						for (int num7 = 0; num7 < pref.SyncAnimator.Length; num7++)
						{
							pref.SyncAnimator[num7].CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
							pref.SyncAnimator[num7].SetLayerWeight(act01.cqb_0, 0f);
						}
					}
				}
				if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".None"))
				{
					act01.STEP = 0;
					act00.enabled = true;
					anim.SetLayerWeight(act01.cqb_0, 0f);
					for (int num8 = 0; num8 < pref.SyncAnimator.Length; num8++)
						pref.SyncAnimator[num8].SetLayerWeight(act01.cqb_0, 0f);
				}
			}
		}
	}
}
