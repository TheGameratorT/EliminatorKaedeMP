using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace EliminatorKaedeMP
{
    public class NetClient
    {
        public delegate void DisconnectedCallback(NetClient client);
        public delegate void PacketReceivedCallback(NetClient client, byte[] bytes);

        private TcpClient tcpClient;
        private NetworkStream tcpStream;
        private List<byte[]> writePktList = new List<byte[]>();

        public DisconnectedCallback OnDisconnected = null;
        public PacketReceivedCallback OnPacketReceived = null;

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

        public void Attach(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            StartIO();
        }

        public void Disconnect()
        {
            try
            {
                tcpStream?.Close();
                tcpClient?.Close();
            }
            catch { }
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

        // Reads exactly 'count' bytes into buffer[offset..offset+count-1], then calls onComplete.
        // Calls Disconnect() if the stream closes before all bytes are read.
        private void ReadExactBytes(byte[] buffer, int offset, int count, Action onComplete)
        {
            if (count == 0)
            {
                onComplete();
                return;
            }
            try
            {
                tcpStream.BeginRead(buffer, offset, count, (IAsyncResult result) =>
                {
                    int fetched;
                    try { fetched = tcpStream.EndRead(result); }
                    catch { Disconnect(); return; }

                    if (fetched == 0) { Disconnect(); return; }

                    int remaining = count - fetched;
                    if (remaining == 0)
                        onComplete();
                    else
                        ReadExactBytes(buffer, offset + fetched, remaining, onComplete);
                }, null);
            }
            catch { Disconnect(); }
        }

        private void ReadIncomingPacketHeader()
        {
            byte[] header = new byte[4];
            ReadExactBytes(header, 0, 4, () =>
            {
                int bodySize = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
                if (bodySize < 0 || bodySize > 10 * 1024 * 1024) // 10 MB sanity cap
                {
                    Plugin.Log("Received packet with invalid size: " + bodySize);
                    Disconnect();
                    return;
                }
                ReadIncomingPacketBody(bodySize);
            });
        }

        private void ReadIncomingPacketBody(int bodySize)
        {
            byte[] body = new byte[bodySize];
            ReadExactBytes(body, 0, bodySize, () =>
            {
                OnPacketReceived?.Invoke(this, body);
                ReadIncomingPacketHeader();
            });
        }

        private void WriteOutgoingPacket()
        {
            byte[] payload;
            lock (writePktList)
                payload = writePktList[0];

            byte[] frame = new byte[payload.Length + 4];
            frame[0] = (byte)payload.Length;
            frame[1] = (byte)(payload.Length >> 8);
            frame[2] = (byte)(payload.Length >> 16);
            frame[3] = (byte)(payload.Length >> 24);
            Array.Copy(payload, 0, frame, 4, payload.Length);

            try
            {
                tcpStream.BeginWrite(frame, 0, frame.Length, (IAsyncResult result) =>
                {
                    try { tcpStream.EndWrite(result); }
                    catch { Disconnect(); return; }

                    bool more;
                    lock (writePktList)
                    {
                        writePktList.RemoveAt(0);
                        more = writePktList.Count != 0;
                    }
                    if (more)
                        WriteOutgoingPacket();
                }, null);
            }
            catch (Exception ex)
            {
                Plugin.Log(ex);
                Disconnect();
            }
        }

        private void StartIO()
        {
            tcpStream = tcpClient.GetStream();
            ReadIncomingPacketHeader();
            bool hasPending;
            lock (writePktList)
                hasPending = writePktList.Count != 0;
            if (hasPending)
                WriteOutgoingPacket();
        }

        public IPAddress GetAddress()
        {
            return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address;
        }
    }
}
