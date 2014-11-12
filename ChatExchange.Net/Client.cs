using System;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CsQuery;



namespace ChatExchangeDotNet
{
	public class Client
	{
		private readonly Regex hostParser = new Regex("https?://chat.|/rooms/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private readonly Regex idParser = new Regex(".*/rooms/|/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public List<Room> Rooms { get; private set; }



		public Client(string email, string password)
		{
			if (String.IsNullOrEmpty(email)) { throw new ArgumentException("'email' must not be null or empty.", "email"); }
			if (String.IsNullOrEmpty(password)) { throw new ArgumentException("'password' must not be null or empty.", "password"); }

			Rooms = new List<Room>();

			SEOpenIDLogin(email, password);
		}



		public Room JoinRoom(string roomURL)
		{
			var host = hostParser.Replace(roomURL, "");
			var id = int.Parse(idParser.Replace(roomURL, ""));

			if (Rooms.Any(room => room.Host == host && room.ID == id)) { throw new Exception("You're already in this room."); }

			if (Rooms.All(room => room.Host != host))
			{
				if (host.ToLowerInvariant() == "stackexchange.com")
				{
					throw new NotImplementedException();

					SEChatLogin();
				}
				else
				{
					SiteLogin(host);
				}
			}

			var r = new Room(host, id);

			Rooms.Add(r);

			return r;
		}


		private void SEOpenIDLogin(string email, string password)
		{
			var getRes = RequestManager.SendGETRequest("https://openid.stackexchange.com/account/login");

			if (getRes == null) { throw new Exception("Could not get OpenID fkey. Do you have an active internet connection?"); }

			var getResContent = RequestManager.GetResponseContent(getRes);

			var data = "email=" + Uri.EscapeDataString(email) + "&password=" + Uri.EscapeDataString(password) + "&fkey=" + CQ.Create(getResContent).GetFkey();

			RequestManager.CookiesToPass = RequestManager.GlobalCookies;

			var res = RequestManager.SendPOSTRequest("https://openid.stackexchange.com/account/login/submit", data);

			if (res == null) { throw new Exception("Could not login using OpenID. Have you entered the correct credentials and have an active internet connection?");}

			HandlePromt(res);
		}

		private void SiteLogin(string host)
		{
			var getRes = RequestManager.SendGETRequest("http://" + host + "/users/login?returnurl = %%2f");

			if (getRes == null) { throw new Exception("Could not get fkey from " + host + ". Do you have an active internet connection?"); }

			var getResContent = RequestManager.GetResponseContent(getRes);

			var data = "oauth_version=null&oauth_server=null&openid_identifier=" + Uri.EscapeDataString("https://openid.stackexchange.com/") + "&fkey=" + CQ.Create(getResContent).GetFkey();

			var postRes = RequestManager.SendPOSTRequest("http://" + host + "/users/authenticate", data);

			if (postRes == null) { throw new Exception("Could not login into site " + host + ". Have you entered the correct credentials and have an active internet connection?"); }

			HandlePromt(postRes);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		/// <param name="res"></param>
		private void HandlePromt(HttpWebResponse res)
		{
			if (res == null || res.ResponseUri == null || !res.ResponseUri.ToString().StartsWith("https://openid.stackexchange.com/account/prompt")) { return; }

			var dom = CQ.Create(RequestManager.GetResponseContent(res));

			var session = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "session").Attributes["value"];

			var data = "session=" + session + "&fkey=" + dom.GetFkey();

			RequestManager.SendPOSTRequest("https://openid.stackexchange.com/account/prompt/submit", data);
		}

		/// <summary>
		/// WARNING! This method is not yet implemented!
		/// </summary>
		private void SEChatLogin()
		{
			//var req = RequestManager.GetResponseContent(RequestManager.SendGETRequest("http://stackexchange.com/users/chat-login"));

			//var dom = CQ.Create(req);

			//var authToken = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "authToken").Attributes["value"];
			//var nonce = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "nonce").Attributes["value"];

			//var data = "authToken=" + authToken + "&nonce=" + nonce;

			//var res = RequestManager.GetResponseContent(RequestManager.SendPOSTRequest("http://chat.stackexchange.com/login/global-fallback", data, true, "http://stackexchange.com/users/chat-login"));

			//fkey = GetFkey(res);
		}
	}
}
