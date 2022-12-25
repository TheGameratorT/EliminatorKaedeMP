using K_PlayerControl;
using System.Collections.Generic;

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

        // Gets the local player controller, null if not in game
        public static PlayerControl GetLocalPlayer()
        {
            return PlayerPref.instance?.PlayerIncetance?.GetComponent<PlayerControl>();
        }

        // Gets the multiplayer handle of a player
        public static EKMPPlayer GetPlayer(PlayerControl player)
        {
            if (player == GetLocalPlayer())
                return Player;
            return ((EKMPPlayerPref)player.Perf).MPPlayer;
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
			playerInfo.Name = Utils.GetPlayerName();
			playerInfo.CharacterID = (byte)PlayerPref.instance.PlayerCharacterID;
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
