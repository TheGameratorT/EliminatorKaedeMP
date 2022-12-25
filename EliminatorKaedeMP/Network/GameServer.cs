using System.IO;
using System.Text;

namespace EliminatorKaedeMP
{
    public class GameServer
    {
        private NetServer netServer;
        private uint nextPlayerID = 0;

        public void Start(int port)
        {
            GameNet.IsServer = true;
            netServer = new NetServer();
            netServer.Start(port);
            netServer.OnClientConnected = OnClientConnected;
            Plugin.Log("The server is running on port: " + port);

            // Create our player
            GameNet.CreateSelfPlayer(null, nextPlayerID);
			nextPlayerID++;
		}

        public void Stop()
        {
            netServer.Stop();
            netServer = null;
            GameNet.IsServer = false;
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
            EKMPPlayerInfo playerInfo;
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
					playerInfo = EKMPPlayerInfo.Read(reader, false);
				}
            }

            Plugin.CallOnMainThread(() =>
            {
				playerInfo.ID = nextPlayerID;
				EKMPPlayer mpPlayer = new EKMPPlayer();
				mpPlayer.Initialize(netClient, playerInfo);

                nextPlayerID++;

                netClient.OnPacketReceived = (NetClient netClient, byte[] bytes) =>
                {
                    mpPlayer.OnPacketReceived(bytes);
                };
                netClient.OnDisconnected = (NetClient netClient) =>
                {
                    Plugin.CallOnMainThread(() => mpPlayer.OnDisconnect());
                };

                mpPlayer.OnJoin();
            });
        }

        // Sends a packet to all the players, except to the server because it already has the data
        public void BroadcastPacket(byte[] bytes)
        {
            foreach (EKMPPlayer player in GameNet.Players)
            {
                if (player.Info.ID != GameNet.Player.Info.ID)
                    player.Client.SendPacket(bytes);
            }
        }
    }
}
