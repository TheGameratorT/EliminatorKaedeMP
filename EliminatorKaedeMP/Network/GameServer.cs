﻿using K_PlayerControl;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

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

            CreateSelfPlayer(); // Create our own player
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
            using MemoryStream stream = new MemoryStream(bytes);
            using BinaryReader reader = new BinaryReader(stream);

            EKMPPlayer mpPlayer = new EKMPPlayer();
            mpPlayer.Client = netClient;
            mpPlayer.TryInstantiateNetPlayer();
            mpPlayer.Name = reader.ReadString();
            mpPlayer.ID = nextPlayerID;

            nextPlayerID++;

            netClient.OnPacketReceived = (NetClient netClient, byte[] bytes) => {
                mpPlayer.OnPacketReceived(bytes);
            };
            netClient.OnDisconnected = (NetClient netClient) => {
                Plugin.CallOnMainThread(() => mpPlayer.OnDisconnect());
            };

            Plugin.CallOnMainThread(() => mpPlayer.OnJoin());
        }

        private void CreateSelfPlayer()
        {
            EKMPPlayer mpPlayer = new EKMPPlayer();
            mpPlayer.Client = null;
            mpPlayer.PlayerCtrl = GameNet.GetLocalPlayer();
            mpPlayer.Name = Utils.GetPlayerName();
            mpPlayer.ID = nextPlayerID;
            nextPlayerID++;
            GameNet.Player = mpPlayer;
            GameNet.Players.Add(mpPlayer);
        }

        public void NotifyPlayerJoined(EKMPPlayer playerWhoJoined)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            
            writer.Write((int) S2CPacketID.PlayerJoin);
            writer.Write(playerWhoJoined.ID);
            writer.Write(playerWhoJoined.Name);

            byte[] bytes = stream.ToArray();
            foreach (EKMPPlayer player in GameNet.Players)
            {
                if (player.ID != GameNet.Player.ID && player != playerWhoJoined)
                    player.Client.SendPacket(bytes);
            }
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
