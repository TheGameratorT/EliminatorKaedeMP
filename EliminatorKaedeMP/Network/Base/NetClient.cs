using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace EliminatorKaedeMP
{
	public class NetClient
	{
		private delegate void ReadSatisfiedCallback(byte[] bytes);
		public delegate void DisconnectedCallback(NetClient client);
		public delegate void PacketReceivedCallback(NetClient client, byte[] bytes);

		private const int RECEIVE_BUFFER_SIZE = 1024;

		private TcpClient tcpClient;
		private NetworkStream tcpStream;
		private List<byte[]> writePktList = new List<byte[]>();
		private byte[] receiveBuffer = new byte[RECEIVE_BUFFER_SIZE];
		private int readPktSize = 0;
		private int bytesToRead = 0;
		private int bytesRead = 0;
		private ReadSatisfiedCallback readSatisfiedCallback;

		public DisconnectedCallback OnDisconnected = null;
		public PacketReceivedCallback OnPacketReceived = null;

		// Use this if you want to create a new TcpClient instance, otherwise use Attach
		public void Connect(string hostname, int port)
		{
			try
			{
				tcpClient = new TcpClient();
				tcpClient.Connect(hostname, port);
				StartIO();
			}
			catch (Exception ex)
			{
				Plugin.Log(ex);
			}
		}

		// Use this if you already have a TcpClient instance, otherwise use Connect
		public void Attach(TcpClient tcpClient)
		{
			this.tcpClient = tcpClient;
			StartIO();
		}

		public void Disconnect()
		{
			tcpStream.Close();
			tcpClient.Close();
			OnDisconnected?.Invoke(this);
		}

		public void SendPacket(byte[] bytes)
		{
			bool noWriteInProgress;
			lock (writePktList)
			{
				noWriteInProgress = writePktList.Count == 0;
				writePktList.Add(bytes);
			}
			if (noWriteInProgress)
				WriteOutgoingPacket();
		}

		private void ReadPacket(int byteCount, ReadSatisfiedCallback readSatisfiedCallback)
		{
			if (byteCount > RECEIVE_BUFFER_SIZE)
			{
				Plugin.Log("Tried to read more bytes than buffer can hold.");
				readSatisfiedCallback(null);
				return;
			}

			bytesToRead = byteCount;
			bytesRead = 0;
			this.readSatisfiedCallback = readSatisfiedCallback;
			ReadUntilSatisfied();
		}

		// Reads from the network stream until the packet has been fully obtained
		private void ReadUntilSatisfied()
		{
			tcpStream.BeginRead(receiveBuffer, bytesRead, bytesToRead - bytesRead, (IAsyncResult result) =>
			{
				int fetchedBytes = tcpStream.EndRead(result);
				if (fetchedBytes == 0)
				{
					readSatisfiedCallback(null);
					return;
				}

				bytesRead += fetchedBytes;
				if (fetchedBytes == bytesToRead)
				{
					byte[] newData = new byte[bytesToRead];
					Array.Copy(receiveBuffer, newData, bytesToRead);
					readSatisfiedCallback(newData);
					return;
				}

				ReadUntilSatisfied();
			}, null);
		}

		private void ReadIncomingPacketHeader()
		{
			ReadPacket(4, (byte[] bytes) =>
			{
				if (bytes == null)
				{
					Disconnect();
					return;
				}
				readPktSize = receiveBuffer[0] | (receiveBuffer[1] << 8) | (receiveBuffer[2] << 16) | (receiveBuffer[3] << 24);
				ReadIncomingPacketBody();
			});
		}

		private void ReadIncomingPacketBody()
		{
			ReadPacket(readPktSize, (byte[] bytes) =>
			{
				if (bytes == null)
				{
					Disconnect();
					return;
				}
				OnPacketReceived?.Invoke(this, bytes);
				ReadIncomingPacketHeader();
			});
		}

		private void WriteOutgoingPacket()
		{
			byte[] writePkt;
			lock (writePktList)
				writePkt = writePktList[0];

			byte[] pktData = new byte[writePkt.Length + 4];
			pktData[0] = (byte)writePkt.Length;
			pktData[1] = (byte)(writePkt.Length >> 8);
			pktData[2] = (byte)(writePkt.Length >> 16);
			pktData[3] = (byte)(writePkt.Length >> 24);
			Array.Copy(writePkt, 0, pktData, 4, writePkt.Length);

			try
			{
				tcpStream.BeginWrite(pktData, 0, pktData.Length, (IAsyncResult result) =>
				{
					tcpStream.EndWrite(result);

					bool writeInProgress;
					lock (writePktList)
					{
						writePktList.RemoveAt(0);
						writeInProgress = writePktList.Count != 0;
					}
					if (writeInProgress)
						WriteOutgoingPacket();
				}, null);
			}
			catch (IOException)
			{
				// Connection was lost
				Disconnect();
			}
			catch (Exception ex)
			{
				// Something went wrong
				Plugin.Log(ex);
				Disconnect();
			}
		}

		private void StartIO()
		{
			tcpStream = tcpClient.GetStream();
			ReadIncomingPacketHeader();
			if (writePktList.Count != 0)
				WriteOutgoingPacket();
		}

		public IPAddress GetAddress()
		{
			return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
		}
	}
}
