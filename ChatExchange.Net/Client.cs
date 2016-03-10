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
using System.Net;
using System.Text.RegularExpressions;
using CsQuery;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides access to chat rooms via logging in using OAuth.
    /// </summary>
    public class Client : IDisposable
    {
        private readonly Regex userUrl = new Regex("href=\"/users/\\d*?/", Extensions.RegexOpts);
        private readonly Regex openidDel = new Regex("https://openid\\.stackexchange\\.com/user/.*?\"", Extensions.RegexOpts);
        private readonly Regex hostParser = new Regex("https?://(chat.)?|/.*", Extensions.RegexOpts);
        private readonly Regex idParser = new Regex(".*/rooms/|/.*", Extensions.RegexOpts);
        private readonly string proxyUrl;
        private readonly string proxyUsername;
        private readonly string proxyPassword;
        private readonly string cookieKey;
        private string openidUrl;
        private bool disposed;

        /// <summary>
        /// Returns a collection of rooms the user is currently in.
        /// </summary>
        public ReadOnlyCollection<Room> Rooms { get; private set; }


        /// <summary>
        /// Logs in using the provided credentials.
        /// </summary>
        /// <param name="email">The account's registered OAuth email.</param>
        /// <param name="password">The account's password.</param>
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

            Rooms = new ReadOnlyCollection<Room>(new List<Room>());

            SEOpenIDLogin(email, password);
        }

        /// <summary>
        /// Logs in with proxy support
        /// </summary>
        public Client(string email, string password, string proxyUrl, string proxyUsername, string proxyPassword)
            : this(email, password)
        {
            this.proxyUrl = proxyUrl;
            this.proxyUsername = proxyUsername;
            this.proxyPassword = proxyPassword;
        }

#pragma warning disable CS1591
        ~Client()
        {
            Dispose();
        }
#pragma warning restore CS1591



        /// <summary>
        /// Joins a chat room specified by its URL.
        /// </summary>
        /// <param name="roomUrl">The URL of the chat room to join.</param>
        /// <returns>A Room object which provides access to chat events/actions.</returns>
        /// <exception cref="System.Exception">
        /// Thrown if you attempt to join a room you are currently in.
        /// </exception>
        public Room JoinRoom(string roomUrl)
        {
            var host = hostParser.Replace(roomUrl, "");
            var id = int.Parse(idParser.Replace(roomUrl, ""));

            return JoinRoom(host, id);
        }

        /// <summary>
        /// Joins a chat room specified by its host and ID.
        /// </summary>
        /// <param name="host">
        /// The host domain of the chat room.
        /// For example: meta.stackexchange.com</param>
        /// <param name="roomID">The unique identification number of the room.</param>
        /// <returns>A Room object which provides access to chat events/actions.</returns>
        /// <exception cref="System.Exception">
        /// Thrown if you attempt to join a room you are currently in.
        /// </exception>
        public Room JoinRoom(string host, int roomID)
        {
            if (Rooms.Any(room => room.Meta.Host == host && room.Meta.ID == roomID))
            {
                throw new Exception("Cannot join a room you are already in.");
            }

            if (Rooms.All(room => room.Meta.Host != host))
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

            var r = new Room(cookieKey, host, roomID, proxyUrl, proxyUsername, proxyPassword);
            var rms = Rooms.ToList();
            rms.Add(r);

            Rooms = new ReadOnlyCollection<Room>(rms);

            return r;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (Rooms?.Count > 0)
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

            var data = $"email={Uri.EscapeDataString(email)}&password=" +
                       $"{Uri.EscapeDataString(password)}&fkey=" +
                       CQ.Create(getResContent).GetInputValue("fkey");

            using (var res = RequestManager.PostRaw(cookieKey,
                "https://openid.stackexchange.com/account/login/submit",
                data))
            {
                if (res == null)
                {
                    throw new AuthenticationException("Unable to authenticate using OpenID.");
                }
                if (res.ResponseUri.ToString() != "https://openid.stackexchange.com/user")
                {
                    throw new AuthenticationException("Invalid OpenID credentials.");
                }

                var html = res.GetContent();
                var del = openidDel.Match(html).Value;

                openidUrl = del.Remove(del.Length - 1, 1);
            }
        }

        private void SiteLogin(string host)
        {
            var getResContent = RequestManager.Get(cookieKey, $"http://{host}/users/login");

            if (string.IsNullOrEmpty(getResContent))
            {
                throw new Exception($"Unable to find fkey from {host}.");
            }

            var fkey = CQ.Create(getResContent).GetInputValue("fkey");

            var data = $"fkey={fkey}" +
                       "&oauth_version=&oauth_server=&openid_username=&openid_identifier=" +
                       Uri.EscapeDataString(openidUrl);

            var referrer = $"https://{host}/users/login?returnurl=" +
                           Uri.EscapeDataString($"http://{host}/");

            using (var postRes = RequestManager.PostRaw(cookieKey,
                $"http://{host}/users/authenticate",
                data,
                referrer))
            {
                if (postRes == null)
                {
                    throw new AuthenticationException($"Unable to login to {host}.");
                }

                var html = postRes.GetContent();
                HandleConfirmationPrompt(postRes.ResponseUri.ToString(), html);
                TryFetchUserID(html);
            }
        }

        private void SEChatLogin()
        {
            var fkeyRes = RequestManager.Get(cookieKey, "https://stackexchange.com/users/login");
            var fkey = CQ.Create(fkeyRes).GetInputValue("fkey");
            var data = $"fkey={fkey}&oauth_version=&oauth_server=&openid_identifier={openidUrl}";
            var referrer = "https://stackexchange.com/users/login";
            var origin = "https://stackexchange.com";

            using (var res = RequestManager.PostRaw(cookieKey, 
                "http://stackexchange.com/users/authenticate",
                data,
                referrer,
                origin))
            {
                var html = res.GetContent();
                HandleConfirmationPrompt(res.ResponseUri.ToString(), html);
                TryFetchUserID(html);
            }
        }

        private void TryFetchUserID(string html)
        {
            var dom = CQ.Create(html);
            var id = 0;

            foreach (var e in dom[".topbar a"])
            {
                if (userUrl.IsMatch(e.OuterHTML))
                {
                    id = int.Parse(e.Attributes["href"].Split('/')[2]);
                    break;
                }
            }

            if (id == 0)
            {
                throw new AuthenticationException("Unable to login to Stack Exchange.");
            }
        }

        private void HandleConfirmationPrompt(string uri, string html)
        {
            if (!uri.ToString().StartsWith("https://openid.stackexchange.com/account/prompt")) return;

            var dom = CQ.Create(html);
            var session = dom["input"].First(e => e.Attributes["name"] != null &&
                                                  e.Attributes["name"] == "session");
            var fkey = dom.GetInputValue("fkey");
            var data = "session=" + session["value"] + "&fkey=" + fkey;

            RequestManager.Post(cookieKey, "https://openid.stackexchange.com/account/prompt/submit", data);
        }
    }
}
