using K_PlayerControl;
using K_PlayerControl.UI;
using RootMotion.FinalIK;
using System;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace EliminatorKaedeMP
{
    public partial class EKMPPlayer
    {
        public enum CtrlKey { Aim, Crouch }

        // Seconds between 30Hz state packets.
        private const float StateSendInterval  = 1f / 30f;
        // Seconds between 5Hz health packets.
        private const float HealthSendInterval = 1f / 5f;

        public static bool IsNetPlayerCtx = false;

        public EKMPPlayerInfo Info;
        public NetClient Client = null;
        public PlayerControl PlayerCtrl = null;
        public IPEndPoint UdpEndpoint = null;

        private RectTransform nicknameCanvasRect = null;

        // Last-known discrete booleans for the LOCAL player (used to detect changes).
        private bool lastAiming    = false;
        private bool lastCrouching = false;

        // Timers for periodic sends.
        private float stateSendTimer  = 0f;
        private float healthSendTimer = 0f;
        private float _diagTimer = 0f;

        // Cached remote-player aim state applied from the interpolation buffer.
        private Vector3 remoteAimForward = Vector3.forward;

        private GameObject kaedeHeadObj  = null;
        private GameObject momijiHeadObj = null;

        // ──────────────────────────────────────────────────────────────────────
        // Packet dispatch (server-side, called for the player that sent a packet)
        // ──────────────────────────────────────────────────────────────────────

        public void OnPacketReceived(byte[] bytes)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(bytes);
                using BinaryReader reader = new BinaryReader(stream);

                C2SPacketID packetID = (C2SPacketID)reader.ReadInt32();
                switch (packetID)
                {
                case C2SPacketID.PlayerState:
                {
                    PlayerStateData data = PlayerStateData.Read(reader);
                    BroadcastStateData(data);
                    Plugin.CallOnMainThread(() => OnStateData(data));
                    break;
                }
                case C2SPacketID.PlayerEvent:
                {
                    PlayerEventID eventID = (PlayerEventID)reader.ReadInt32();
                    int data0 = reader.ReadInt32();
                    int data1 = reader.ReadInt32();
                    BroadcastEventData(eventID, data0, data1);
                    Plugin.CallOnMainThread(() => OnEventData(eventID, data0, data1));
                    break;
                }
                case C2SPacketID.PlayerHealth:
                {
                    PlayerHealthData health = PlayerHealthData.Read(reader);
                    BroadcastHealthData(health);
                    Plugin.CallOnMainThread(() => OnHealthData(health));
                    break;
                }
                case C2SPacketID.PlayerChangeChar:
                {
                    int charID = reader.ReadInt32();
                    BroadcastCharChangeData(charID);
                    Plugin.CallOnMainThread(() => SetPlayerCharacter(charID));
                    break;
                }
                case C2SPacketID.PlayerClothInfo:
                {
                    EKMPPlayerClothInfo clothInfo = EKMPPlayerClothInfo.Read(reader);
                    BroadcastClothInfoData(clothInfo);
                    Plugin.CallOnMainThread(() => OnClothInfoData(clothInfo));
                    break;
                }
                }
            }
            catch (Exception ex) { Plugin.Log(ex); }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Join / leave
        // ──────────────────────────────────────────────────────────────────────

        public void OnJoin()
        {
            GameNet.Players.Add(this);
            Plugin.Log(Info.Name + " has joined!");

            if (GameNet.IsServer)
            {
                // Notify all existing players about the newcomer.
                byte[] joinNotice;
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write((int)S2CPacketID.PlayerJoin);
                        Info.Write(writer, true);
                    }
                    joinNotice = stream.ToArray();
                }
                BroadcastPacket(joinNotice);

                // Send the full game state to the player that just joined.
                GameJoinInfoData joinData = new GameJoinInfoData();
                joinData.PlayerID   = Info.ID;
                joinData.SceneID    = (byte)Utils.GetCurrentScene();
                joinData.PlayerInfos = new EKMPPlayerInfo[GameNet.Players.Count - 1];
                int i = 0;
                foreach (EKMPPlayer p in GameNet.Players)
                {
                    if (p == this) continue;
                    joinData.PlayerInfos[i++] = p.Info;
                }
                byte[] joinReply;
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write((int)S2CPacketID.GameJoinInfo);
                        joinData.Write(writer);
                    }
                    joinReply = stream.ToArray();
                }
                Client.SendPacket(joinReply);
            }
        }

        public void OnDisconnect()
        {
            GameNet.Players.Remove(this);
            DestroyObjects();
            Plugin.Log(Info.Name + " has left!");

            if (GameNet.IsServer)
            {
                byte[] leaveMsg = new byte[8];
                Utils.WriteInt(leaveMsg, 0, (int)S2CPacketID.PlayerLeave);
                Utils.WriteInt(leaveMsg, 4, (int)Info.ID);
                GameNet.Server.BroadcastPacket(leaveMsg);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Initialization
        // ──────────────────────────────────────────────────────────────────────

        public void DestroyObjects()
        {
            if (PlayerCtrl != null)
            {
                UnityEngine.Object.Destroy(PlayerCtrl.Perf.MainCamera.gameObject);
                UnityEngine.Object.Destroy(PlayerCtrl.Perf.gameObject);
                UnityEngine.Object.Destroy(PlayerCtrl.gameObject);
                PlayerCtrl = null;
            }
        }

        public void Initialize(NetClient client, EKMPPlayerInfo playerInfo)
        {
            Info   = playerInfo;
            Client = client;
            TryInstantiatePlayer();
        }

        public void TryInstantiatePlayer()
        {
            PlayerControl local = GameNet.GetLocalPlayer();
            if (local == null) return;

            if (Info.ID == GameNet.Player.Info.ID)
            {
                PlayerCtrl = local;
                return;
            }

            IsNetPlayerCtx = true;
            PlayerCtrl = UnityEngine.Object.Instantiate(local.gameObject).GetComponent<PlayerControl>();
            try { InitializeNetPlayer(); }
            catch (Exception ex) { Plugin.Log(ex); }
            IsNetPlayerCtx = false;
        }

        public void InitializeNetPlayer()
        {
            PlayerControl player = PlayerCtrl;

            Player_Equipment playerEquipment = player.GetComponent<Player_Equipment>();
            PlayerAct_00     playerAct00     = player.GetComponent<PlayerAct_00>();
            PlayerAct_01     playerAct01     = player.GetComponent<PlayerAct_01>();
            Rigidbody        rigidbody       = player.GetComponent<Rigidbody>();
            CapsuleCollider  capsule         = player.GetComponent<CapsuleCollider>();

            playerEquipment.initialize = false;

            PlayerPref playerPref = CreateNetPlayerPref();
            Player_DecalManager   decal    = Player_DecalManager.Instance;
            Player_Config_manager keyInput = Player_Config_manager.Instance;
            PlayerSound_Manager   sound    = PlayerSound_Manager.Instance;
            Player_EffectManager  effect   = Player_EffectManager.Instance;

            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidbody.isKinematic = false;
            capsule.enabled       = true;

            PhysicMaterial zeroFriction = new PhysicMaterial();
            zeroFriction.dynamicFriction = 0f;
            zeroFriction.staticFriction  = 0f;
            zeroFriction.frictionCombine = PhysicMaterialCombine.Minimum;
            zeroFriction.bounciness      = 0f;
            zeroFriction.bounceCombine   = PhysicMaterialCombine.Minimum;

            PhysicMaterial highFriction = new PhysicMaterial();
            highFriction.dynamicFriction = 0f;
            highFriction.staticFriction  = 1f;
            highFriction.bounciness      = 0f;

            GrounderFBBIK[] groundIKList = new GrounderFBBIK[playerPref.SyncAnimator.Length];
            for (int i = 0; i < playerPref.SyncAnimator.Length; i++)
                groundIKList[i] = playerPref.SyncAnimator[i].gameObject.GetComponent<GrounderFBBIK>();

            player.Decal           = decal;
            player.KeyInput        = keyInput;
            player.Sound           = sound;
            player.Effect          = effect;
            player.anim            = player.GetComponent<Animator>();
            player.Helth           = player.GetComponent<Player_Helth>();
            player.Player_Equipment = playerEquipment;
            player.PlayerAct       = playerAct00;
            player.PlayerAct01     = playerAct01;
            player.cameraTransform = playerPref.MainCamera.transform;
            player.Buffer_FOV      = playerPref.MainCamera.GetComponent<Camera>().fieldOfView;
            player.m_Rigidbody     = rigidbody;
            player.Capsule         = capsule;
            player.zeroFrictionMaterial = zeroFriction;
            player.highFrictionMaterial = highFriction;
            player.PlayerState     = PlayerControl.State.Playable;
            player.FlyState        = PlayerControl.Fly.none;
            player.Gimic           = PlayerControl.StageGimic.NULL;
            player.FPV_target      = null;
            player.GroundIK_list   = groundIKList;

            SetPlayerCharacter(Info.CharacterID);
            InitializePlayerEquipment(playerEquipment, playerPref);
            InitializePlayerAct00(player);
            InitializePlayerAct01(playerAct01, playerPref, keyInput);
            sound.PlayerSound_ini("AudioPosition");
            player.downforce_store        = player.DownForce;
            player.obstacleRaycastStart   = player.transform.FindDeep("DEMO_Pelvis", false);
            player.FPS_CAM_Target         = GameObject.Find("FPS_cam_target");

            Player_FootSoundControler footSoundCtrl = player.GetComponent<Player_FootSoundControler>();
            footSoundCtrl.Pref          = playerPref;
            footSoundCtrl.Sound         = PlayerSound_Manager.Instance;
            footSoundCtrl.anim          = player.GetComponent<Animator>();

            foreach (Player_FootSound fs in player.GetComponentsInChildren<Player_FootSound>())
            {
                fs.Pref           = playerPref;
                fs.PlayerObject   = player.gameObject;
                fs.FootSoundCtrl  = footSoundCtrl;
            }

            kaedeHeadObj  = player.transform.Find("Root/DEMO_Pelvis/DEMO_Spine/DEMO_Spine1/Spine_1_5/DEMO_Spine2/DEMO_Spine3/DEMO_Neck/DEMO_Neck2/DEMO_Head").gameObject;
            momijiHeadObj = player.transform.Find("momiji_rev_201805/Root/DEMO_Pelvis/DEMO_Spine/DEMO_Spine1/Spine_1_5/DEMO_Spine2/DEMO_Spine3/DEMO_Neck/DEMO_Neck2/DEMO_Head").gameObject;

            // Nickname canvas
            GameObject canvasObj = new GameObject("NicknameCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            nicknameCanvasRect = (RectTransform)canvasObj.transform;
            canvas.renderMode = RenderMode.WorldSpace;
            RepositionNametag();
            nicknameCanvasRect.localPosition = new Vector3(0f, 0.28f, 0f);
            nicknameCanvasRect.localRotation = Quaternion.identity;
            nicknameCanvasRect.localScale    = new Vector3(0.0025f, 0.0025f, 1f);
            nicknameCanvasRect.pivot         = new Vector2(0.5f, 0.5f);
            nicknameCanvasRect.sizeDelta     = new Vector2(350f, 40f);

            GameObject textObj = new GameObject("NicknameText");
            Text nameText = textObj.AddComponent<Text>();
            textObj.AddComponent<Outline>();
            RectTransform textRect = (RectTransform)textObj.transform;
            nameText.text      = Info.Name;
            nameText.font      = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize  = 30;
            nameText.alignment = TextAnchor.MiddleCenter;
            textRect.SetParent(nicknameCanvasRect);
            textRect.localPosition = Vector3.zero;
            textRect.localRotation = Quaternion.identity;
            textRect.localScale    = new Vector3(-1f, 1f, 1f);
            textRect.pivot         = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta     = new Vector2(350f, 40f);

            player.player_ini = true;
        }

        private PlayerPref CreateNetPlayerPref()
        {
            GameObject prefObj = new GameObject("PlayerPref_" + Info.ID);
            EKMPPlayerPref pref = prefObj.AddComponent<EKMPPlayerPref>();
            PlayerCtrl.Perf = pref;

            GameObject cam = new GameObject("NetCamera_" + Info.ID);
            cam.AddComponent<Camera>().enabled = false;

            ClothSystem_Initialize(prefObj.AddComponent<UI_ClothSystem>());
            CreatePlayerData();

            pref.MPPlayer           = this;
            pref.PlayerIncetance    = PlayerCtrl.gameObject;
            pref.PerfIncetance      = prefObj;
            pref.SyncAnimator       = PlayerPref.Instance.SyncAnimator;
            pref.GameCamera         = PlayerPref.Instance.GameCamera;
            pref.MainCamera         = cam;
            pref.DummyCamera        = PlayerPref.Instance.DummyCamera;
            pref.weponList          = PlayerPref.Instance.weponList;
            pref.sub_weponList      = PlayerPref.Instance.sub_weponList;
            pref.wepon_static_List  = PlayerPref.Instance.wepon_static_List;
            pref.LayerMaskInfo      = PlayerPref.Instance.LayerMaskInfo;

            prefObj.AddComponent<UI_weponIcon>();
            InitializeToiletEventManager(prefObj.AddComponent<ToiletEventManager>());
            return pref;
        }

        private void CreatePlayerData()
        {
            PlayerPref pref   = PlayerCtrl.Perf;
            PlayerPref ogPref = PlayerPref.instance;
            Transform  tr     = PlayerCtrl.transform;

            GameObject kObj = UnityEngine.Object.Instantiate(ogPref.PlayerData[0].gameObject);
            kObj.transform.parent = pref.transform;
            kObj.name = "PlayableCharacter_Kaede";
            PlayableCharacterData kData = kObj.GetComponent<PlayableCharacterData>();
            kData.PlayerData    = new GameObject[2];
            kData.PlayerData[0] = tr.Find("Root/DEMO_Pelvis/DEMO_Spine/DEMO_Spine1/Spine_1_5/DEMO_Spine2/DEMO_Spine3/DEMO_Neck/DEMO_Neck2/DEMO_Head").gameObject;
            kData.PlayerData[1] = tr.Find("geomGrp").gameObject;
            ClothPurchase_Initialize(kObj.AddComponent<EKMP_UI_cloth_Purchase>(), 0);

            GameObject mObj = UnityEngine.Object.Instantiate(ogPref.PlayerData[1].gameObject);
            mObj.transform.parent = pref.transform;
            mObj.name = "PlayableCharacter_Momiji";
            PlayableCharacterData mData = mObj.GetComponent<PlayableCharacterData>();
            mData.PlayerData    = new GameObject[2];
            mData.PlayerData[0] = tr.Find("momiji_rev_201805/Root").gameObject;
            mData.PlayerData[1] = tr.Find("momiji_rev_201805/geomGrp").gameObject;
            ClothPurchase_Initialize(mObj.AddComponent<EKMP_UI_cloth_Purchase>(), 1);

            pref.PlayerData    = new PlayableCharacterData[2];
            pref.PlayerData[0] = kData;
            pref.PlayerData[1] = mData;
        }

        private static void InitializePlayerEquipment(Player_Equipment eq, PlayerPref pref)
        {
            eq.Perf      = pref;
            eq.Sound     = PlayerSound_Manager.Instance;
            eq.anim      = eq.GetComponent<Animator>();
            eq._LimbIK   = eq.GetComponent<LimbIK>();
            eq.Act       = eq.GetComponent<PlayerAct_00>();
            pref.isMain  = true;
            eq.setWepon(0);
            eq.setWepon(1);
            eq.weponIcon = pref.gameObject.GetComponent<UI_weponIcon>();
            eq.initialize = true;
        }

        public static void InitializePlayerAct00(PlayerControl player)
        {
            PlayerAct_00 act00   = player.GetComponent<PlayerAct_00>();
            PlayerPref   pref    = player.Perf;
            PlayerAct_00 ogAct00 = GameNet.GetLocalPlayer().GetComponent<PlayerAct_00>();

            act00.config      = ogAct00.config;
            act00.fvp_config  = ogAct00.fvp_config;
            act00.config_col  = ogAct00.config_col;
            act00.wepon_prefab = ogAct00.wepon_prefab;
            act00.KeyInput    = player.KeyInput;
            act00.Sound       = PlayerSound_Manager.Instance;
            act00.Perf        = pref;
            act00.UI          = UI_Interactive.Instance;
            act00.PE          = act00.GetComponent<Player_Equipment>();
            act00.p_Granade   = act00.GetComponent<Player_Granade>();
            act00.weponIcon   = pref.GetComponent<UI_weponIcon>();
            act00._AimIK      = act00.GetComponent<AimIK>();
            act00.m_rigidBody = act00.GetComponent<Rigidbody>();
            act00.player_ini  = false;
            act00.weponID     = pref.Main_weponID;
            act00.SubID       = pref.Sub_weponID;
            act00.wepon_prefab = pref.E_mainWepon;
            act00.Sub_prefab  = pref.E_SubWepon;

            act00.a_hash_AngleV = Animator.StringToHash("angleV");
            act00.a_hash_AngleH = Animator.StringToHash("angleH");
            act00.a_shotFloat   = Animator.StringToHash("ShotFloat");
            act00.a_SHOT        = Animator.StringToHash("Shot");
            act00.a_RELOAD      = Animator.StringToHash("Reload");
            act00.a_NearWall    = Animator.StringToHash("NearWall");
            act00.a_Aim         = Animator.StringToHash("Aim");
            act00.a_TurnFloat   = Animator.StringToHash("TurnFloat");
            act00.a_weponid     = Animator.StringToHash("WeponID");
            act00.a_subid       = Animator.StringToHash("SubID");

            AimIK[] aimIKs = new AimIK[pref.SyncAnimator.Length];
            act00.AimIKs = aimIKs;
            for (int i = 0; i < pref.SyncAnimator.Length; i++)
                aimIKs[i] = pref.SyncAnimator[i].gameObject.GetComponent<AimIK>();

            if (pref.isMain)
            {
                if (act00.wepon_prefab != null)
                {
                    act00.LayerName = "Gun_act_" + act00.weponID;
                    act00.ReloadComp_wep();
                }
                else
                {
                    act00.Kaede_mag = null;
                    act00.g_anim    = null;
                }
            }
            else if (pref.isSub)
            {
                if (act00.Sub_prefab != null)
                {
                    act00.LayerName = "Sub_act_" + act00.SubID;
                    act00.ReloadComp_wep();
                }
                else
                {
                    act00.Kaede_mag = null;
                    act00.g_anim    = null;
                }
            }
            else
            {
                act00.Kaede_mag = null;
                act00.g_anim    = null;
            }

            act00.PlayerControl = act00.GetComponent<PlayerControl>();
            act00.anim          = act00.GetComponent<Animator>();
            act00.cameraTransform = pref.MainCamera.transform;
            act00.player_ini    = true;
            act00.RELOAD_STEP   = PlayerAct_00.ReloadState.None;
        }

        private static void InitializePlayerAct01(PlayerAct_01 act01, PlayerPref pref, Player_Config_manager keyInput)
        {
            act01.Perf          = pref;
            act01.KeyInput      = keyInput;
            act01.Sound         = PlayerSound_Manager.Instance;
            act01.playerControl = act01.GetComponent<PlayerControl>();
            act01.act           = act01.GetComponent<PlayerAct_00>();
            Animator anim       = act01.GetComponent<Animator>();
            act01.anim          = anim;
            int cqb0            = anim.GetLayerIndex("CQB_0");
            act01.cqb_0         = cqb0;
            act01.cqb_1         = anim.GetLayerIndex("Kaede_motion");
            anim.SetLayerWeight(cqb0, 0f);
            for (int i = 0; i < pref.SyncAnimator.Length; i++)
                pref.SyncAnimator[i].SetLayerWeight(cqb0, 0f);
            act01.player_ini = true;
        }

        private void InitializeToiletEventManager(ToiletEventManager toiletMgr)
        {
            PlayerControl player = PlayerCtrl;
            PlayerPref    pref   = player.Perf;

            toiletMgr.GM          = GameManager.Instance;
            toiletMgr.Perf        = pref;
            toiletMgr.Player      = player.gameObject;
            toiletMgr.PE          = player.GetComponent<Player_Equipment>();
            toiletMgr.PC          = player.GetComponent<PlayerControl>();
            toiletMgr.PA          = player.GetComponent<PlayerAct_00>();
            toiletMgr.PH          = player.GetComponent<Player_Helth>();
            toiletMgr.EC          = player.GetComponent<EventControl>();
            toiletMgr.CS          = null;
            toiletMgr.ClothSys    = pref.GetComponent<UI_ClothSystem>();
            toiletMgr.UI_behaviro = pref.GetComponent<UI_behaviorPanelManager>();
            toiletMgr.GameCamera  = pref.GameCamera;
            for (int i = 0; i < toiletMgr.mizutamariList.Length; i++)
                toiletMgr.mizutamariList[i] = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Broadcast helpers (server relays a packet to everyone except its sender
        // and the server's own local player)
        // ──────────────────────────────────────────────────────────────────────

        // Relay to everyone except the server's local player and this player's client.
        public void BroadcastPacket(byte[] bytes)
        {
            foreach (EKMPPlayer p in GameNet.Players)
            {
                if (p.Info.ID != GameNet.Player.Info.ID && p != this)
                    p.Client.SendPacket(bytes);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // PLAYER STATE (30Hz movement + game-state snapshot)
        // ══════════════════════════════════════════════════════════════════════

        private static int _sendStateLogCount = 0;
        private void SendStateData()
        {
            if (_sendStateLogCount < 3) { _sendStateLogCount++; Plugin.Log($"[DIAG] SendStateData called (#{_sendStateLogCount})"); }
            PlayerControl player = PlayerCtrl;
            Rigidbody     rb     = player.GetComponent<Rigidbody>();

            PlayerStateData data = new PlayerStateData();
            data.Position      = player.transform.position;
            data.Rotation      = player.transform.rotation;
            data.Velocity      = rb.velocity;
            data.CameraForward = player.cameraTransform.forward;
            data.FloatH        = player.float_h;
            data.FloatV        = player.float_v;
            data.PlayerStateID = (int)player.PlayerState;
            data.FlyStateID    = (int)player.FlyState;
            data.AnimFlags     = PlayerStateData.BuildAnimFlags(
                                     player.m_IsGrounded,
                                     player.IsAiming(),
                                     player.IsCrouch());
            data.Sick          = player.Helth.Sick;

            // Toilet event: read the EventMotions layer directly from the animator.
            // ToiletEventManager lives on the scene's toilet object, not on PlayerPref,
            // so GetComponent from here always returns null for the local player.
            {
                int evLayer = player.anim.GetLayerIndex("EventMotions");
                if (evLayer >= 0 && player.anim.GetLayerWeight(evLayer) > 0.5f)
                {
                    AnimatorStateInfo evInfo = player.anim.GetCurrentAnimatorStateInfo(evLayer);
                    data.ToiletAnimHash = evInfo.fullPathHash;
                    data.ToiletAnimTime = evInfo.normalizedTime % 1f;
                }
            }

            if (GameNet.IsServer)
            {
                BroadcastStateData(data);
            }
            else
            {
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write((int)C2SPacketID.PlayerState);
                        data.Write(bw);
                    }
                    bytes = ms.ToArray();
                }
                GameNet.Client.SendUdpPacket(bytes);
            }
        }

        private void BroadcastStateData(PlayerStateData data)
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write((int)S2CPacketID.PlayerState);
                    bw.Write(Info.ID);
                    data.Write(bw);
                }
                bytes = ms.ToArray();
            }
            UdpBroadcastPacket(bytes);
        }

        // Sends UDP to all connected clients except the server's local player and this player.
        // Falls back to TCP for any client whose UDP endpoint is not yet registered.
        private void UdpBroadcastPacket(byte[] bytes)
        {
            foreach (EKMPPlayer p in GameNet.Players)
            {
                if (p.Info.ID == GameNet.Player.Info.ID || p == this)
                    continue;
                if (p.UdpEndpoint != null)
                    GameNet.Server.SendUdpTo(bytes, p.UdpEndpoint);
                else
                    p.Client?.SendPacket(bytes);
            }
        }

        // Called on main thread when state data arrives for this remote player.
        public void OnStateData(PlayerStateData data)
        {
            AddSnapshot(data);
        }

        // ══════════════════════════════════════════════════════════════════════
        // PLAYER EVENTS (discrete, immediate)
        // ══════════════════════════════════════════════════════════════════════

        private void SendEventData(PlayerEventID eventID, int data0 = 0, int data1 = 0)
        {
            if (GameNet.IsServer)
            {
                BroadcastEventData(eventID, data0, data1);
            }
            else
            {
                byte[] bytes = new byte[sizeof(int) * 4];
                Utils.WriteInt(bytes, 0,  (int)C2SPacketID.PlayerEvent);
                Utils.WriteInt(bytes, 4,  (int)eventID);
                Utils.WriteInt(bytes, 8,  data0);
                Utils.WriteInt(bytes, 12, data1);
                Client.SendPacket(bytes);
            }
        }

        private void BroadcastEventData(PlayerEventID eventID, int data0, int data1)
        {
            byte[] bytes = new byte[sizeof(int) * 5];
            Utils.WriteInt(bytes, 0,  (int)S2CPacketID.PlayerEvent);
            Utils.WriteInt(bytes, 4,  (int)Info.ID);
            Utils.WriteInt(bytes, 8,  (int)eventID);
            Utils.WriteInt(bytes, 12, data0);
            Utils.WriteInt(bytes, 16, data1);
            BroadcastPacket(bytes);
        }

        public void OnEventData(PlayerEventID eventID, int data0, int data1)
        {
            switch (eventID)
            {
            case PlayerEventID.Jump:
                OnJumpData(data0);
                break;
            case PlayerEventID.CtrlKey:
                OnControlData((CtrlKey)data0, data1 != 0);
                break;
            case PlayerEventID.KnifeUse:
                OnKnifeUseData(data0);
                break;
            case PlayerEventID.DamageExploFront:
                OnDamageExploFront(data0 != 0);
                break;
            case PlayerEventID.Vomit:
                OnVomitEvent();
                break;
            case PlayerEventID.GunFire:
                OnGunFireEvent();
                break;
            case PlayerEventID.Reload:
                OnReloadEvent();
                break;
            case PlayerEventID.WeaponSwitch:
                OnWeaponSwitchEvent(data0);
                break;
            case PlayerEventID.Grenade:
                OnGrenadeEvent(data0);
                break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HEALTH (5Hz broadcast)
        // ══════════════════════════════════════════════════════════════════════

        private void SendHealthData()
        {
            Player_Helth h = PlayerCtrl.Helth;
            PlayerHealthData data = new PlayerHealthData();
            data.Health = h.Helth;
            data.Urine  = h.urine;
            data.Feces  = h.feces;
            data.Sick   = h.Sick;

            if (GameNet.IsServer)
            {
                BroadcastHealthData(data);
            }
            else
            {
                byte[] bytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(ms))
                    {
                        bw.Write((int)C2SPacketID.PlayerHealth);
                        data.Write(bw);
                    }
                    bytes = ms.ToArray();
                }
                GameNet.Client.SendUdpPacket(bytes);
            }
        }

        private void BroadcastHealthData(PlayerHealthData data)
        {
            byte[] bytes;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write((int)S2CPacketID.PlayerHealth);
                    bw.Write(Info.ID);
                    data.Write(bw);
                }
                bytes = ms.ToArray();
            }
            UdpBroadcastPacket(bytes);
        }

        public void OnHealthData(PlayerHealthData data)
        {
            Player_Helth h = PlayerCtrl?.Helth;
            if (h == null) return;
            h.Helth = data.Health;
            h.urine = data.Urine;
            h.feces = data.Feces;
            // Sick is a derived value in the original game; we just record it for
            // use in the animator parameters.  We don't need to set h.Sick directly
            // because it is recalculated from drug values in FixedUpdate.
        }

        // ══════════════════════════════════════════════════════════════════════
        // UPDATE (called from the PlayerControl.Update postfix patch)
        // ══════════════════════════════════════════════════════════════════════

        public void Update()
        {
            if (PlayerCtrl == null) return;

            bool isLocal = GameNet.GetLocalPlayer() == PlayerCtrl;

            _diagTimer += Time.deltaTime;
            if (_diagTimer >= 3f)
            {
                _diagTimer = 0f;
                Plugin.Log($"[DIAG] Update id={Info.ID} isLocal={isLocal} PlayerCtrl={(PlayerCtrl != null ? PlayerCtrl.name : "null")} GetLocalPlayer={(GameNet.GetLocalPlayer() != null ? GameNet.GetLocalPlayer().name : "null")}");
            }

            if (isLocal)
            {
                // ── periodic state send ──
                stateSendTimer += Time.deltaTime;
                if (stateSendTimer >= StateSendInterval)
                {
                    SendStateData();
                    stateSendTimer = 0f;
                }

                // ── periodic health send ──
                healthSendTimer += Time.deltaTime;
                if (healthSendTimer >= HealthSendInterval)
                {
                    SendHealthData();
                    healthSendTimer = 0f;
                }

                // ── aim / crouch change events ──
                bool isAiming    = PlayerCtrl.IsAiming();
                bool isCrouching = PlayerCtrl.IsCrouch();

                if (lastAiming != isAiming)
                {
                    SendEventData(PlayerEventID.CtrlKey, (int)CtrlKey.Aim, isAiming ? 1 : 0);
                    lastAiming = isAiming;
                }
                if (lastCrouching != isCrouching)
                {
                    SendEventData(PlayerEventID.CtrlKey, (int)CtrlKey.Crouch, isCrouching ? 1 : 0);
                    lastCrouching = isCrouching;
                }
            }
            else
            {
                // ── face nametag toward local camera ──
                if (nicknameCanvasRect != null)
                {
                    Vector3 dir = (PlayerPref.instance.MainCamera.transform.position - nicknameCanvasRect.position).normalized;
                    nicknameCanvasRect.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // LATEUPDATE (replaces PlayerControl.LateUpdate for all players)
        // ══════════════════════════════════════════════════════════════════════

        public void LateUpdate()
        {
            PlayerControl player = PlayerCtrl;
            if (!player.player_ini || Time.timeScale <= 0f) return;

            bool isLocal = player == GameNet.GetLocalPlayer();

            PlayerPref            pref      = player.Perf;
            Player_Helth          health    = player.Helth;
            Player_Equipment      equipment = player.Player_Equipment;
            Player_Config_manager keyInput  = player.KeyInput;
            PlayerAct_00          act00     = player.PlayerAct;
            Animator              anim      = player.anim;

            if (player.PlayerState == PlayerControl.State.Playable)
            {
                if (isLocal)
                {
                    // Aim
                    if ((pref.isMain || pref.isSub) && equipment.E_state == Player_Equipment.State.None)
                        player.aim = health.Sick < player.Sick_thredhold && Input.GetKey(keyInput.WeponHold);
                    else
                        player.aim = false;

                    // Movement inputs
                    if (health.Sick >= player.Sick_thredhold && player.IsCrouch())
                    {
                        player.input_h = false;
                        player.input_v = false;
                        player.float_h = 0f;
                        player.float_v = 0f;
                    }
                    else
                    {
                        float slope    = player.SlopeSpeed();
                        player.input_h = Input.GetButton("Horizontal");
                        player.input_v = Input.GetButton("Vertical");
                        player.float_h = Input.GetAxis("Horizontal") * slope;
                        player.float_v = Input.GetAxis("Vertical")   * slope;
                    }

                    // FPV toggle
                    if (player.IsAiming())
                    {
                        if (Input.GetKeyDown(keyInput.FirestPersonView))
                        {
                            if (player.FPV) player.SetTPS();
                            else            player.SetFPV();
                        }
                    }
                    else { player.SetTPS(); }

                    if (player.FPV && anim.GetBool(Animator.StringToHash("NearWall")) && player.IsSniper() && pref.isMain)
                        player.SetTPS();

                    // Weapon switching
                    if (act00.RELOAD_STEP == PlayerAct_00.ReloadState.None &&
                        health.Sick < player.Sick_thredhold &&
                        player.PlayerState == PlayerControl.State.Playable)
                    {
                        float wheel = Input.GetAxis("Mouse ScrollWheel");
                        player.MouseWheel = wheel;
                        if (!player.IsAiming())
                        {
                            if ((wheel < 0f || Input.GetKeyDown(keyInput.ChangeSubWepon)) && pref.isMain)
                            {
                                equipment.switchWepon = true;
                                player.ChangeEquipment(0);
                                SendEventData(PlayerEventID.WeaponSwitch, 0);
                            }
                            if ((wheel > 0f || Input.GetKeyDown(keyInput.ChangeMainWepon)) && pref.isSub)
                            {
                                equipment.switchWepon = true;
                                player.ChangeEquipment(1);
                                SendEventData(PlayerEventID.WeaponSwitch, 1);
                            }
                        }
                    }

                    // Crouch toggle
                    player.crouchHandler = Input.GetKeyDown(keyInput.Crouch);
                }
                else
                {
                    // Apply interpolated state for remote players.
                    ApplyInterpolatedState(player, pref, anim);
                    // Update aim direction based on remote camera for AimIK.
                    UpdateRemoteAimIK(player, pref);
                }

                // Animator parameters (shared – both local and remote need these set).
                anim.SetBool("Aim",    player.IsAiming());
                anim.SetBool("Crouch", player.IsCrouch());
                anim.SetFloat("H",     player.float_h);
                anim.SetFloat("V",     player.float_v);
                for (int i = 0; i < pref.SyncAnimator.Length; i++)
                {
                    pref.SyncAnimator[i].SetBool("Aim",    player.IsAiming());
                    pref.SyncAnimator[i].SetBool("Crouch", player.IsCrouch());
                    pref.SyncAnimator[i].SetFloat("H",     player.float_h);
                    pref.SyncAnimator[i].SetFloat("V",     player.float_v);
                }

                // CrouchFloat smooth
                float crouchTarget = player.IsCrouch() ? 1f : 0f;
                anim.SetFloat("CrouchFloat", crouchTarget, 0.1f, Time.deltaTime);
                for (int i = 0; i < pref.SyncAnimator.Length; i++)
                    pref.SyncAnimator[i].SetFloat("CrouchFloat", crouchTarget, 0.1f, Time.deltaTime);

                if (isLocal)
                {
                    if (player.crouchHandler && player.IsGrounded())
                        player.crouch = !player.IsCrouch();
                    player.crouchHandler = false;
                }

                player.MovementManagement(player.float_h, player.float_v, player.run, player.sprint);
                JumpManagement();

                float inputSum = Mathf.Clamp01(Mathf.Abs(player.float_h) + Mathf.Abs(player.float_v));
                anim.SetFloat("input_sum", inputSum);
                for (int i = 0; i < pref.SyncAnimator.Length; i++)
                    pref.SyncAnimator[i].SetFloat("input_sum", inputSum);

                // Sick animation parameters (now driven for all players).
                if (!isLocal && PlayerCtrl.Helth != null)
                {
                    float sick      = PlayerCtrl.Helth.Sick;
                    float sickSpeed = 1f - sick * 0.6f;
                    anim.SetFloat("Sick",       sick);
                    anim.SetFloat("Sick_Speed", sickSpeed);
                    for (int i = 0; i < pref.SyncAnimator.Length; i++)
                    {
                        pref.SyncAnimator[i].SetFloat("Sick",       sick);
                        pref.SyncAnimator[i].SetFloat("Sick_Speed", sickSpeed);
                    }
                }
            }
            else
            {
                player.aim = false;
            }

            player.CheckGroundStatus();
        }

        // Apply the current interpolated snapshot to the remote PlayerControl.
        private void ApplyInterpolatedState(PlayerControl player, PlayerPref pref, Animator anim)
        {
            if (!TryGetInterpolatedState(out StateSnapshot snap))
                return;

            // Position and rotation — set directly; remote players are visual puppets.
            player.transform.position = snap.Position;
            player.transform.rotation = snap.Rotation;

            // Movement inputs drive the animator blend tree.
            player.float_h = snap.FloatH;
            player.float_v = snap.FloatV;
            player.input_h = Mathf.Abs(snap.FloatH) > 0.01f;
            player.input_v = Mathf.Abs(snap.FloatV) > 0.01f;

            // Aim / crouch discrete state.
            player.aim   = snap.IsAiming;
            player.crouch = snap.IsCrouching;
            player.m_IsGrounded = snap.IsGrounded;

            // Camera forward on the remote's virtual camera (used for AimIK).
            player.cameraTransform.rotation = Quaternion.LookRotation(snap.CameraForward, Vector3.up);
            remoteAimForward = snap.CameraForward;

            // Sick — drive the health component so the animator parameter picks it up.
            if (player.Helth != null)
                player.Helth.Sick = snap.Sick;

            // Toilet event — drive the EventMotions animator layer directly from the
            // snapshot.  The ToiletEventManager.FixedUpdate is skipped for remote
            // players (patched in Patches.cs) so we must advance animations here.
            int evLayer = anim.GetLayerIndex("EventMotions");
            if (evLayer >= 0)
            {
                if (snap.ToiletAnimHash != 0)
                {
                    anim.SetLayerWeight(evLayer, 1f);
                    if (anim.GetCurrentAnimatorStateInfo(evLayer).fullPathHash != snap.ToiletAnimHash)
                        anim.Play(snap.ToiletAnimHash, evLayer, snap.ToiletAnimTime);
                }
                else if (anim.GetLayerWeight(evLayer) > 0f)
                {
                    anim.SetLayerWeight(evLayer, 0f);
                }
            }

            // PlayerState / FlyState – only apply "safe" state transitions here.
            // Discrete event-driven transitions (damage, vomit, etc.) are handled
            // by OnEventData so they arrive with correct timing.
            PlayerControl.State receivedState = (PlayerControl.State)snap.PlayerStateID;
            PlayerControl.Fly   receivedFly   = (PlayerControl.Fly)snap.FlyStateID;

            // Only synchronize FlyState transitions (jump/fall) — they are driven
            // by physics and look broken without syncing.
            if (player.FlyState != receivedFly)
                player.FlyState = receivedFly;
        }

        // Update AimIK target position for remote players based on their actual camera direction.
        private void UpdateRemoteAimIK(PlayerControl player, PlayerPref pref)
        {
            PlayerAct_00 act00 = player.GetComponent<PlayerAct_00>();
            if (act00 == null || !act00.player_ini)
                return;

            // Always update AimIK weights to handle transition when stopping aim
            float aimWeight = player.IsAiming() ? act00.AimIK_weight : 0f;
            if (act00._AimIK != null)
                act00._AimIK.solver.IKPositionWeight = Mathf.Lerp(act00._AimIK.solver.IKPositionWeight, aimWeight, act00.AImIK_Lerp);
            
            // Update sync animators' AimIKs as well
            for (int i = 0; i < act00.AimIKs.Length; i++)
            {
                if (act00.AimIKs[i] != null)
                    act00.AimIKs[i].solver.IKPositionWeight = Mathf.Lerp(act00.AimIKs[i].solver.IKPositionWeight, aimWeight, act00.AImIK_Lerp);
            }

            // Only update aim target position when aiming
            if (!player.IsAiming())
                return;

            // Get the weapon component to determine muzzle position
            var weapon = pref.isMain ? pref.E_mainWepon : pref.E_SubWepon;
            if (weapon == null)
                return;

            var gunControl = weapon.GetComponent<kaede_gunAct_control>();
            if (gunControl == null || gunControl.t_mussle == null)
                return;

            // Calculate aim direction from remote camera forward
            Vector3 muzzlePos = gunControl.t_mussle.position;
            Vector3 aimDirection = remoteAimForward;

            // Cast a ray from the muzzle in the aim direction to find the target
            RaycastHit hitInfo;
            Vector3 targetPos;
            if (Physics.Raycast(muzzlePos, aimDirection, out hitInfo, 1000f, pref.LayerMaskInfo[2]))
            {
                targetPos = hitInfo.point;
            }
            else
            {
                targetPos = muzzlePos + aimDirection * 100f;
            }

            // Update AimTarget position - this drives the AimIK
            if (act00.AimTarget != null)
            {
                act00.AimTarget.transform.position = targetPos;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // JUMP MANAGEMENT (replaces PlayerControl.JumpManagement)
        // ══════════════════════════════════════════════════════════════════════

        public void JumpManagement()
        {
            PlayerControl player = PlayerCtrl;
            if (player != GameNet.GetLocalPlayer() || player.FlyState != PlayerControl.Fly.none)
                return;

            AnimatorStateInfo si = player.anim.GetCurrentAnimatorStateInfo(0);
            player.stateInfo = si;

            if (player.timeToNextJump > 0f)
                player.timeToNextJump -= Time.deltaTime;

            if (!Input.GetKeyDown(player.KeyInput.Jump) || !(player.Helth.Sick < player.Sick_thredhold))
                return;
            if (!player.IsGrounded() || player.PlayerState != PlayerControl.State.Playable)
                return;

            if (player.IsAiming())
            {
                int rollHash_fwd  = Animator.StringToHash("Kaede_motion.roll_forward");
                int rollHash_back = Animator.StringToHash("Kaede_motion.roll_back");
                int rollHash_l    = Animator.StringToHash("Kaede_motion.roll_left");
                int rollHash_r    = Animator.StringToHash("Kaede_motion.roll_right");
                if (si.fullPathHash == rollHash_fwd  || si.fullPathHash == rollHash_back ||
                    si.fullPathHash == rollHash_l    || si.fullPathHash == rollHash_r)
                    return;

                if (player.float_h >= 0.5f || player.float_h <= -0.5f || player.float_v >= 0.5f)
                {
                    BeginJumpRoll("Kaede_motion.roll_forward");
                    SendEventData(PlayerEventID.Jump, 1);
                }
                else if (player.float_v <= -0.5f)
                {
                    BeginJumpRoll("Kaede_motion.roll_back");
                    SendEventData(PlayerEventID.Jump, 2);
                }
            }
            else
            {
                int idleHash   = Animator.StringToHash("Kaede_motion.Idle_00");
                int locoHash   = Animator.StringToHash("Kaede_motion.Locomotion");
                int crouchHash = Animator.StringToHash("Kaede_motion.crouch");
                if (si.fullPathHash != idleHash && si.fullPathHash != locoHash && si.fullPathHash != crouchHash)
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
                        SendEventData(PlayerEventID.Jump, 4);
                    }
                    else
                    {
                        BeginJumpType5();
                        SendEventData(PlayerEventID.Jump, 5);
                    }
                }
                else
                {
                    SendEventData(PlayerEventID.Jump, 3);
                }
            }
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
            PlayerCtrl.m_storeVector = new Vector3(0f, PlayerCtrl.jumpHeight * 1.5f, 0f);
            PlayerCtrl.m_Rigidbody.velocity = PlayerCtrl.m_storeVector;
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

        public void OnJumpData(int jumpType)
        {
            if (jumpType == 0) return;
            if      (jumpType == 1) BeginJumpRoll("Kaede_motion.roll_forward");
            else if (jumpType == 2) BeginJumpRoll("Kaede_motion.roll_back");
            else if (jumpType >= 3 && jumpType <= 5)
            {
                EndCrouchAnim();
                if (jumpType == 4) BeginJumpType4();
                else if (jumpType == 5) BeginJumpType5();
            }
        }

        public void OnControlData(CtrlKey key, bool isDown)
        {
            switch (key)
            {
            case CtrlKey.Aim:    PlayerCtrl.aim   = isDown; break;
            case CtrlKey.Crouch: PlayerCtrl.crouch = isDown; break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // KNIFE (CQB) – kept from original, wired through unified event system
        // ══════════════════════════════════════════════════════════════════════

        public void Act01Update(PlayerAct_01 act01)
        {
            if (!act01.player_ini) return;

            PlayerControl player = act01.playerControl;
            PlayerPref    pref   = act01.Perf;
            Animator      anim   = act01.anim;
            PlayerAct_00  act00  = act01.act;

            if (player.PlayerState != PlayerControl.State.Playable &&
                player.PlayerState != PlayerControl.State.OnKnifeAct)
                return;

            act01.stateInfo = anim.GetCurrentAnimatorStateInfo(act01.cqb_0);

            bool isLocal = player == GameNet.GetLocalPlayer();
            if (isLocal)
            {
                if (Input.GetKeyDown(act01.KeyInput.WeponUse) && !player.IsAiming() && !player.IsCrouch())
                {
                    if (act00.Granade != PlayerAct_00.GranadeState.None ||
                        DoorActManager.Instance.DoorState != DoorActManager.State.Door_Idle)
                        return;

                    if (act00.RELOAD_STEP != PlayerAct_00.ReloadState.None)
                    {
                        act00.CancelReload();
                        act00.RELOAD_STEP = PlayerAct_00.ReloadState.None;
                    }

                    if (act01.STEP == 0)
                    {
                        act01.OnClick = 1;
                        act01.STEP    = 1;
                        SendEventData(PlayerEventID.KnifeUse, 0);
                    }
                    else if (act01.STEP == 3)
                    {
                        act01.OnClick = 2;
                        SendEventData(PlayerEventID.KnifeUse, 1);
                    }
                    else if (act01.STEP == 4)
                    {
                        act01.OnClick = 3;
                        SendEventData(PlayerEventID.KnifeUse, 2);
                    }
                }
            }

            if (!player.IsGrounded()) act01.CancelKnife();

            if (act01.STEP > 2 && act01.STEP < 10)
            {
                Vector3    camFwd    = pref.MainCamera.transform.TransformDirection(Vector3.forward);
                camFwd.y = 0f;
                Quaternion targetRot = Quaternion.LookRotation(camFwd, Vector3.up);
                Quaternion newRot    = Quaternion.Slerp(act01.GetComponent<Rigidbody>().rotation, targetRot, 0.8f);
                act01.GetComponent<Rigidbody>().MoveRotation(newRot);
                if (PlayerPrefs.GetFloat("Key_ActDirection", 0f) == 1f)
                    act01.transform.rotation = newRot;
            }

            if (act01.STEP == 0) return;

            if (act01.STEP == 1)
            {
                act01.IsMove = false;
                anim.CrossFadeInFixedTime(act01.LayerName[0] + ".OnKnife", 0.1f);
                for (int j = 0; j < pref.SyncAnimator.Length; j++)
                    pref.SyncAnimator[j].CrossFadeInFixedTime(act01.LayerName[0] + ".OnKnife", 0.1f);
                act00.enabled = false;
                act01.STEP    = 2;
            }
            else if (act01.STEP == 2)
            {
                float lw = Mathf.Lerp(anim.GetLayerWeight(act01.cqb_0), 1f, 0.2f);
                anim.SetLayerWeight(act01.cqb_0, lw);
                for (int k = 0; k < pref.SyncAnimator.Length; k++)
                    pref.SyncAnimator[k].SetLayerWeight(act01.cqb_0, lw);
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
                    act01.STEP         = 3;
                }
            }
            else if (act01.STEP == 3)
            {
                act01.test = act01.stateInfo.normalizedTime;
                if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".CQB_00"))
                {
                    if ((act01.OnClick < 2 || isLocal) && act01.stateInfo.normalizedTime > 0.9f)
                    {
                        act01.ShowCollsion(0);
                        BeginKnifeAnim(".CQB_03", 0.3f);
                        player.PlayerState = PlayerControl.State.Playable;
                        act01.STEP         = 10;
                    }
                    else if (act01.stateInfo.normalizedTime > 0.6f && act01.OnClick >= 2)
                    {
                        BeginKnifeAnim(".CQB_01", 0f);
                        act01.STEP = 4;
                    }
                }
            }
            else if (act01.STEP == 4)
            {
                if (act01.stateInfo.fullPathHash == Animator.StringToHash(act01.LayerName[0] + ".CQB_01"))
                {
                    if ((act01.OnClick < 3 || isLocal) && act01.stateInfo.normalizedTime > 0.9f)
                    {
                        act01.ShowCollsion(0);
                        BeginKnifeAnim(".CQB_04", 0.3f);
                        player.PlayerState = PlayerControl.State.Playable;
                        act01.STEP         = 10;
                    }
                    else if (act01.stateInfo.normalizedTime > 0.65f && act01.OnClick >= 3)
                    {
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
                    act01.STEP         = 10;
                }
            }
            else if (act01.STEP == 10)
            {
                int offHash  = Animator.StringToHash(act01.LayerName[0] + ".OffKnife");
                int idleHash = Animator.StringToHash(act01.LayerName[0] + ".Idle_00");
                int noneHash = Animator.StringToHash(act01.LayerName[0] + ".None");

                void finishKnife()
                {
                    act01.Knife_Hand.SetActive(false);
                    act01.Knife_Holder.SetActive(true);
                    float lw = Mathf.Lerp(anim.GetLayerWeight(act01.cqb_0), 0f, 0.3f);
                    anim.SetLayerWeight(act01.cqb_0, lw);
                    for (int n = 0; n < pref.SyncAnimator.Length; n++)
                        pref.SyncAnimator[n].SetLayerWeight(act01.cqb_0, lw);
                    if (anim.GetLayerWeight(act01.cqb_0) < 0.1f)
                    {
                        act01.STEP    = 0;
                        act00.enabled = true;
                        anim.CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
                        anim.SetLayerWeight(act01.cqb_0, 0f);
                        for (int m = 0; m < pref.SyncAnimator.Length; m++)
                        {
                            pref.SyncAnimator[m].CrossFadeInFixedTime(act01.LayerName[0] + ".None", 0.05f);
                            pref.SyncAnimator[m].SetLayerWeight(act01.cqb_0, 0f);
                        }
                    }
                }

                if ((act01.stateInfo.fullPathHash == offHash && act01.stateInfo.normalizedTime > 0.9f) ||
                    act01.stateInfo.fullPathHash == idleHash)
                    finishKnife();

                if (act01.stateInfo.fullPathHash == noneHash)
                {
                    act01.STEP    = 0;
                    act00.enabled = true;
                    anim.SetLayerWeight(act01.cqb_0, 0f);
                    for (int p = 0; p < pref.SyncAnimator.Length; p++)
                        pref.SyncAnimator[p].SetLayerWeight(act01.cqb_0, 0f);
                }
            }
        }

        public void OnKnifeUseData(int step)
        {
            PlayerAct_01 act01 = PlayerCtrl.PlayerAct01;
            if (step == 0)
            {
                act01.OnClick = 1;
                act01.STEP    = 1;
            }
            else if (step == 1)
            {
                act01.OnClick = 2;
                if (act01.STEP == 0 || act01.STEP > 3)
                    ForceBeginKnifeStep(4, ".CQB_01");
            }
            else if (step == 2)
            {
                act01.OnClick = 3;
                if (act01.STEP == 0 || act01.STEP > 4)
                {
                    act01.ShowCollsion(0);
                    ForceBeginKnifeStep(5, ".CQB_02");
                }
            }
        }

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

        private void BeginKnifeAnim(string animSuffix, float duration)
        {
            PlayerAct_01 act01 = PlayerCtrl.PlayerAct01;
            Animator     anim  = PlayerCtrl.anim;
            PlayerPref   pref  = PlayerCtrl.Perf;
            string l0 = act01.LayerName[0] + animSuffix;
            string l1 = act01.LayerName[1] + animSuffix;
            anim.CrossFadeInFixedTime(l0, duration);
            anim.CrossFadeInFixedTime(l1, duration);
            for (int m = 0; m < pref.SyncAnimator.Length; m++)
            {
                pref.SyncAnimator[m].CrossFadeInFixedTime(l0, duration);
                pref.SyncAnimator[m].CrossFadeInFixedTime(l1, duration);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CHARACTER CHANGE
        // ══════════════════════════════════════════════════════════════════════

        private void BroadcastCharChangeData(int characterID)
        {
            byte[] data = new byte[sizeof(int) * 3];
            Utils.WriteInt(data, 0, (int)S2CPacketID.PlayerChangeChar);
            Utils.WriteInt(data, 4, (int)Info.ID);
            Utils.WriteInt(data, 8, characterID);
            BroadcastPacket(data);
        }

        private void SendCharChangeData(int characterID)
        {
            if (GameNet.IsServer)
                BroadcastCharChangeData(characterID);
            else
            {
                byte[] data = new byte[sizeof(int) * 2];
                Utils.WriteInt(data, 0, (int)C2SPacketID.PlayerChangeChar);
                Utils.WriteInt(data, 4, characterID);
                Client.SendPacket(data);
            }
        }

        public void SetPlayerCharacter(int characterID)
        {
            PlayerPref pref    = PlayerCtrl.Perf;
            bool       isLocal = PlayerCtrl == GameNet.GetLocalPlayer();

            if (isLocal && (pref.SaveDataFail || !pref.R18))
                characterID = 0;

            for (int i = 0; i < pref.PlayerData.Length; i++)
            {
                bool active = (i == characterID);
                for (int j = 0; j < pref.PlayerData[i].PlayerData.Length; j++)
                    pref.PlayerData[i].PlayerData[j].SetActive(active);
            }

            if (PlayerCtrl.GetComponent<Player_Equipment>().initialize)
                pref.changeWeponID(pref.Main_weponID);

            pref.PlayerCharacterID = characterID;
            Info.CharacterID       = (byte)characterID;

            if (isLocal)
            {
                SendCharChangeData(characterID);
                if (!pref.SaveDataFail)
                {
                    SaveData.SetInt(pref.KEY_PlayerCharacterID, characterID);
                    SaveData.Save();
                }
            }
            else if (PlayerCtrl.player_ini)
            {
                RepositionNametag();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DAMAGE / VOMIT events
        // ══════════════════════════════════════════════════════════════════════

        // Call this from a Harmony patch on Player_Helth.Damage when IsNetGame().
        public void SendDamageExploFront(bool isFront)
        {
            SendEventData(PlayerEventID.DamageExploFront, isFront ? 1 : 0);
        }

        private void OnDamageExploFront(bool isFront)
        {
            if (PlayerCtrl == GameNet.GetLocalPlayer()) return;
            string anim = isFront ? "Down_front" : "Down_back";
            PlayerCtrl.anim.CrossFadeInFixedTime(anim, 0f);
            PlayerPref pref = PlayerCtrl.Perf;
            for (int i = 0; i < pref.SyncAnimator.Length; i++)
                pref.SyncAnimator[i].CrossFadeInFixedTime(anim, 0f);
            PlayerCtrl.aim = false;
            PlayerCtrl.PlayerAct.WeponLayerWeight_IK_reset();
            PlayerCtrl.rotat_bias = 0f;
            PlayerCtrl.speed_bias = 0f;
            PlayerCtrl.PlayerState = PlayerControl.State.Damage_start;
        }

        // Call from Harmony patch on PlayerControl.Vomit.
        public void SendVomitEvent()
        {
            SendEventData(PlayerEventID.Vomit);
        }

        private void OnVomitEvent()
        {
            if (PlayerCtrl == GameNet.GetLocalPlayer()) return;
            PlayerControl player = PlayerCtrl;
            PlayerPref    pref   = player.Perf;
            player.PlayerAct.WeponLayerWeight_zero();
            player.rotat_bias = 0f;
            player.speed_bias = 0f;
            player.anim.SetLayerWeight(player.anim.GetLayerIndex("Facial"), 0f);
            for (int i = 0; i < pref.SyncAnimator.Length; i++)
                pref.SyncAnimator[i].SetLayerWeight(player.anim.GetLayerIndex("Facial"), 0f);
            player.PlayerState = PlayerControl.State.Vomit_start;
        }

        // ══════════════════════════════════════════════════════════════════════
        // WEAPON events (visual-only for now)
        // ══════════════════════════════════════════════════════════════════════

        public void SendGunFire()
        {
            SendEventData(PlayerEventID.GunFire);
        }

        private void OnGunFireEvent()
        {
            // Muzzle flash + shell casing – trigger via PlayerAct_00's existing effect system.
            // The gun GameObject on the remote player is already positioned correctly.
            PlayerAct_00 act00 = PlayerCtrl.PlayerAct;
            if (act00 != null && act00.g_anim != null)
                act00.anim.SetTrigger(act00.a_SHOT);
        }

        public void SendReload()
        {
            SendEventData(PlayerEventID.Reload);
        }

        private void OnReloadEvent()
        {
            PlayerAct_00 act00 = PlayerCtrl.PlayerAct;
            if (act00 != null)
                act00.anim.SetTrigger(act00.a_RELOAD);
        }

        private void OnWeaponSwitchEvent(int slot)
        {
            if (PlayerCtrl == GameNet.GetLocalPlayer()) return;
            if (slot == 0) PlayerCtrl.ChangeEquipment(0);
            else           PlayerCtrl.ChangeEquipment(1);
        }

        private void OnGrenadeEvent(int granadeState)
        {
            // Visual placeholder: handle grenade animation state on remote players.
            // Full grenade simulation (physics, damage) deferred to future work.
        }

        // ══════════════════════════════════════════════════════════════════════
        // NAMETAG
        // ══════════════════════════════════════════════════════════════════════

        private void RepositionNametag()
        {
            nicknameCanvasRect.SetParent((Info.CharacterID == 0 ? kaedeHeadObj : momijiHeadObj).transform);
        }

        private GameObject FindGameObjectFromLocal(GameObject obj)
        {
            string path = Utils.GetGameObjectPath(obj).Substring(27);
            return PlayerCtrl.transform.Find(path).gameObject;
        }
    }
}
