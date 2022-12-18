using K_PlayerControl;
using System.Collections.Generic;
using UnityEngine;

namespace EliminatorKaedeMP
{
    // This is our multiplayer handle for the player
    public class EKMPPlayer
    {
        public NetClient Client = null;
        public PlayerControl PlayerCtrl = null;
        public string Name;
        public uint ID;

        // Server - This function is called on the player that sent the packet
        public void OnPacketReceived(byte[] bytes)
        {

        }

        public void OnJoin()
        {
            GameNet.Players.Add(this);

            Plugin.Log(Name + " has joined!");
            
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
                Client.SendPacket(Utils.SerializePacket(S2CPacketID.GameJoinInfo, joinData));
            }
        }

        public void OnDisconnect()
        {
            GameNet.Players.Remove(this);
            if (PlayerCtrl != null)
                Object.Destroy(PlayerCtrl.gameObject);

            Plugin.Log(Name + " has left!");

            if (GameNet.IsServer)
            {
                byte[] bytes = new byte[8];
                Utils.WriteInt(bytes, 0, (int) S2CPacketID.PlayerLeave);
                Utils.WriteInt(bytes, 4, (int) ID);
                GameNet.Server.BroadcastPacket(bytes);
            }
        }

        public void OnMove()
        {

        }
    }
}
