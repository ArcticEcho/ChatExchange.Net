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
using RestSharp;
using static ChatExchangeDotNet.RequestManager;

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
        private readonly string accountEmail;
        private readonly string accountPassword;
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

            accountEmail = email;
            accountPassword = password;
            cookieKey = email.Split('@')[0];

            if (Cookies.ContainsKey(cookieKey))
            {
                throw new Exception("Cannot create multiple instances of the same user.");
            }

            Rooms = new ReadOnlyCollection<Room>(new List<Room>());

            SEOpenIDLogin();
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
        /// <param name="loadUsersAsync">
        /// Specifies whether the Room object should fetch the: room owners,
        /// current users and pingable users lists asynchronously (true) or not (false).
        /// </param>
        /// <exception cref="Exception">
        /// Thrown if you attempt to join a room you are currently in.
        /// </exception>
        public Room JoinRoom(string roomUrl, bool loadUsersAsync = false)
        {
            var host = hostParser.Replace(roomUrl, "");
            var id = int.Parse(idParser.Replace(roomUrl, ""));

            return JoinRoom(host, id, loadUsersAsync);
        }

        /// <summary>
        /// Joins a chat room specified by its host and ID.
        /// </summary>
        /// <param name="host">
        /// The host domain of the chat room.
        /// For example: meta.stackexchange.com</param>
        /// <param name="roomID">The unique identification number of the room.</param>
        /// <param name="loadUsersAsync">
        /// Specifies whether the Room object should fetch the: room owners,
        /// current users and pingable users lists asynchronously (true) or not (false).
        /// </param>
        /// <returns>A Room object which provides access to chat events/actions.</returns>
        /// <exception cref="Exception">
        /// Thrown if you attempt to join a room you are currently in.
        /// </exception>
        public Room JoinRoom(string host, int roomID, bool loadUsersAsync = false)
        {
            if (Rooms.Any(room => room.Meta.Host == host && room.Meta.ID == roomID && !room.dispose))
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

            var r = new Room(cookieKey, host, roomID, proxyUrl, proxyUsername, proxyPassword, loadUsersAsync);
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
                Cookies.Remove(cookieKey);
            }

            GC.SuppressFinalize(this);
        }



        private void SEOpenIDLogin()
        {
            var getRes = SendRequest(cookieKey, GenerateRequest(Method.GET, "https://openid.stackexchange.com/account/login"));

            if (getRes == null || string.IsNullOrWhiteSpace(getRes.Content))
            {
                throw new WebException("Invalid response received while logging in. (#193)");
            }

            var req = GenerateRequest(Method.POST, "https://openid.stackexchange.com/account/login/submit", getRes.ResponseUri.OriginalString, "https://openid.stackexchange.com");

            req = req.AddData("email", accountEmail);
            req = req.AddData("password", accountPassword);
            req = req.AddData("fkey", CQ.Create(getRes.Content).GetInputValue("fkey"));

            var postRes = SendRequest(cookieKey, req);

            if (postRes == null)
            {
                throw new WebException("Invalid response received while logging in. (#206)");
            }
            if (postRes.ResponseUri.ToString() != "https://openid.stackexchange.com/user")
            {
                throw new AuthenticationException("Invalid OpenID credentials.");
            }

            Cookies[cookieKey] = Cookies[cookieKey].Where(x => x.Name != "anon").ToList();

            var html = postRes.Content;
            var del = openidDel.Match(html).Value;

            openidUrl = del.Remove(del.Length - 1, 1);
        }

        private void SiteLogin(string host)
        {
            var getRes = SendRequest(cookieKey, GenerateRequest(Method.GET, $"http://{host}/users/login"));

            if (getRes == null || string.IsNullOrWhiteSpace(getRes.Content))
            {
                throw new WebException("Invalid response received while logging in. (#227)");
            }

            var referrer = $"https://{host}/users/login?returnurl=" + Uri.EscapeDataString($"http://{host}/");

            var req = GenerateRequest(Method.POST, $"http://{host}/users/authenticate", referrer);

            req = req.AddData("fkey", CQ.Create(getRes.Content).GetInputValue("fkey"));
            req = req.AddData("openid_identifier", openidUrl);

            var postRes = SendRequest(cookieKey, req);

            if (postRes == null || string.IsNullOrWhiteSpace(postRes.Content))
            {
                throw new WebException($"Invalid response received while logging into {host}. (#241)");
            }

            HandleConfirmationPrompt(postRes.ResponseUri.ToString(), postRes.Content);
            TryFetchUserID(postRes.ResponseUri.Host);
        }

        private void SEChatLogin()
        {
            var fkeyRes = SendRequest(cookieKey, GenerateRequest(Method.GET, "https://stackexchange.com/users/login"));
            var fkey = CQ.Create(fkeyRes.Content).GetInputValue("fkey");

            var endpoint = "http://stackexchange.com/users/authenticate";
            var referrer = "https://stackexchange.com/users/login";
            var origin = "https://stackexchange.com";

            var req = GenerateRequest(Method.POST, endpoint, referrer, origin);

            req = req.AddData("fkey", fkey);
            req = req.AddData("openid_identifier", openidUrl);
            req = req.AddData("oauth_version", "");
            req = req.AddData("oauth_server", "");

            var res = SendRequest(cookieKey, req);

            HandleConfirmationPrompt(res.ResponseUri.ToString(), res.Content);
            TryFetchUserID(res.ResponseUri.Host);
        }

        private void TryFetchUserID(string host)
        {
            var res = SendRequest(cookieKey, GenerateRequest(Method.GET, $"http://{host}/users/current"));

            var dom = CQ.Create(res.Content);
            var id = 0;

            foreach (var e in dom[".so-header a"])
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
            var resUrl = uri.ToString();

            if (resUrl.StartsWith("https://openid.stackexchange.com/account/prompt"))
            {
                var dom = CQ.Create(html);
                var session = dom["input"].First(e => e.Attributes["name"] != null &&
                                                      e.Attributes["name"] == "session")["value"];
                var fkey = dom.GetInputValue("fkey");

                var req = GenerateRequest(Method.POST, "https://openid.stackexchange.com/account/prompt/submit");

                req = req.AddData("session", session);
                req = req.AddData("fkey", fkey);

                SendRequest(cookieKey, req);
            }
            else if (resUrl.StartsWith("https://openid.stackexchange.com/account/login"))
            {
                var dom = CQ.Create(html);
                var session = dom["input"].First(e => e.Attributes["name"] != null &&
                                                      e.Attributes["name"] == "session")["value"];
                var fkey = dom.GetInputValue("fkey");

                var endpoint = "https://openid.stackexchange.com/account/login/submit";
                var origin = "https://openid.stackexchange.com";
                var referrer = $"https://openid.stackexchange.com/account/login?session={session}";

                var req = GenerateRequest(Method.POST, endpoint, referrer, origin);

                req = req.AddData("email", accountEmail);
                req = req.AddData("password", accountPassword);
                req = req.AddData("session", session);
                req = req.AddData("fkey", fkey);

                var res = SendRequest(cookieKey, req);
            }
        }
    }
}
