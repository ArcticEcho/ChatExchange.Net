using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatExchangeDotNet.WebSockets
{
    internal class WebSocketNative : WebSocket
    {
        private CancellationTokenSource cTkn;
		private bool connected;



        public WebSocketNative()
        {
            cTkn = new CancellationTokenSource();
            Task.Run(() => WSRecovery());
        }

        ~WebSocketNative()
        {
            Dispose();
        }



        public override void Connect(string url, string origin)
        {
            if (connected)
            {
                throw new Exception("WebSocket is already connected.");
            }

            connected = true;
            socketUrl = url;
            orgn = origin;
            Task.Run(() => ListenerLoop());
        }

        public override void Disconnect()
        {
            if (!connected)
            {
                throw new Exception("WebSocket is already disconnected.");
            }

            connected = false;
            stop = true;
            cTkn.Cancel();
        }

        public override void Dispose()
        {
            if (dispose) return;
            dispose = true;
            stop = true;

            cTkn.Cancel();
            GC.SuppressFinalize(this);
        }



        private void ListenerLoop()
        {
            while (!stop)
            {
                try
                {
                   using (var socket = new ClientWebSocket())
                    {
                        socket.Options.SetRequestHeader("Origin", orgn);
                        socket.ConnectAsync(new Uri(socketUrl), CancellationToken.None).Wait();

                        while (!stop && socket.State == WebSocketState.Open)
                        {
                            var buffer = new ArraySegment<byte>(new byte[5 * 1024]);
                            var res = socket.ReceiveAsync(buffer, cTkn.Token).Result;

                            if (res.MessageType != WebSocketMessageType.Text) continue;

                            var strMsg = Encoding.UTF8.GetString(buffer.Array, 0, res.Count);

                            lastMsg = DateTime.UtcNow;

                            OnMessageReceived(strMsg);
                        }

                        if (socket.State == WebSocketState.Open)
                        {
                            socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                        }
                    }
                }
                catch (Exception ex)
                {
					if (ex.InnerException.GetType() != typeof(TaskCanceledException))
					{
						OnExceptionRaised(ex);
						Thread.Sleep(5000);
					}
                }
            }

            stop = false;
            cTkn = new CancellationTokenSource();
        }
    }
}
