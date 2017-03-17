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
using static ChatExchangeDotNet.RequestManager;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Represents a chat message.
    /// </summary>
    public class Message : IDisposable
    {
        private readonly Regex messageEdits = new Regex("<div class=\"message\"", Extensions.RegexOpts);
        private readonly EventManager evMan;
        private readonly Guid trackID;

        internal bool DisposeObject { get; private set; }

        /// <summary>
        /// The host domain of the chat message.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The room's ID in which the message resides.
        /// </summary>
        public int RoomID { get; private set; }

        /// <summary>
        /// The unique identification number of the message.
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// The ID of the message to which this message is replying to.
        /// Set to -1 if the message is not a reply.
        /// </summary>
        public int ParentID { get; private set; }

        /// <summary>
        /// The author of the message.
        /// </summary>
        public User Author { get; private set; }

        // Kept updated by the room's EventManager (hence the internal set).
        /// <summary>
        /// The content/body of the message.
        /// </summary>
        public string Content { get; internal set; }

        /// <summary>
        /// Indicates if the message is currently deleted.
        /// </summary>
        public bool IsDeleted { get; internal set; }

        /// <summary>
        /// The number of stars the message currently has.
        /// </summary>
        public int StarCount { get; internal set; }

        /// <summary>
        /// The number of pins the message currently has.
        /// </summary>
        public int PinCount { get; internal set; }

        /// <summary>
        /// The current number of edits this message has received.
        /// </summary>
        public int EditCount { get; internal set; }



        internal Message(Room room, ref EventManager eventManager, int messageID, int authorID)
        {
            if (room == null) throw new ArgumentException(nameof(room));
            if (messageID < 0) throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' can not be less than 0.");

            evMan = eventManager;
            Content = GetMessageText(room, messageID);

            if (Content == null)
            {
                throw new MessageNotFoundException();
            }

            if (Content.IsReply())
            {
                ParentID = int.Parse(Content.Substring(1, Content.IndexOf(' ')));
            }
            else
            {
                ParentID = -1;
            }

            if (room.StripMention)
            {
                Content = StripMentions(room, Content);
            }

            Host = room.Meta.Host;
            RoomID = room.Meta.ID;
            ID = messageID;
            Author = room.GetUser(authorID);

            trackID = eventManager.TrackMessage(this, room.InitialisePrimaryContentOnly);

            if (!room.InitialisePrimaryContentOnly)
            {
                var historyHtml = SimpleGet($"http://chat.{Host}/messages/{ID}/history");

                SetStarPinCount(historyHtml);
                EditCount = GetEditCount(historyHtml);
            }
        }

        ~Message()
        {
            Dispose();
        }


        /// <summary>
        /// Fetches the content of a message.
        /// </summary>
        /// <param name="room">
        /// The room in which the message resides.
        /// </param>
        /// <param name="messageID">
        /// The unique identification number of the message to fetch.
        /// </param>
        /// <returns>The content of the message.</returns>
        /// <exception cref="MessageNotFoundException">
        /// Thrown if the message cannot be found (a result of an incorrect ID, or deletion).
        /// </exception>
        public static string GetMessageText(Room room, int messageID)
        {
            try
            {
                var req = new HttpReq
                {
                    Endpoint = $"http://chat.{room.Meta.Host}/message/{messageID}?plain=true",
                    Method = HttpMethod.GET
                };

                var res = SendRequest(req);

                if (res.StatusCode != HttpStatusCode.OK) return null;

                var content = res.Data;

                if (string.IsNullOrWhiteSpace(res.Data)) return null;

                content = WebUtility.HtmlDecode(content);

                return content.Trim();
            }
            catch (WebException ex) when (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
            {
                // If the input is valid, we've probably hit a deleted message.

                return null;
            }
        }

        public static string StripMentions(Room room, string messageText)
        {
            if (messageText.IsReply())
            {
                messageText = Regex.Replace(messageText, @"^:\d+\s", "");
            }

            var ping = Regex.Match(messageText, @"@\S+");
            var stripped = messageText;

            while (ping.Success)
            {
                var pingedName = ping.Value.Remove(0, 1).ToLowerInvariant();
                var nameMatchesPing = false;
                var names = room.Usernames;

                for (var i = pingedName.Length; i > 2; i--)
                {
                    foreach (var name in names)
                    {
                        var n = name.Replace(" ", "").ToLowerInvariant();
                        if (n.Length >= i && pingedName == n.Substring(0, i))
                        {
                            nameMatchesPing = true;
                            break;
                        }
                    }

                    if (nameMatchesPing) break;
                }

                if (nameMatchesPing)
                {
                    stripped = stripped.Remove(ping.Index, ping.Length);
                }

                ping = ping.NextMatch();
            }

            return stripped.Trim();
        }

        public void Dispose()
        {
            if (DisposeObject) return;
            DisposeObject = true;

            evMan.UntrackObject(trackID);

            GC.SuppressFinalize(this);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetHashCode() == obj.GetHashCode()) return true;

            return false;
        }

        public override int GetHashCode() => ID;

        public override string ToString() => Content;



        private void SetStarPinCount(string html)
        {
            StarCount = GetStarPinCount(html, true);
            PinCount = GetStarPinCount(html, false);
        }

        private int GetEditCount(string html)
        {
            var msgs = messageEdits.Matches(html);

            return Math.Max((msgs?.Count ?? 0) - 2, 0);
        }

        private int GetStarPinCount(string html, bool stars)
        {
            var dom = CQ.Create(html)[stars ? ".stars" : ".owner-star"];
            var count = 0;

            if (dom != null && dom.Length != 0)
            {
                var c = dom[".times"]?.First()?.Text();

                if (!string.IsNullOrEmpty(c))
                {
                    count = int.Parse(c);
                }
                else
                {
                    count = 1;
                }
            }

            return count;
        }
    }
}
