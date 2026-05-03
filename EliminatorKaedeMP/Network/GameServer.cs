using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace EliminatorKaedeMP
{
    public class GameServer
    {
        private NetServer netServer;
        private uint nextPlayerID = 0;

        private readonly object udpMapLock = new object();
        private readonly Dictionary<string, EKMPPlayer> udpEndpointMap = new Dictionary<string, EKMPPlayer>();

        public void Start(int port)
        {
            GameNet.IsServer = true;
            netServer = new NetServer();
            netServer.Start(port);
            netServer.OnClientConnected = OnClientConnected;
            netServer.UdpSocket.OnDatagramReceived = OnUdpDatagramReceived;
            Plugin.Log("The server is running on port: " + port);

            // Create our player
            GameNet.CreateSelfPlayer(null, nextPlayerID);
			nextPlayerID++;
		}

        public void Stop()
        {
            netServer.Stop();
            netServer = null;
            lock (udpMapLock) udpEndpointMap.Clear();
            GameNet.IsServer = false;
            Plugin.Log("The server has stopped.");
        }

        public void OnClientConnected(NetClient netClient)
        {
            netClient.OnPacketReceived = OnHandshakePacketReceived;
        }

        private void OnHandshakePacketReceived(NetClient netClient, byte[] bytes)
        {
            if (Encoding.UTF8.GetString(bytes) != "EKMP")
            {
                netClient.Disconnect();
                return;
            }

            netClient.SendPacket(bytes);
            netClient.OnPacketReceived = OnPlayerInfoPacketReceived;
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

        public void SendUdpTo(byte[] bytes, IPEndPoint endpoint)
        {
            netServer.UdpSocket.SendTo(bytes, endpoint);
        }

        private void OnUdpDatagramReceived(byte[] data, IPEndPoint from)
        {
            if (data.Length < 4) return;
            C2SPacketID packetID = (C2SPacketID)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));

            if (packetID == C2SPacketID.UdpHandshake)
            {
                if (data.Length < 8) return;
                uint playerID = (uint)(data[4] | (data[5] << 8) | (data[6] << 16) | (data[7] << 24));
                Plugin.CallOnMainThread(() =>
                {
                    EKMPPlayer player = GameNet.GetPlayer(playerID);
                    if (player == null) return;
                    player.UdpEndpoint = from;
                    lock (udpMapLock)
                        udpEndpointMap[from.ToString()] = player;
                });
                return;
            }

            EKMPPlayer sender;
            lock (udpMapLock)
                udpEndpointMap.TryGetValue(from.ToString(), out sender);
            sender?.OnPacketReceived(data);
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
