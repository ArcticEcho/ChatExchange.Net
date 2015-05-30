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
using ServiceStack.Text;

namespace ChatExchangeDotNet
{
    public class User
    {
        public string Name { get; private set; }
        public int ID { get; private set; }
        public int Reputation { get; private set; }
        public bool IsPingable { get; private set; }
        public bool IsMod { get; private set; }
        public bool IsRoomOwner { get; private set; }
        public int RoomID { get; private set; }
        public string Host { get; private set; }



        public User(string host, int roomID, int userID, bool? isPingable = null)
        {
            FetchUserData(host, roomID, userID, isPingable);
        }



        public static bool CanPing(string host, int roomID, int userID)
        {
            var json = RequestManager.SendGETRequest("", "http://chat." + host + "/rooms/pingable/" + roomID);
            if (string.IsNullOrEmpty(json)) { return false; }
            var data = JsonSerializer.DeserializeFromString<HashSet<List<object>>>(json);

            foreach (var user in data)
            {
                if (int.Parse(user[0].ToString()) == userID)
                {
                    return true;
                }
            }

            return false;
        }

        public override int GetHashCode()
        {
            return ID;
        }

        public void InvalidateCache()
        {
            FetchUserData(Host, RoomID, ID, null);
        }



        private void FetchUserData(string host, int roomID, int userID, bool? isPingable)
        {
            ID = userID;
            RoomID = roomID;
            Host = host;

            var resContent = RequestManager.SendPOSTRequest("", "http://chat." + host + "/user/info", "ids=" + userID + "&roomid=" + roomID);

            if (!string.IsNullOrEmpty(resContent) && resContent.StartsWith("{\"users\":[{"))
            {
                var json = JsonObject.Parse(resContent);
                var data = json.Get<List<Dictionary<string, object>>>("users");

                if (data.Count != 0)
                {
                    Name = (string)data[0]["name"];
                    Reputation = int.Parse(data[0]["reputation"].ToString());
                    if (data[0].ContainsKey("is_moderator") && data[0]["is_moderator"] != null)
                    {
                        IsMod = bool.Parse(data[0]["is_moderator"].ToString());
                    }
                    if (data[0].ContainsKey("is_owner") && data[0]["is_owner"] != null)
                    {
                        IsRoomOwner = bool.Parse(data[0]["is_owner"].ToString());
                    }
                    IsPingable = isPingable ?? CanPing(host, roomID, userID);
                    return;
                }
            }

            throw new Exception("Unable to fetch data for user: " + userID);
        }
    }
}
