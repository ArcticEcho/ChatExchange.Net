/*
 * ChatExchange.Net. A .Net (4.0) API for interacting with Stack Exchange chat.
 * Copyright © 2015, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using CsQuery;

namespace ChatExchangeDotNet
{
    public class Client : IDisposable
    {
        private readonly Regex hostParser = new Regex("https?://(chat.)?|/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly Regex idParser = new Regex(".*/rooms/|/.*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly string cookieKey;
        private string openidUrl;
        private bool disposed;

        public List<Room> Rooms { get; private set; }



        public Client(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                throw new ArgumentException("'email' must be a valid email address.", "email");
            }
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("'password' must not be null or empty.", "password");
            }

            cookieKey = email.Split('@')[0];

            if (RequestManager.Cookies.ContainsKey(cookieKey))
            {
                throw new Exception("Cannot create multiple instances of the same user.");
            }

            RequestManager.Cookies.Add(cookieKey, new CookieContainer());

            Rooms = new List<Room>();

            SEOpenIDLogin(email, password);
        }

        ~Client()
        {
            Dispose();
        }



        public Room JoinRoom(string roomUrl)
        {
            var host = hostParser.Replace(roomUrl, "");
            var id = int.Parse(idParser.Replace(roomUrl, ""));

            if (Rooms.Any(room => room.Host == host && room.ID == id))
            {
                throw new Exception("Cannot join a room you are already in.");
            }

            if (Rooms.All(room => room.Host != host))
            {
                if (host.ToLowerInvariant() == "stackexchange.com")
                {
                    SEChatLogin();
                }
                else
                {
                    SiteLogin(host);
                }
            }

            var r = new Room(cookieKey, host, id);

            Rooms.Add(r);

            return r;
        }

        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;

            if (Rooms != null && Rooms.Count > 0)
            {
                foreach (var room in Rooms)
                {
                    room.Dispose();
                }
            }

            if (!string.IsNullOrEmpty(cookieKey))
            {
                RequestManager.Cookies.Remove(cookieKey);
            }

            GC.SuppressFinalize(this);
        }



        private void SEOpenIDLogin(string email, string password)
        {
            var getResContent = RequestManager.Get(cookieKey, "https://openid.stackexchange.com/account/login");

            if (string.IsNullOrEmpty(getResContent))
            {
                throw new Exception("Unable to find OpenID fkey.");
            }

            var data = "email=" + Uri.EscapeDataString(email) + "&password=" +
                       Uri.EscapeDataString(password) + "&fkey=" +
                       CQ.Create(getResContent).GetInputValue("fkey");

            using (var res = RequestManager.PostRaw(cookieKey, "https://openid.stackexchange.com/account/login/submit", data))
            {
                if (res == null)
                {
                    throw new AuthenticationException("Unable to authenticate using OpenID.");
                }
                if (!string.IsNullOrEmpty(res.Headers["p3p"]))
                {
                    throw new AuthenticationException("Invalid OpenID credentials.");
                }

                var dom = CQ.Create(res.GetContent());

                openidUrl = WebUtility.HtmlDecode(dom["#delegate"][0].InnerHTML);
                openidUrl = openidUrl.Remove(0, openidUrl.LastIndexOf("href", StringComparison.Ordinal) + 6);
                openidUrl = openidUrl.Remove(openidUrl.IndexOf("\"", StringComparison.Ordinal));
            }
        }

        private void SiteLogin(string host)
        {
            var getResContent = RequestManager.Get(cookieKey, "http://" + host + "/users/login");

            if (string.IsNullOrEmpty(getResContent))
            {
                throw new Exception("Unable to find fkey from " + host + ".");
            }

            var fkey = CQ.Create(getResContent).GetInputValue("fkey");

            var data = "fkey=" + fkey +
                       "&oauth_version=&oauth_server=&openid_username=&openid_identifier=" +
                       Uri.EscapeDataString(openidUrl);

            var referrer = "https://" + host + "/users/login?returnurl=" +
                           Uri.EscapeDataString("http://" + host + "/");

            using (var postRes = RequestManager.PostRaw(cookieKey, "http://" + host + "/users/authenticate", data, referrer))
            {
                if (postRes == null)
                {
                    throw new AuthenticationException("Unable to login to " + host + ".");
                }

                HandleConfirmationPrompt(postRes);
            }
        }

        private void HandleConfirmationPrompt(HttpWebResponse res)
        {
            if (!res.ResponseUri.ToString().StartsWith("https://openid.stackexchange.com/account/prompt")) { return; }

            var dom = CQ.Create(res.GetContent());
            var session = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "session");
            var fkey = dom.GetInputValue("fkey");
            var data = "session=" + session["value"] + "&fkey=" + fkey;

            RequestManager.Post(cookieKey, "https://openid.stackexchange.com/account/prompt/submit", data);
        }

        private void SEChatLogin()
        {
            // Login to SE.
            RequestManager.Get(cookieKey, "http://stackexchange.com/users/authenticate?openid_identifier=" + Uri.EscapeDataString(openidUrl));

            var html = RequestManager.Get(cookieKey, "http://stackexchange.com/users/chat-login");
            var dom = CQ.Create(html);
            var authToken = Uri.EscapeDataString(dom.GetInputValue("authToken"));
            var nonce = Uri.EscapeDataString(dom.GetInputValue("nonce"));
            var data = "authToken=" + authToken + "&nonce=" + nonce;
            var refOrigin = "http://chat.stackexchange.com";

            // Login to chat.SE.
            var postResContent = RequestManager.Post(cookieKey, "http://chat.stackexchange.com/users/login/global", data, refOrigin, refOrigin);
        }
    }
}
