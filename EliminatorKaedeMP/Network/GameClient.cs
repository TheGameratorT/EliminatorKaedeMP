using System;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;

namespace EliminatorKaedeMP
{
    public class GameClient
    {
        private NetClient netClient;

        public void Connect(string hostname, int port)
        {
            netClient = new NetClient();
            netClient.Connect(hostname, port);
            netClient.OnDisconnected = OnDisconnected;
            netClient.OnPacketReceived = OnHandshakePacketReceived;
            netClient.SendPacket(Encoding.UTF8.GetBytes("EKMP")); // Begin handshake
            GameNet.IsConnected = true;
            GameNet.Player = null;
            GameNet.Players.Clear();
        }

        public void Disconnect()
        {
            netClient.Disconnect();
        }

        private void OnDisconnected(NetClient netClient)
        {
            GameNet.IsConnected = false;
            GameNet.Player = null;
            GameNet.Players.Clear();
            Plugin.Log("Disconnected from the server.");
        }

        private void OnHandshakePacketReceived(NetClient netClient, byte[] bytes)
        {
            if (Encoding.UTF8.GetString(bytes) != "EKMP")
            {
                Plugin.Log("Got invalid handshake confirmation from server.");
                netClient.Disconnect();
                return;
            }

            Plugin.Log("Handshake valid, sending username...");
            
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(GameNet.PlayerName);
            netClient.SendPacket(stream.ToArray());

            netClient.OnPacketReceived = OnPacketReceived;
        }

        private void OnPacketReceived(NetClient netClient, byte[] bytes)
        {
            try
            {
                using MemoryStream stream = new MemoryStream(bytes);
                using BinaryReader reader = new BinaryReader(stream);

                S2CPacket packetID = (S2CPacket) reader.Read();
                switch (packetID)
                {
                case S2CPacket.GameJoinInfo:
                {
                    // Receive all information about the game
                    GameJoinInfoData joinInfo = (GameJoinInfoData) Utils.DeserializePacket(stream);
                    uint playerID = joinInfo.PlayerID;
                    Plugin.CallOnMainThread(() =>
                    {
                        foreach (PlayerInfoData playerInfo in joinInfo.PlayerInfos)
                        {
                            EKMPPlayer mpPlayer = new EKMPPlayer();
                            if (playerInfo.ID == playerID)
			    	            GameNet.Player = mpPlayer; // This is our player instance
                            mpPlayer.Client = null;
                            mpPlayer.Player = null;
                            mpPlayer.ID = playerInfo.ID;
                            mpPlayer.Name = playerInfo.Name;
                            mpPlayer.OnJoin();
                        }
                    });
                    break;
                }
                case S2CPacket.PlayerJoin:
                {
                    EKMPPlayer mpPlayer = new EKMPPlayer();
                    mpPlayer.Client = null;
                    mpPlayer.Player = null;
                    mpPlayer.ID = reader.ReadUInt32();
                    mpPlayer.Name = reader.ReadString();
                    Plugin.CallOnMainThread(() => mpPlayer.OnJoin());
                    break;
                }
                case S2CPacket.PlayerLeave:
                {
                    uint playerID = reader.ReadUInt32();
                    Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnDisconnected());
                    break;
                }
                case S2CPacket.PlayerMove:
                {
                    uint playerID = reader.ReadUInt32();
                    Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnMove());
                    break;
                }
                case S2CPacket.SceneChange:
                {
                    int sceneID = reader.Read();
                    Plugin.CallOnMainThread(() => SceneManager.LoadScene(sceneID));
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
    }
}
