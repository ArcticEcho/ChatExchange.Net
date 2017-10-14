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
        private readonly string cookieKey;
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

            if (HasCookieKey(cookieKey))
            {
                throw new Exception("Cannot create multiple instances of the same user.");
            }

            AddCookieKey(cookieKey);

            Rooms = new ReadOnlyCollection<Room>(new List<Room>());
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
                    //TODO: Support chat.se.
                }
                else
                {
                    SiteLogin(host);
                }
            }

            var r = new Room(cookieKey, host, roomID, loadUsersAsync);
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
                RemoveCookieKey(cookieKey);
            }

            GC.SuppressFinalize(this);
        }



        private void SiteLogin(string host)
        {
            var fkeyHtml = SimpleGet($"https://{host}/users/login", cookieKey);

            if (string.IsNullOrWhiteSpace(fkeyHtml))
            {
                throw new Exception("Invalid response received while logging in. (Unable to get fkey.)");
            }

            var fkey = CQ.Create(fkeyHtml).GetInputValue("fkey");
            var req = new HttpReq
            {
                Endpoint = $"https://{host}/users/login",
                Method = HttpMethod.POST,
                CookieKey = cookieKey,
                Referrer = $"https://{host}/users/login",
                Origin = $"https://{host}",
                CookieFilter = new Func<Cookie, bool>(c =>
                {
                    return c.Name == "prov" || c.Name == "fkey";
                })
            };

            req.AddDataKVPair("fkey", fkey);
            req.AddDataKVPair("ssrc", "");
            req.AddDataKVPair("email", accountEmail);
            req.AddDataKVPair("password", accountPassword);
            req.AddDataKVPair("oauth_version", "");
            req.AddDataKVPair("oauth_server", "");
            req.AddDataKVPair("openid_username", "");
            req.AddDataKVPair("openid_identifier", "");

            var cookie = new Cookie
            {
                Name = "fkey",
                Value = fkey,
                Domain = host
            };

            AddCookie(cookieKey, cookie);

            var postRes = SendRequest(req);

            if (postRes == null || string.IsNullOrWhiteSpace(postRes.Data))
            {
                throw new Exception($"Invalid response received while logging into {host}. (#241)");
            }

            TryFetchUserID(host);
        }

        private void TryFetchUserID(string host)
        {
            var html = SimpleGet($"https://{host}/users/current", cookieKey);
            var dom = CQ.Create(html);
            var id = 0;

            foreach (var e in dom[".-actions a"])
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
    }
}
