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





using Newtonsoft.Json.Linq;

namespace ChatExchangeDotNet
{
    public class User
    {
        public string Name { get; private set; }
        public int ID { get; private set; }
        public bool IsMod { get; private set; }
        public bool IsRoomOwner { get; private set; }
        public int Reputation { get; private set; }
        public int RoomID { get; private set; }
        public string Host { get; private set; }



        public User(string host, int roomID, int id)
        {
            ID = id;
            RoomID = roomID;
            Host = host;

            var res = RequestManager.SendPOSTRequest("", "http://chat." + host + "/user/info", "ids=" + id + "&roomid=" + roomID);

            if (res == null)
            {
                Reputation = -1;
            }
            else
            {
                var resContent = RequestManager.GetResponseContent(res);
                var json = JObject.Parse(resContent);
                var name = json["users"][0]["name"];
                var isMod = json["users"][0]["is_moderator"];
                var isOwner = json["users"][0]["is_owner"];
                var rep = json["users"][0]["reputation"];

                Name = name != null && name.Type == JTokenType.String ? (string)name : "";
                IsMod = isMod != null && isMod.Type == JTokenType.Boolean && (bool)isMod;
                IsRoomOwner = isOwner != null && isOwner.Type == JTokenType.Boolean && (bool)isOwner;
                Reputation = rep == null || rep.Type != JTokenType.Integer ? 1 : (int)rep;
            }
        }
    }
}
