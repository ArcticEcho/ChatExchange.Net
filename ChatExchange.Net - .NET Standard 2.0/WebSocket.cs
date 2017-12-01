using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatExchangeDotNet
{
    internal class WebSocket : IDisposable
    {
        private CancellationTokenSource cTkn;
        private DateTime lastMsg = DateTime.MaxValue;
        private string socketUrl;
        private string orgn;
        private bool connected;
        private bool stop;
        private bool dispose;

        public delegate void OnMessageEventHandler(string data);
        public event OnMessageEventHandler OnMessage;

        public delegate void OnErrorEventHandler(Exception ex);
        public event OnErrorEventHandler OnError;

        public delegate string OnReconnectNeededEventHandler();
        public event OnReconnectNeededEventHandler OnReconnectNeeded;



        public WebSocket()
        {
            cTkn = new CancellationTokenSource();
            Task.Run(() => WSRecovery());
        }

        ~WebSocket()
        {
            Dispose();
        }



        public void Connect(string url, string origin)
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

        public void Disconnect()
        {
            if (!connected)
            {
                throw new Exception("WebSocket is already disconnected.");
            }

            connected = false;
            stop = true;
            cTkn.Cancel();
        }

        public void Dispose()
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

                            OnMessage.Invoke(strMsg);

                            lastMsg = DateTime.UtcNow;
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
						OnError.Invoke(ex);
						Thread.Sleep(5000);
					}
                }
            }

            stop = false;
            cTkn = new CancellationTokenSource();
        }

        private void WSRecovery()
        {
            while (!dispose)
            {
                try
                {
                    if ((DateTime.UtcNow - lastMsg).TotalSeconds > 30)
                    {
                        socketUrl = OnReconnectNeeded.Invoke();
                        Disconnect();
                        Connect(socketUrl, orgn);
                    }
                }
                catch (Exception ex)
                {
                    OnError.Invoke(ex);
                }

                Thread.Sleep(5000);
            }
        }
    }
}
