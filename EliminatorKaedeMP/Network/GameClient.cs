using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine.SceneManagement;

namespace EliminatorKaedeMP
{
	public class GameClient
	{
		private NetClient netClient;
		private UdpSocket udpSocket;

		public void Connect(string hostname, int port)
		{
			GameNet.IsClient = true;
			netClient = new NetClient();
			netClient.Connect(hostname, port);
			netClient.OnDisconnected = OnDisconnected;
			netClient.OnPacketReceived = OnHandshakePacketReceived;
			netClient.SendPacket(Encoding.UTF8.GetBytes("EKMP")); // Begin handshake
			GameNet.Player = null;
			GameNet.Players.Clear();

			udpSocket = new UdpSocket();
			udpSocket.StartClient(hostname, port);
			udpSocket.OnDatagramReceived = OnUdpDatagramReceived;
		}

		public void Disconnect()
		{
			netClient.Disconnect();
			udpSocket?.Close();
			udpSocket = null;
		}

		public void SendUdpPacket(byte[] bytes)
		{
			udpSocket?.Send(bytes);
		}

		private void SendUdpHandshake(uint playerID)
		{
			byte[] data = new byte[8];
			Utils.WriteInt(data, 0, (int)C2SPacketID.UdpHandshake);
			Utils.WriteInt(data, 4, (int)playerID);
			udpSocket.Send(data);
		}

		private void OnDisconnected(NetClient netClient)
		{
			GameNet.IsClient = false;
			udpSocket?.Close();
			udpSocket = null;
			foreach (EKMPPlayer player in GameNet.Players)
			{
				if (player.Info.ID != GameNet.Player.Info.ID)
					player.DestroyObjects();
			}
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

			Plugin.Log("Got valid server handshake confirmation, proceeding...");

			EKMPPlayerInfo playerInfo = new EKMPPlayerInfo();
			GameNet.InitLocalPlayerInfo(playerInfo);
			byte[] bytes2;
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
				{
					playerInfo.Write(writer, false);
				}
				bytes2 = stream.ToArray();
			}
			netClient.SendPacket(bytes2);

			netClient.OnPacketReceived = OnPacketReceived;
		}

		private void OnUdpDatagramReceived(byte[] data, IPEndPoint from)
		{
			OnPacketReceived(netClient, data);
		}

		private void OnPacketReceived(NetClient netClient, byte[] bytes)
		{
			try
			{
				using MemoryStream stream = new MemoryStream(bytes);
				using BinaryReader reader = new BinaryReader(stream);

				S2CPacketID packetID = (S2CPacketID)reader.ReadInt32();
				switch (packetID)
				{
				case S2CPacketID.GameJoinInfo:
				{
					GameJoinInfoData joinInfo = GameJoinInfoData.Read(reader);
					uint playerID = joinInfo.PlayerID;
					int sceneID = joinInfo.SceneID;
					SendUdpHandshake(playerID); // register our UDP endpoint with the server
					Plugin.CallOnMainThread(() =>
					{
						GameNet.CreateSelfPlayer(netClient, playerID);
						foreach (EKMPPlayerInfo playerInfo in joinInfo.PlayerInfos)
						{
							EKMPPlayer mpPlayer = new EKMPPlayer();
							mpPlayer.Initialize(netClient, playerInfo);
							GameNet.Players.Add(mpPlayer);
						}
						if (Utils.GetCurrentScene() != sceneID)
							SceneManager.LoadScene(sceneID);
					});
					break;
				}
				case S2CPacketID.SceneChange:
				{
					int sceneID = reader.ReadInt32();
					Plugin.CallOnMainThread(() => SceneManager.LoadScene(sceneID));
					break;
				}
				case S2CPacketID.PlayerJoin:
				{
					EKMPPlayerInfo playerInfo = EKMPPlayerInfo.Read(reader, true);
					Plugin.CallOnMainThread(() =>
					{
						EKMPPlayer mpPlayer = new EKMPPlayer();
						mpPlayer.Initialize(null, playerInfo);
						mpPlayer.OnJoin();
					});
					break;
				}
				case S2CPacketID.PlayerLeave:
				{
					uint playerID = reader.ReadUInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnDisconnect());
					break;
				}
				case S2CPacketID.PlayerState:
				{
					uint playerID = reader.ReadUInt32();
					PlayerStateData data = PlayerStateData.Read(reader);
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID)?.OnStateData(data));
					break;
				}
				case S2CPacketID.PlayerEvent:
				{
					uint playerID = reader.ReadUInt32();
					PlayerEventID eventID = (PlayerEventID)reader.ReadInt32();
					int data0 = reader.ReadInt32();
					int data1 = reader.ReadInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID)?.OnEventData(eventID, data0, data1));
					break;
				}
				case S2CPacketID.PlayerHealth:
				{
					uint playerID = reader.ReadUInt32();
					PlayerHealthData health = PlayerHealthData.Read(reader);
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID)?.OnHealthData(health));
					break;
				}
				case S2CPacketID.PlayerChangeChar:
				{
					uint playerID = reader.ReadUInt32();
					int charID = reader.ReadInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).SetPlayerCharacter(charID));
					break;
				}
				case S2CPacketID.PlayerClothInfo:
				{
					uint playerID = reader.ReadUInt32();
					EKMPPlayerClothInfo clothInfo = EKMPPlayerClothInfo.Read(reader);
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnClothInfoData(clothInfo));
					break;
				}
				case S2CPacketID.ToiletState:
				{
					uint playerID = reader.ReadUInt32();
					ToiletStateData toiletData = ToiletStateData.Read(reader);
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnToiletStateData(toiletData));
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
