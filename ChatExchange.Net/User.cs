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
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static ChatExchangeDotNet.RequestManager;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides various pieces of information for a given chat user.
    /// </summary>
    public class User
    {
        private string cookieKey;

        /// <summary>
        /// Returns the user's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the user's unique identification number.
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// Returns the amount of reputation the user has.
        /// </summary>
        public int Reputation { get; private set; }

        /// <summary>
        /// Returns true if the user can be "pinged" from the room, otherwise false.
        /// </summary>
        public bool IsPingable { get; private set; }

        /// <summary>
        /// Returns true if the user is a moderator.
        /// </summary>
        public bool IsMod { get; private set; }

        /// <summary>
        /// Returns true if the user is an owner of the room, otherwise false.
        /// </summary>
        public bool IsRoomOwner { get; internal set; }

        /// <summary>
        /// The room's ID to which this data is valid.
        /// </summary>
        public int RoomID { get; private set; }

        /// <summary>
        /// The host domain to which this data is valid.
        /// </summary>
        public string Host { get; private set; }



        internal User(RoomMetaInfo meta, int userID)
        {
            var ex = FetchUserData(meta.Host, meta.ID, userID, null, null);

            if (ex != null)
            {
                throw ex;
            }
        }

        internal User(RoomMetaInfo meta, int userID, bool isPingable)
        {
            var ex = FetchUserData(meta.Host, meta.ID, userID, isPingable, null);

            if (ex != null)
            {
                throw ex;
            }
        }

        internal User(RoomMetaInfo meta, int userID, string cookieKey)
        {
            var ex = FetchUserData(meta.Host, meta.ID, userID, null, cookieKey);

            if (ex != null)
            {
                throw ex;
            }
        }



        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetHashCode() == obj.GetHashCode()) return true;

            return false;
        }

        public override int GetHashCode() => ID;

        public override string ToString() => Name;

        public void InvalidateCache() => FetchUserData(Host, RoomID, ID, null, cookieKey);

        /// <summary>
        /// Checks if a user exists.
        /// </summary>
        /// <param name="meta">The room meta required to check if the user exists.</param>
        /// <param name="userId">The ID of the user to check.</param>
        public static bool Exists(RoomMetaInfo meta, int userId)
        {
            if (meta == null) return false;

            return Exists(meta.Host, userId);
        }

        /// <summary>
        /// Checks if a user exists.
        /// </summary>
        /// <param name="host">The host to be check for the user.</param>
        /// <param name="userId">The ID of the user to check.</param>
        public static bool Exists(string host, int userId)
        {
            var req = new HttpReq
            {
                Endpoint = $"https://chat.{host}/users/{userId}",
                Method = HttpMethod.GET
            };

            var res = SendRequest(req);

            return res.StatusCode == HttpStatusCode.OK;
        }



        internal static bool CanPing(string cookieKey, string host, int roomID, int userID)
        {
            var json = SimpleGet($"https://chat.{host}/rooms/pingable/{roomID}", cookieKey);

            if (string.IsNullOrEmpty(json)) return false;

            var data = JsonConvert.DeserializeObject<HashSet<List<object>>>(json);

            foreach (var user in data)
            {
                if (int.Parse(user[0].ToString()) == userID)
                {
                    return true;
                }
            }

            return false;
        }



        private Exception FetchUserData(string host, int roomID, int userID, bool? isPingable, string cookieKey)
        {
            this.cookieKey = cookieKey;
            ID = userID;
            RoomID = roomID;
            Host = host;

            if (!Exists(host, userID)) throw new UserNotFoundException();

            var req = new HttpReq
            {
                Endpoint = "https://chat." + host + "/user/info",
                Method = HttpMethod.POST,
            };
            req.AddDataKVPair("ids", userID.ToString());
            req.AddDataKVPair("roomid", roomID.ToString());

            var resData = SendRequest(req).Data;

            if (!string.IsNullOrEmpty(resData) && resData.StartsWith("{\"users\":[{"))
            {
                dynamic data = JObject.Parse(resData);

                if (data.Count != 0)
                {
                    Name = data.users[0]["name"];
                    Reputation = data.users[0]["reputation"];
                    if (data.users[0].ContainsKey("is_moderator") && data.users[0]["is_moderator"] != null)
                    {
                        IsMod = data.users[0]["is_moderator"];
                    }
                    if (data.users[0].ContainsKey("is_owner") && data.users[0]["is_owner"] != null)
                    {
                        IsRoomOwner = data.users[0]["is_owner"];
                    }
                }

                IsPingable = isPingable ?? !string.IsNullOrEmpty(cookieKey) && CanPing(cookieKey, host, roomID, userID);

                return null;
            }

            return new Exception("Unable to fetch data for user: " + userID);
        }
    }
}
