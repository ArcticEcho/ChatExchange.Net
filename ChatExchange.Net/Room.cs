using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CsQuery;
using Newtonsoft.Json.Linq;
using WebSocket4Net;



namespace ChatExchangeDotNet
{
	public class Room
	{
		private readonly RequestManager reqManager = new RequestManager();
		private string fkey;
		private readonly string host;
		private CookieCollection siteCookies = new CookieCollection();

		public int ID { get; private set; }
		
		public Action<Message> NewMessageEvent { get; set; }



		// TODO: Clean up code after debugging.

		public Room(string roomUrl, string email, string password)
		{
			if (String.IsNullOrEmpty(roomUrl)) { throw new ArgumentException("roomUrl must not be null or empty.", "roomUrl"); }
			if (String.IsNullOrEmpty(email)) { throw new ArgumentException("email must not be null or empty.", "email"); }
			if (String.IsNullOrEmpty(roomUrl)) { throw new ArgumentException("password must not be null or empty.", "password"); }

			ID = int.Parse(new Regex(@".*/rooms/|/.*").Replace(roomUrl, ""));
			host = new Regex("https?://chat.|/rooms/.*").Replace(roomUrl, "");

			// Login.

			SEOpenIDLogin(email, password);
			SiteLogin();

			if (host.ToLowerInvariant() == "stackexchange.com")
			{
				SEChatLogin();
			}

			// Join room.

			var data = "roomid=" + ID + "&fkey=" + fkey; // "since=0&mode=Messages&msgCount=100&fkey=" + fkey;

			var cookies = reqManager.CookiesToPass;

			reqManager.CookiesToPass = null;

			var t = reqManager.GetResponseContent(reqManager.SendPOSTRequest("http://chat." + host + "/chats/" + ID + "/events", data));

			var eventTime = (int)JObject.Parse(t)["time"];

			// Initialise websocket.

			data = "roomid=" + ID + "&fkey=" + fkey;

			var url = "http://chat." + host + "/ws-auth";

			cookies = new CookieContainer();

			cookies.Add(siteCookies);

			reqManager.CookiesToPass = cookies;

			var r = reqManager.GetResponseContent(reqManager.SendPOSTRequest(url, data)); // Returns "Oops" page, rather than requested data.

			var wsurl = (string)JObject.Parse(r)["url"] + "?l=" + eventTime;

			var socket = new WebSocket(wsurl, "", null, null, "", "http://chat." + host);

			socket.MessageReceived += (o, oo) =>
			{
				var g = oo.Message;

				// TODO: Finish parsing received message data.

				var message = (string)JObject.Parse(g)["content"];

				NewMessageEvent(new Message(message, -1, "", -1));
			};
		}



		private void SEOpenIDLogin(string email, string password)
		{
			var req = reqManager.GetResponseContent(reqManager.SendGETRequest("https://openid.stackexchange.com/account/login"));

			var data = "email=" + Uri.EscapeDataString(email) + "&password=" + Uri.EscapeDataString(password) + "&fkey=" + GetFkey(CQ.Create(req));

			var res = reqManager.SendPOSTRequest("https://openid.stackexchange.com/account/login/submit", data);

			HandlePromt(res);
		}

		private void SiteLogin()
		{
			var req = reqManager.GetResponseContent(reqManager.SendGETRequest("http://" + host + "/users/login?returnurl = %%2f"));

			fkey = GetFkey(CQ.Create(req));

			var data = "oauth_version=null&oauth_server=null&openid_identifier=" + Uri.EscapeDataString("https://openid.stackexchange.com/") + "&fkey=" + fkey;
			
			var res = reqManager.SendPOSTRequest("http://" + host + "/users/authenticate", data);

			siteCookies = res.Cookies;

			HandlePromt(res);
		}

		private void HandlePromt(HttpWebResponse res)
		{
			if (res == null || res.ResponseUri == null || !res.ResponseUri.ToString().StartsWith("https://openid.stackexchange.com/account/prompt")) { return; }

			var dom = CQ.Create(reqManager.GetResponseContent(res));

			var session = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "session").Attributes["value"];

			var data = "session=" + session + "&fkey=" + GetFkey(dom);

			reqManager.SendPOSTRequest("https://openid.stackexchange.com/account/prompt/submit", data);
		}

		/// <summary>
		/// WARNING! This method is still broken!
		/// </summary>
		private void SEChatLogin()
		{
			var req = reqManager.GetResponseContent(reqManager.SendGETRequest("http://stackexchange.com/users/chat-login"));

			var dom = CQ.Create(req);

			//var authToken = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "authToken").Attributes["value"];
			//var nonce = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "nonce").Attributes["value"];

			//var data = "authToken=" + authToken + "&nonce=" + nonce;

			var data = "oauth_version=null&oauth_server=null&openid_identifier=" + Uri.EscapeDataString("https://openid.stackexchange.com/") + "&fkey=" + fkey;

			var res = reqManager.GetResponseContent(reqManager.SendPOSTRequest("http://chat.stackexchange.com/login/global-fallback", data, true, "http://stackexchange.com/users/chat-login"));

			fkey = GetFkey(res);
		}

		private string GetFkey(CQ dom)
		{
			return dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "fkey").Attributes["value"];
		}
	}
}
