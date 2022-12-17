using System.IO;
using System.Net;
using System.Text;

namespace EliminatorKaedeMP
{
    public class GameServer
    {
        private NetServer netServer;
        private uint nextPlayerID = 0;

        public void Start(int port)
        {
            netServer = new NetServer();
            netServer.Start(port);
            netServer.OnClientConnected = OnClientConnected;
            Plugin.Log("The server is running on port: " + port);

            CreateSelfPlayer(); // Create our own player
        }

        public void Stop()
        {
            netServer.Stop();
            netServer = null;
            Plugin.Log("The server has stopped.");
        }

        public void OnClientConnected(NetClient netClient)
        {
            netClient.OnPacketReceived = OnHandshakePacketReceived;
        }

        private void OnHandshakePacketReceived(NetClient netClient, byte[] bytes)
        {
            // Here we check if an handshake packet is valid and
            // if true let the client know and wait for the player information

            if (Encoding.UTF8.GetString(bytes) != "EKMP")
            {
                netClient.Disconnect();
                return;
            }

            netClient.SendPacket(bytes); // Send back to the client what he gave us
            netClient.OnPacketReceived = OnPlayerInfoPacketReceived; // Wait for player information
        }

        private void OnPlayerInfoPacketReceived(NetClient netClient, byte[] bytes)
        {
            using MemoryStream stream = new MemoryStream(bytes);
            using BinaryReader reader = new BinaryReader(stream);

            EKMPPlayer mpPlayer = new EKMPPlayer();
            mpPlayer.Client = netClient;
            mpPlayer.Player = null;
            mpPlayer.Name = reader.ReadString();
            mpPlayer.ID = nextPlayerID;

            nextPlayerID++;

            netClient.OnPacketReceived = (NetClient netClient, byte[] bytes) => {
                mpPlayer.OnPacketReceived(bytes);
            };
            netClient.OnDisconnected = (NetClient netClient) => {
                Plugin.CallOnMainThread(() => mpPlayer.OnDisconnected());
            };

            Plugin.CallOnMainThread(() => mpPlayer.OnJoin());
        }

        private void CreateSelfPlayer()
        {
            EKMPPlayer mpPlayer = new EKMPPlayer();
            mpPlayer.Client = null;
            mpPlayer.Player = null;
            mpPlayer.Name = GameNet.PlayerName;
            mpPlayer.ID = nextPlayerID;
            nextPlayerID++;
            GameNet.Player = mpPlayer;
            GameNet.Players.Add(mpPlayer);
        }

        public void NotifyPlayerJoined(EKMPPlayer player)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            
            writer.Write((int) S2CPacket.PlayerJoin);
            writer.Write(player.ID);
            writer.Write(player.Name);

            BroadcastPacket(stream.ToArray());
        }

        // Sends a packet to all the players, except ourselves
        public void BroadcastPacket(byte[] bytes)
        {
            foreach (EKMPPlayer player in GameNet.Players)
            {
                if (player.ID != GameNet.Player.ID)
                    player.Client.SendPacket(bytes);
            }
        }
    }
}
