using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace EliminatorKaedeMP
{
	public class NetServer
	{
		public delegate void ConnectedCallback(NetClient client);

		private TcpListener tcpListener = null;
		private Thread acceptorThread = null;
		private bool acceptorRunning = false;

		public ConnectedCallback OnClientConnected = null;

		public void Start(int port)
		{
			tcpListener = new TcpListener(IPAddress.Any, port);
			tcpListener.Start();

			acceptorRunning = true;
			acceptorThread = new Thread(new ThreadStart(AcceptorThread));
			acceptorThread.Start();
		}

		public void Stop()
		{
			tcpListener.Stop();
			acceptorRunning = false;
			acceptorThread.Join();
		}

		private void AcceptorThread()
		{
			while (acceptorRunning)
			{
				TcpClient tcpClient;
				try
				{
					tcpClient = tcpListener.AcceptTcpClient();
				}
				catch (Exception)
				{
					continue;
				}

				NetClient netClient = new NetClient();
				netClient.Attach(tcpClient);
				OnClientConnected?.Invoke(netClient);
			}
		}
	}
}
