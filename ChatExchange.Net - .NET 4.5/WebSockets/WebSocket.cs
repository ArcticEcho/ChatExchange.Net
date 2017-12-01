using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatExchangeDotNet.WebSockets
{
	internal abstract class WebSocket : IDisposable
	{
		protected DateTime lastMsg = DateTime.MaxValue;
		protected string socketUrl;
		protected string orgn;
		protected bool stop;
		protected bool dispose;

		public TimeSpan IdlePeriod { get; set; } = TimeSpan.FromSeconds(120);

		public delegate void OnMessageEventHandler(string data);
		public event OnMessageEventHandler MessageReceived;

		public delegate void OnErrorEventHandler(Exception ex);
		public event OnErrorEventHandler ExceptionRaised;

		public delegate string OnReconnectNeededEventHandler();
		public event OnReconnectNeededEventHandler ReconnectNeeded;

		public abstract void Connect(string url, string origin);

		public abstract void Disconnect();

		public abstract void Dispose();

		protected virtual void WSRecovery()
		{
			while (!dispose)
			{
				try
				{
					if ((DateTime.UtcNow - lastMsg) > IdlePeriod)
					{
						socketUrl = OnReconnectNeeded();
						Disconnect();
						Connect(socketUrl, orgn);
					}
				}
				catch (Exception ex)
				{
					OnExceptionRaised(ex);
				}

				Thread.Sleep(5000);
			}
		}

		protected void OnMessageReceived(string data)
		{
			if (MessageReceived != null)
			{
				MessageReceived.Invoke(data);
			}
		}

		protected void OnExceptionRaised(Exception e)
		{
			if (MessageReceived != null)
			{
				ExceptionRaised.Invoke(e);
			}
		}

		protected string OnReconnectNeeded()
		{
			return ReconnectNeeded?.Invoke();
		}
	}
}
