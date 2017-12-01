using System;

namespace ChatExchangeDotNet.WebSockets
{
	internal class WebSocketWSSharp : WebSocket
	{
		private WebSocketSharp.WebSocket socket;
		private bool connected;



		~WebSocketWSSharp()
		{
			Dispose();
		}



		public override void Connect(string url, string origin)
		{
			if (connected)
			{
				throw new Exception("WebSocket is already connected.");
			}

			socket = new WebSocketSharp.WebSocket(url)
			{
				Origin = origin
			};

			socket.OnMessage += (o, e) =>
			{
				lastMsg = DateTime.UtcNow;

				OnMessageReceived(e.Data);
			};

			socket.OnError += (o, e) =>
			{
				OnExceptionRaised(e.Exception);
			};

			try
			{
				socket.Connect();
				connected = true;
			}
			catch (Exception ex)
			{
				OnExceptionRaised(ex);
			}
		}

		public override void Disconnect()
		{
			if (!connected)
			{
				throw new Exception("WebSocket is already disconnected.");
			}

			try
			{
				socket.Close();
				connected = false;
			}
			catch (Exception ex)
			{
				OnExceptionRaised(ex);
			}
		}

		public override void Dispose()
		{
			if (dispose) return;
			dispose = true;

			Disconnect();
			GC.SuppressFinalize(this);
		}
	}
}
