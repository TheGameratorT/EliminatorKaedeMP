using K_PlayerControl;
using System.Collections.Generic;

namespace EliminatorKaedeMP
{
    // This is our multiplayer handle for the player
    public class EKMPPlayer
    {
        public NetClient Client = null;
        public PlayerControl Player = null;
        public string Name;
        public uint ID;

        // Server - This function is called on the player that sent the packet
        public void OnPacketReceived(byte[] bytes)
        {

        }

        public void OnJoin()
        {
            GameNet.Players.Add(this);
            
            if (GameNet.IsServer)
            {
                GameNet.Server.NotifyPlayerJoined(this); // This is sent to everyone, except the player who joined

                // This is sent to the player who joined, unless that player is also the server
                GameJoinInfoData joinData;
                joinData.PlayerID = ID;
                joinData.PlayerInfos = new List<PlayerInfoData>();
                foreach (EKMPPlayer mpPlayer in GameNet.Players)
                {
                    PlayerInfoData playerInfo;
                    playerInfo.ID = mpPlayer.ID;
                    playerInfo.Name = mpPlayer.Name;
                    joinData.PlayerInfos.Add(playerInfo);
                }
                Client.SendPacket(Utils.SerializePacket(S2CPacket.GameJoinInfo, joinData));
            }
        }

        public void OnDisconnected()
        {
            GameNet.Players.Remove(this);

            if (GameNet.IsServer)
            {
                byte[] bytes = new byte[8];
                Utils.WriteInt(bytes, 0, (int) S2CPacket.PlayerLeave);
                Utils.WriteInt(bytes, 4, (int) ID);
                GameNet.Server.BroadcastPacket(bytes);
            }
        }

        public void OnMove()
        {

        }
    }
}
