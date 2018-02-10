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
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using static ChatExchangeDotNet.RequestManager;

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


		//TODO: Finish fixing this crap.
        internal RoomMetaInfo(string host, int roomID)
        {
   //         var html = SimpleGet($"http://chat.{host}/rooms/info/{roomID}");
			//var dom = new HtmlParser().Parse(html);

   //         Host = host;
   //         ID = roomID;

   //         GetRoomStringMeta(dom, host, roomID, out var name, out var desc, out var tags);

   //         Name = name;
   //         Description = desc;
   //         Tags = tags;

   //         var firstMsg = DateTime.MinValue;
   //         DateTime.TryParse(dom[".room-keycell"][0].ParentNode[1].InnerText, out firstMsg);
   //         FirstMessage = firstMsg;

   //         var req = new HttpReq
   //         {
   //             Endpoint = $"http://chat.{host}/chats/{roomID}/events",
   //             Method = HttpMethod.POST
   //         };
   //         req.AddDataKVPair("mode", "messages");
   //         req.AddDataKVPair("msgCount", "1");

   //         var jsonRes = SendRequest(req).Data;
   //         var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonRes);
   //         var events = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(json["events"].ToString());
   //         int.TryParse(events[0]["time_stamp"].ToString(), out var lastMsg);
   //         LastMessage = new DateTime(1970, 1, 1).AddSeconds(lastMsg);

   //         var totalMsg = -1;
   //         int.TryParse(dom[".clear-both.room-message-count-xxl"][0].InnerHTML, out totalMsg);
   //         AllTimeMessages = totalMsg;
        }

        //internal static void GetRoomStringMeta(IHtmlDocument dom, string host, int id, out string name, out string description, out string[] tags)
        //{
        //    name = WebUtility.HtmlDecode(dom[".subheader h1"][0].InnerText);
        //    description = WebUtility.HtmlDecode(dom[".roomcard-xxl p"][0].InnerText);

        //    var ts = new List<string>();
        //    foreach (var t in dom[".tag"])
        //    {
        //        ts.Add(WebUtility.HtmlDecode(t.InnerText));
        //    }
        //    tags = ts.ToArray();

        //    return dom;
        //}
    }
}
