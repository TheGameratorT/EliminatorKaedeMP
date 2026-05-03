using System;
using System.Net;
using System.Net.Sockets;

namespace EliminatorKaedeMP
{
    public class UdpSocket
    {
        public delegate void DatagramCallback(byte[] data, IPEndPoint from);
        public DatagramCallback OnDatagramReceived;

        private UdpClient udpClient;

        // Server: bind to port, receive datagrams from any endpoint.
        public void StartServer(int port)
        {
            udpClient = new UdpClient(port);
            BeginReceive();
        }

        // Client: target a specific server; only datagrams from that endpoint are delivered.
        public void StartClient(string hostname, int port)
        {
            udpClient = new UdpClient();
            udpClient.Connect(hostname, port);
            BeginReceive();
        }

        // Client: send to connected server.
        public void Send(byte[] data)
        {
            try { udpClient.Send(data, data.Length); }
            catch { }
        }

        // Server: send to a specific client endpoint.
        public void SendTo(byte[] data, IPEndPoint endpoint)
        {
            try { udpClient.Send(data, data.Length, endpoint); }
            catch { }
        }

        public void Close()
        {
            try { udpClient.Close(); }
            catch { }
        }

        private void BeginReceive()
        {
            try
            {
                udpClient.BeginReceive((IAsyncResult ar) =>
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data;
                    try { data = udpClient.EndReceive(ar, ref remote); }
                    catch { return; }
                    OnDatagramReceived?.Invoke(data, remote);
                    BeginReceive();
                }, null);
            }
            catch { }
        }
    }
}
