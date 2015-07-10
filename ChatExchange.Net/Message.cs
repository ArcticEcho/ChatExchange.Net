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
using System.Net;
using System.Text.RegularExpressions;
using CsQuery;

namespace ChatExchangeDotNet
{
    public class Message
    {
        private Regex messageEdits = new Regex("<div class=\"message\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private bool stripMention;
        private string content;

        public int ID { get; private set; }
        public int ParentID { get; private set; }
        public User Author { get; private set; }
        public string Host { get; private set; }
        public int RoomID { get; private set; }

        public string Content
        {
            get
            {
                return content;
            }
        }

        public int StarCount
        {
            get
            {
                var resContent = RequestManager.SendGETRequest("", "http://chat." + Host + "/messages/" + ID + "/history");

                if (string.IsNullOrEmpty(resContent)) { return -1; }

                var dom = CQ.Create(resContent)[".stars"];
                var count = 0;

                if (dom != null && dom.Length != 0)
                {
                    if (dom[".times"] != null && !string.IsNullOrEmpty(dom[".times"].First().Text()))
                    {
                        count = int.Parse(dom[".times"].First().Text());
                    }
                    else
                    {
                        count = 1;
                    }
                }

                return count;
            }
        }

        public int PinCount
        {
            get
            {
                var resContent = RequestManager.SendGETRequest("", "http://chat." + Host + "/messages/" + ID + "/history");

                if (string.IsNullOrEmpty(resContent)) { return -1; }

                var dom = CQ.Create(resContent)[".owner-star"];
                var count = 0;

                if (dom != null && dom.Length != 0)
                {
                    if (dom[".times"] != null && !string.IsNullOrEmpty(dom[".times"].First().Text()))
                    {
                        count = int.Parse(dom[".times"].First().Text());
                    }
                    else
                    {
                        count = 1;
                    }
                }

                return count;
            }
        }

        public int EditCount
        {
            get
            {
                var resContent = RequestManager.SendGETRequest("", "http://chat." + Host + "/messages/" + ID + "/history");

                return messageEdits.Matches(resContent).Count - 2;
            }
        }



        public Message(string host, int roomID, int messageID, User author, bool stripMention = true, int parentID = -1)
        {
            EventManager temp = null;
            var ex = Initialise(host, roomID, messageID, author, stripMention, parentID, ref temp);

            if (ex != null)
            {
                throw ex;
            }
        }

        public Message(ref EventManager evMan, string host, int roomID, int messageID, User author, bool stripMention = true, int parentID = -1)
        {
            var ex = Initialise(host, roomID, messageID, author, stripMention, parentID, ref evMan);

            if (ex != null)
            {
                throw ex;
            }
        }



        public static string GetMessageContent(string host, int messageID, bool stripMention = true)
        {
            using (var res = RequestManager.SendGETRequestRaw("", "http://chat." + host + "/message/" + messageID + "?plain=true"))
            {
                if (res == null || res.StatusCode != HttpStatusCode.OK) { return null; }

                var content = RequestManager.GetResponseContent(res);

                return string.IsNullOrEmpty(content) ? null : WebUtility.HtmlDecode(stripMention ? content.StripMention() : content);
            }
        }

        public override int GetHashCode()
        {
            return ID;
        }

        public override string ToString()
        {
            return Content;
        }



        private Exception Initialise(string host, int roomID, int messageID, User author, bool stripMention, int parentID, ref EventManager evMan)
        {
            if (string.IsNullOrEmpty(host)) { return new ArgumentException("'host' can not be null or empty.", "host"); }
            if (messageID < 0) { return new ArgumentOutOfRangeException("messageID", "'ID' can not be less than 0."); }
            if (author == null) { return new ArgumentNullException("author"); }

            this.stripMention = stripMention;
            content = GetMessageContent(host, messageID, stripMention);
            Host = host;
            RoomID = roomID;
            ID = messageID;
            Author = author;
            ParentID = parentID;

            if (evMan != null)
            {
                evMan.ConnectListener(EventType.MessageEdited, new Action<Message>(editedMessage =>
                {
                    if (editedMessage.ID == ID)
                    {
                        content = editedMessage.Content;
                    }
                }));
            }

            return null;
        }
    }
}
