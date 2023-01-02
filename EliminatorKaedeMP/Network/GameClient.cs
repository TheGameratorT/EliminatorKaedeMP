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
			GameNet.IsClient = true;
			netClient = new NetClient();
			netClient.Connect(hostname, port);
			netClient.OnDisconnected = OnDisconnected;
			netClient.OnPacketReceived = OnHandshakePacketReceived;
			netClient.SendPacket(Encoding.UTF8.GetBytes("EKMP")); // Begin handshake
			GameNet.Player = null;
			GameNet.Players.Clear();
		}

		public void Disconnect()
		{
			netClient.Disconnect();
		}

		private void OnDisconnected(NetClient netClient)
		{
			GameNet.IsClient = false;
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

			// Send the player info, server will receive it on GameServer.OnPlayerInfoPacketReceived
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
					// Receive all information about the game
					GameJoinInfoData joinInfo = GameJoinInfoData.Read(reader);
					uint playerID = joinInfo.PlayerID;
					int sceneID = joinInfo.SceneID;
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
				case S2CPacketID.PlayerMove:
				{
					uint playerID = reader.ReadUInt32();
					stream.Position = 8;
					PlayerMoveData playerMoveData = (PlayerMoveData)Utils.Deserialize(stream);
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnMoveData(playerMoveData));
					break;
				}
				case S2CPacketID.PlayerJump:
				{
					uint playerID = reader.ReadUInt32();
					int jumpType = reader.ReadInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnJumpData(jumpType));
					break;
				}
				case S2CPacketID.PlayerCtrlKey:
				{
					uint playerID = reader.ReadUInt32();
					EKMPPlayer.CtrlKey key = (EKMPPlayer.CtrlKey)reader.ReadInt32();
					bool isDown = reader.ReadInt32() != 0 ? true : false;
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnControlData(key, isDown));
					break;
				}
				case S2CPacketID.PlayerKnifeUse:
				{
					uint playerID = reader.ReadUInt32();
					int state = reader.ReadInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).OnKnifeUseData(state));
					break;
				}
				case S2CPacketID.PlayerChangeChar:
				{
					uint playerID = reader.ReadUInt32();
					int charID = reader.ReadInt32();
					Plugin.CallOnMainThread(() => GameNet.GetPlayer(playerID).SetPlayerCharacter(charID));
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
