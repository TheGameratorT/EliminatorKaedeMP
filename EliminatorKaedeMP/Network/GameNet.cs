using K_PlayerControl;
using System.Collections.Generic;
using UnityEngine;

namespace EliminatorKaedeMP
{
    public class GameNet
    {
        public static bool IsServer = false;
        public static bool IsConnected = false;
        public static GameServer Server;
        public static GameClient Client;
        public static readonly List<EKMPPlayer> Players = new List<EKMPPlayer>();
        public static EKMPPlayer Player = null;
        public static string PlayerName = "TheGameratorT";

        public static void CreateServer(int port)
        {
            IsServer = true;
            Server = new GameServer();
            Server.Start(port);
        }

        public static void StopServer()
        {
            Server.Stop();
            Server = null;
            IsServer = false;
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
            IsConnected = false;
        }

        public static EKMPPlayer GetPlayer(uint playerID)
        {
            foreach (EKMPPlayer player in Players)
            {
                if (player.ID == playerID)
                    return player;
            }
            return null;
        }

        // Returns true if we are a server or if we are a client connected to a server, false otherwise
        public static bool IsNetGame()
        {
            return IsServer || IsConnected;
        }

        // Tries to create a player if in game
        public static PlayerControl TryInstantiatePlayer()
        {
            if (!Utils.IsInGame())
                return null;
            PlayerControl player = Utils.GetLocalPlayer();
            if (player == null)
                return null;
            PlayerControl newPlayer = Object.Instantiate(player.gameObject).GetComponent<PlayerControl>();
            return newPlayer;
        }
    }
}
