using K_PlayerControl;
using System.Collections.Generic;
using UnityEngine;

namespace EliminatorKaedeMP
{
    public class GameNet
    {
        public static bool IsServer = false;
        public static bool IsClient = false;
        public static GameServer Server;
        public static GameClient Client;
        public static readonly List<EKMPPlayer> Players = new List<EKMPPlayer>();
        public static EKMPPlayer Player = null;

        public static void CreateServer(int port)
        {
            Server = new GameServer();
            Server.Start(port);
        }

        public static void StopServer()
        {
            Server.Stop();
            Server = null;
        }

        public static void ConnectToServer(string hostname, int port)
        {
            Client = new GameClient();
            Client.Connect(hostname, port);
        }

        public static void Disconnect()
        {
            Client.Disconnect();
            Client = null;
		}

        // Gets a player controller by the ID of the player, null if not found
        public static EKMPPlayer GetPlayer(uint playerID)
        {
            foreach (EKMPPlayer player in Players)
            {
                if (player.Info.ID == playerID)
                    return player;
            }
            return null;
        }

        private static int _localPlayerNullLogs = 0;

        // Gets the local player controller, null if not in game
        public static PlayerControl GetLocalPlayer()
        {
            var result = PlayerPref.instance?.PlayerIncetance?.GetComponent<PlayerControl>();
            if (result == null && _localPlayerNullLogs < 5)
            {
                _localPlayerNullLogs++;
                Plugin.Log($"[DIAG] GetLocalPlayer null #{_localPlayerNullLogs}: PlayerPref.instance={(PlayerPref.instance != null ? "SET" : "NULL")}");
            }
            return result;
        }

        // Gets the multiplayer handle of a player
        public static EKMPPlayer GetPlayer(PlayerControl player)
        {
            if (player == GetLocalPlayer())
                return Player;
            try { return ((EKMPPlayerPref)player.Perf).MPPlayer; }
            catch (System.Exception ex) { Plugin.Log($"[DIAG] GetPlayer cast failed for {player.name}: {ex.Message}"); return null; }
        }

        // Returns true if we are a server or if we are a client connected to a server, false otherwise
        public static bool IsNetGame()
        {
            return IsServer || IsClient;
        }

        public static void OnGameStart()
        {
            if (!IsNetGame())
                return;

            // We must make sure that the players are spawned if we enter a game
            foreach (EKMPPlayer player in Players)
				player.TryInstantiatePlayer();
        }

        public static void InitLocalPlayerInfo(EKMPPlayerInfo playerInfo)
		{
			PlayerPref pref = PlayerPref.instance;
			UI_ClothSystem cs = pref.GetComponent<UI_ClothSystem>();

			playerInfo.Name = Utils.GetPlayerName();
			playerInfo.CharacterID = (byte)pref.PlayerCharacterID;

            EKMPPlayerClothInfo clothInfo = new EKMPPlayerClothInfo();
			clothInfo.ClothID = (byte)cs.clothID;
			clothInfo.S_underHair = cs.S_underHair;
			clothInfo.S_underHair_alpha = cs.S_underHair_alpha;
			clothInfo.S_underHair_density = cs.S_underHair_density;
			clothInfo.S_HairStyle = cs.S_HairStyle;
			clothInfo.S_HIYAKE_kosa = cs.S_HIYAKE_kosa;
			clothInfo.S_HIYAKE_patan = cs.S_HIYAKE_patan;
			clothInfo.S_MatColor = new Color[10];
			for (int i = 0; i < 10; i++)
			{
				Color def = cs.DefaultColor[i];
				int dr = Mathf.FloorToInt(def.r * 255f);
				int dg = Mathf.FloorToInt(def.g * 255f);
				int db = Mathf.FloorToInt(def.b * 255f);
				int da = Mathf.FloorToInt(def.a * 255f);
				Color color;
				color.r = PlayerPrefs.GetInt(cs.KEY_MatColor[i, 0], dr) / 255.0f;
				color.g = PlayerPrefs.GetInt(cs.KEY_MatColor[i, 1], dg) / 255.0f;
				color.b = PlayerPrefs.GetInt(cs.KEY_MatColor[i, 2], db) / 255.0f;
				color.a = PlayerPrefs.GetInt(cs.KEY_MatColor[i, 3], da) / 255.0f;
				clothInfo.S_MatColor[i] = color;
			}
			playerInfo.Cloth = clothInfo;
		}

        // Creates an EKMPPlayer instance for our local player
        public static void CreateSelfPlayer(NetClient netClient, uint playerID)
		{
			EKMPPlayerInfo playerInfo = new EKMPPlayerInfo();
			playerInfo.ID = playerID;
			InitLocalPlayerInfo(playerInfo);
			EKMPPlayer mpPlayer = new EKMPPlayer();
			Player = mpPlayer;
			mpPlayer.Initialize(netClient, playerInfo); // Must come after GameNet.Player = player
			Players.Add(mpPlayer);
		}
    }
}
