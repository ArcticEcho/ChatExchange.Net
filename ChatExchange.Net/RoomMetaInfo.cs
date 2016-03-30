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
using CsQuery;
using ServiceStack.Text;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides meta data for a given room.
    /// </summary>
    public class RoomMetaInfo
    {
        private int totalMessageCount;

        /// <summary>
        /// Returns the host domain of the room.
        /// For example: meta.stackexchange.com.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// Returns the room's unique identification number.
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// Returns the name of the room.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Returns the description of the room.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Returns an array of tags with which the room is tagged with.
        /// </summary>
        public string[] Tags { get; internal set; }

        /// <summary>
        /// Returns the date of the first message posted in the room.
        /// </summary>
        public DateTime FirstMessage { get; private set; }

        /// <summary>
        /// Returns a DateTime of the latest message in the room.
        /// </summary>
        public DateTime LastMessage { get; internal set; }

        /// <summary>
        /// Returns the total number of messages posted in the room since its creation.
        /// (Excludes messages moved out of the room.)
        /// </summary>
        public int AllTimeMessages
        {
            get
            {
                return totalMessageCount;
            }
            internal set
            {
                if (totalMessageCount == -1) return;

                totalMessageCount = value;
            }
        }



        internal RoomMetaInfo(string host, int roomID)
        {
            Host = host;
            ID = roomID;

            string name, desc;
            string[] tags;
            var dom = GetRoomStringMeta(host, roomID, out name, out desc, out tags);

            Name = name;
            Description = desc;
            Tags = tags;

            var firstMsg = DateTime.MinValue;
            DateTime.TryParse(dom[".room-keycell"][0].ParentNode[1].InnerText, out firstMsg);
            FirstMessage = firstMsg;

            var req = RequestManager.GenerateRequest(RestSharp.Method.POST, $"http://chat.{host}/chats/{roomID}/events");
            req = req.AddData("mode", "messages");
            req = req.AddData("msgCount", 1);

            var jsonRes = RequestManager.SendRequest(req).Content;
            var json = JsonSerializer.DeserializeFromString<Dictionary<string, Dictionary<string, object>[]>>(jsonRes);
            var lastMsg = 0;
            int.TryParse(json["events"][0]["time_stamp"].ToString(), out lastMsg);
            LastMessage = new DateTime(1970, 1, 1).AddSeconds(lastMsg);

            var totalMsg = -1;
            int.TryParse(dom[".clear-both.room-message-count-xxl"][0].InnerHTML, out totalMsg);
            AllTimeMessages = totalMsg;
        }

        internal static CQ GetRoomStringMeta(string host, int id, out string name, out string description, out string[] tags)
        {
            var dom = CQ.CreateFromUrl($"http://chat.{host}/rooms/info/{id}");

            name = dom[".subheader h1"][0].InnerText;
            description = dom[".roomcard-xxl p"][0].InnerText;

            var ts = new List<string>();
            foreach (var t in dom[".tag"])
            {
                ts.Add(t.InnerText);
            }
            tags = ts.ToArray();

            return dom;
        }
    }
}
