using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatExchangeDotNet
{
    internal class WebSocket : IDisposable
    {
        private CancellationTokenSource cTkn;
        private DateTime lastMsg = DateTime.MinValue;
        private string socketUrl;
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
            Task.Run(() => WSRecovery());
        }

        ~WebSocket()
        {
            Dispose();
        }



        public void Connect(string url)
        {
            if (connected)
            {
                throw new Exception("WebSocket is already connected.");
            }

            socketUrl = url;
            Task.Run(() => ListenerLoop());
        }

        public void Disconnect()
        {
            if (!connected)
            {
                throw new Exception("WebSocket is already disconnected.");
            }

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
                        socket.ConnectAsync(new Uri(socketUrl), CancellationToken.None).Wait();
                        while (!stop)
                        {
                            var buffer = new ArraySegment<byte>(new byte[5 * 1024]);
                            socket.ReceiveAsync(buffer, cTkn.Token);
                            var strMsg = Encoding.UTF8.GetString(buffer.Array);
                            OnMessage.Invoke(strMsg);
                            lastMsg = DateTime.UtcNow;
                        }
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                    }
                }
                catch (Exception ex)
                {
                    OnError.Invoke(ex);
                    Thread.Sleep(5000);
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
                        Connect(socketUrl);
                    }
                }
                catch (Exception ex)
                {
                    OnError.Invoke(ex);
                }

                Thread.Sleep(1000);
            }
        }
    }
}
