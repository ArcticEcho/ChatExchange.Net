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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CsQuery;
using ServiceStack.Text;
using WebSocketSharp;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides access to chat room functions, such as, message posting/editing/deleting/starring,
    /// user kick-muting/access level changing, basic message/user retrieval and the ability to subscribe to events.
    /// </summary>
    public class Room : IDisposable
    {
        private static readonly Regex findUsers = new Regex(@"id:\s?(\d+),\sname", Extensions.RegexOpts);
        private static readonly Regex getId = new Regex(@"id: (?<id>\d+)", Extensions.RegexOpts);
        private readonly AutoResetEvent throttleARE = new AutoResetEvent(false);
        private readonly ManualResetEvent passWSRecMre = new ManualResetEvent(false);
        private readonly ManualResetEvent aggWSRecMre = new ManualResetEvent(false);
        private readonly ActionExecutor actEx;
        private readonly string chatRoot;
        private readonly string cookieKey;
        private bool dispose;
        private bool hasLeft;
        private string fkey;
        private KeyValuePair<string, DateTime> lastMsg;
        private TimeSpan socketRecTimeout;
        private WebSocket socket;
        private EventManager evMan;


        # region Public properties/indexer.

        /// <summary>
        /// If true, restarts the event listener
        /// WebSocket after a period of inactivity.
        /// Default set to true.
        /// </summary>
        public bool AggressiveWebSocketRecovery { get; set; } = true;

        /// <summary>
        /// If true, removes (@Username) mentions and the message
        /// reply prefix (:012345) from all messages. Default set to true.
        /// </summary>
        public bool StripMention { get; set; } = true;

        /// <summary>
        /// If true, all Message instances will NOT initialise/update
        /// their StarCount and PinCount properties.
        /// (Setting to "true" can help increase performance.)
        /// Default set to false.
        /// </summary>
        public bool InitialisePrimaryContentOnly { get; set; } = false;

        /// <summary>
        /// Specifies how long to attempt to recovery the
        /// WebSocket after the connection closed;
        /// after which, an error is passed to the InternalException
        /// event and the room self-destructs.
        /// (Default set to 15 minutes.)
        /// </summary>
        public TimeSpan WebSocketRecoveryTimeout
        {
            get { return socketRecTimeout; }

            set
            {
                if (value.TotalSeconds < 15)
                {
                    throw new ArgumentOutOfRangeException("value", "Must be more then 15 seconds.");
                }

                socketRecTimeout = value;
            }
        }

        /// <summary>
        /// The host domain of the room.
        /// </summary>
        public string Host { get; private set; }

        /// <summary>
        /// The identification number of the room.
        /// </summary>
        public int ID { get; private set; }

        /// <summary>
        /// Returns the currently logged in user.
        /// </summary>
        public User Me { get; private set; }

        // Get room name.
        //public string Name { get; private set; }

        // Get room desc.
        //public string Description { get; private set; }

        // Get room tags.
        //public string Tags { get; private set; }

        /// <summary>
        /// Provides a means to (dis)connect chat event listeners (Delegates).
        /// </summary>
        public EventManager EventManager => evMan;

        /// <summary>
        /// Gets the Message object associated with the specified message ID.
        /// </summary>
        /// <param name="messageID"></param>
        /// <returns>The Message object associated with the specified ID.</returns>
        public Message this[int messageID]
        {
            get
            {
                if (messageID < 0) throw new IndexOutOfRangeException();

                return GetMessage(messageID);
            }
        }

        # endregion.



        /// <summary>
        /// Provides access to chat room functions, such as, message posting/editing/deleting/starring,
        /// user kick-muting/access level changing, basic message/user retrieval and the ability to subscribe to events.
        /// </summary>
        /// <param name="host">The host domain of the room (e.g., meta.stackexchange.com).</param>
        /// <param name="ID">The room's identification number.</param>
        public Room(string cookieKey, string host, int ID)
        {
            if (string.IsNullOrEmpty(cookieKey)) throw new ArgumentNullException("cookieKey"); 
            if (string.IsNullOrEmpty(host)) throw new ArgumentNullException("'host' must not be null or empty.", "host"); 
            if (ID < 0) throw new ArgumentOutOfRangeException("ID", "'ID' must not be negative."); 

            this.ID = ID;
            this.cookieKey = cookieKey;
            evMan = new EventManager();
            actEx = new ActionExecutor(ref evMan);
            chatRoot = $"http://chat.{host}";
            socketRecTimeout = TimeSpan.FromMinutes(15);
            Host = host;
            Me = GetMe();

            SetFkey();

            var count = GetGlobalEventCount();
            var url = GetSocketURL(count);

            InitialiseSocket(url);

            Task.Factory.StartNew(() => WSRecovery());
        }

        ~Room()
        {
            Dispose();
        }



        public override int GetHashCode() => ID;

        //public override string ToString()
        //{
        //    return ""; // Return room name.
        //}

        public void Dispose()
        {
            if (dispose) return;
            dispose = true;

            if ((socket?.ReadyState ?? WebSocketState.Closed) == WebSocketState.Open)
            {
                try
                {
                    socket.Close(CloseStatusCode.Normal);
                }
                catch (Exception ex)
                {
                    evMan.CallListeners(EventType.InternalException, false, ex);
                }
            }

            if (throttleARE != null)
            {
                throttleARE.Set(); // Release any threads currently being throttled.
                throttleARE.Dispose();
            }

            if (passWSRecMre != null)
            {
                passWSRecMre.Set();
                passWSRecMre.Dispose();
            }

            if (aggWSRecMre != null)
            {
                aggWSRecMre.Set();
                aggWSRecMre.Dispose();
            }

            actEx?.Dispose();
            evMan?.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Leaves the room and then disposes it.
        /// </summary>
        public void Leave()
        {
            if (hasLeft) return;

            RequestManager.Post(cookieKey, $"{chatRoot}/chats/leave/{ID}", $"quiet=true&fkey={fkey}");

            hasLeft = true;

            Dispose();
        }

        /// <summary>
        /// Retrieves a message from the room.
        /// </summary>
        /// <param name="messageID">The ID of the message to fetch.</param>
        /// <returns>A Message object representing the requested message, or null if the message could not be found.</returns>
        public Message GetMessage(int messageID)
        {
            string resContent;

            try
            {
                resContent = RequestManager.Get(cookieKey, $"{chatRoot}/messages/{messageID}/history");
            }
            catch (WebException ex)
            {
                // If the input is valid, we've probably hit a deleted message.
                if (ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                {
                    throw new MessageNotFoundException();
                }
                else
                {
                    throw ex;
                }
            }

            if (string.IsNullOrEmpty(resContent))
            {
                throw new Exception($"Unable to fetch data for message {messageID}.");
            }

            var lastestDom = CQ.Create(resContent).Select(".monologue").Last();
            var content = Message.GetMessageContent(Host, messageID, StripMention);

            if (content == null) throw new MessageNotFoundException();

            var parentID = content.IsReply() ? int.Parse(content.Substring(1, content.IndexOf(' '))) : -1;
            var authorID = int.Parse(lastestDom[".username a"].First().Attr("href").Split('/')[2]);
            var author = GetUser(authorID);
            var message = new Message(this, ref evMan, messageID, author, parentID);

            return message;
        }

        /// <summary>
        /// Fetches user data for the specified user ID.
        /// </summary>
        /// <param name="userID">The user ID to look up.</param>
        public User GetUser(int userID)
        {
            var u = new User(Host, ID, userID, cookieKey);

            evMan.TrackUser(u);

            return u;
        }

        /// <summary>
        /// Fetches a list of all users that are currently able to receive "ping"s.
        /// </summary>
        public HashSet<User> GetPingableUsers()
        {
            var json = RequestManager.Get(cookieKey, $"http://chat.{Host}/rooms/pingable/{ID}");

            if (string.IsNullOrEmpty(json)) return null;

            var data = JsonSerializer.DeserializeFromString<HashSet<List<object>>>(json);
            var users = new HashSet<User>();

            foreach (var user in data)
            {
                var userID = int.Parse(user[0].ToString());
                users.Add(new User(Host, ID, userID, true));
            }

            return users;
        }

        /// <summary>
        /// Fetches a list of all users that are currently in the room.
        /// (Pawcrafted by ProgramFOX.)
        /// </summary>
        public HashSet<User> GetCurrentUsers()
        {
            var html = RequestManager.Get(cookieKey, $"http://chat.{Host}/rooms/{ID}/");
            var doc = CQ.CreateDocument(html);
            var obj = doc.Select("script")[3];
            var ids = findUsers.Matches(obj.InnerText);

            var users = new HashSet<User>();
            foreach (Match id in ids)
            {
                var userID = 0;

                if (int.TryParse(id.Groups[1].Value, out userID))
                {
                    users.Add(new User(Host, ID, userID, true));
                }
            }

            return users;
        }

        #region Normal user chat commands.

        /// <summary>
        /// Posts a new message in the room.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <returns>A Message object representing the newly posted message (if successful), otherwise returns null.</returns>
        public Message PostMessage(object message)
        {
            if (message == null || string.IsNullOrEmpty(message.ToString()))
            {
                throw new ArgumentException("'message' cannot be null or return an empty/null string upon calling .ToString().", "message");
            }
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot post message when you have left the room.");
            }
            if (Me.Reputation < 20)
            {
                throw new Exception("You must have at least 20 reputation to post a message.");
            }

            var ex = CheckDupeMsg(message.ToString());
            if (ex != null) throw ex;

            var action = new ChatAction(ActionType.PostMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var data = $"text={Uri.EscapeDataString(message.ToString()).Replace("%5Cn", "%0A")}&fkey={fkey}";
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/chats/{ID}/messages/new", data);

                    if (string.IsNullOrEmpty(resContent) || hasLeft) return null;
                    if (HandleThrottling(resContent)) continue;

                    var json = JsonObject.Parse(resContent);
                    var messageID = -1;
                    if (json.ContainsKey("id"))
                    {
                        messageID = json.Get<int>("id");
                    }
                    else
                    {
                        return null;
                    }

                    return this[messageID];
                }

                return null;
            }));

            return (Message)actEx.ExecuteAction(action);
        }

        /// <summary>
        /// Posts a new message in the room without: parsing the
        /// action's response or throwing any exceptions.
        /// (Benchmarks show an approximate performance increase of
        /// 3.5x-9.5x depending on network capabilities.)
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <returns>True if the message was successfully posted, otherwise false.</returns>
        public bool PostMessageFast(object message)
        {
            if (message == null || string.IsNullOrEmpty(message.ToString()) || hasLeft ||
                CheckDupeMsg(message.ToString()) != null || Me.Reputation < 20)
            {
                return false;
            }

            var action = new ChatAction(ActionType.PostMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var data = "text=" + Uri.EscapeDataString(message.ToString()).Replace("%5Cn", "%0A") + "&fkey=" + fkey;
                    var resContent = RequestManager.Post(cookieKey, chatRoot + "/chats/" + ID + "/messages/new", data);

                    if (string.IsNullOrEmpty(resContent) || hasLeft) return false;
                    if (HandleThrottling(resContent)) continue;

                    return JsonObject.Parse(resContent).ContainsKey("id");
                }

                return false;
            }));

            return (bool)actEx.ExecuteAction(action);
        }

        public Message PostReply(int targetMessageID, object message) =>
            PostMessage($":{targetMessageID} {message}");

        public Message PostReply(Message targetMessage, object message) =>
            PostMessage($":{targetMessage.ID} {message}");

        public bool PostReplyFast(int targatMessageID, object message) =>
            PostMessageFast($":{targatMessageID} {message}");

        public bool PostReplyFast(Message targatMessage, object message) =>
            PostMessageFast($":{targatMessage.ID} {message}");

        public bool EditMessage(Message oldMessage, object newMessage) =>
            EditMessage(oldMessage.ID, newMessage);

        public bool EditMessage(int messageID, object newMessage)
        {
            if (newMessage == null || string.IsNullOrEmpty(newMessage.ToString()))
            {
                throw new ArgumentException("'newMessage' cannot be null or return an " +
                    "empty/null string upon calling .ToString().", "newMessage");
            }
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot edit message when you have left the room.");
            }

            var action = new ChatAction(ActionType.EditMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var data = $"text={Uri.EscapeDataString(newMessage.ToString()).Replace("%5Cn", "%0A")}&fkey={fkey}";
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/messages/{messageID}", data);

                    if (string.IsNullOrEmpty(resContent) || hasLeft) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        public bool DeleteMessage(Message message) => DeleteMessage(message.ID);

        public bool DeleteMessage(int messageID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot delete message when you have left the room.");
            }

            var action = new ChatAction(ActionType.DeleteMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/messages/{messageID}/delete", $"fkey={fkey}");

                    if (string.IsNullOrEmpty(resContent) || hasLeft) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        public bool ToggleStar(Message message) => ToggleStar(message.ID);

        public bool ToggleStar(int messageID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot toggle message star when you have left the room.");
            }

            var action = new ChatAction(ActionType.ToggleMessageStar, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/messages/{messageID}/star", $"fkey={fkey}");

                    if (string.IsNullOrEmpty(resContent) || hasLeft) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        #endregion

        #region Owner chat commands.

        public bool ClearMessageStars(Message message) => ClearMessageStars(message.ID);

        public bool ClearMessageStars(int messageID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot clear message's stars when you have left the room.");
            }
            if (!Me.IsMod || !Me.IsRoomOwner)
            {
                throw new InvalidOperationException("Unable to clear message stars. You have " +
                    "insufficient privileges (must be a room owner or moderator).");
            }

            var action = new ChatAction(ActionType.ClearMessageStars, new Func<object>(() =>
            {
                while (true)
                {
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/messages/{messageID}/unstar", $"fkey={fkey}");

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        public bool TogglePin(Message message) => TogglePin(message.ID);

        public bool TogglePin(int messageID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot clear message's stars when you have left the room.");
            }
            if (!Me.IsMod || !Me.IsRoomOwner)
            {
                throw new InvalidOperationException("Unable to (un)pin a message. You have " +
                    "insufficient privileges (must be a room owner or moderator).");
            }

            var action = new ChatAction(ActionType.ToggleMessagePin, new Func<object>(() =>
            {
                while (true)
                {
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/messages/{messageID}/owner-star", $"fkey={fkey}");

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        public bool KickMute(User user)
        {
            return KickMute(user.ID);
        }

        public bool KickMute(int userID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot clear message's stars when you have left the room.");
            }
            if (!Me.IsMod || !Me.IsRoomOwner)
            {
                throw new InvalidOperationException("Unable to kick-mute user. You have " +
                    "insufficient privileges (must be a room owner or moderator).");
            }

            var action = new ChatAction(ActionType.KickMute, new Func<object>(() =>
            {
                while (true)
                {
                    var data = $"userID={userID}&fkey={fkey}";
                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/rooms/kickmute/{ID}", data);

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent?.Contains("has been kicked");
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        public bool SetUserRoomAccess(UserRoomAccess access, User user) =>
            SetUserRoomAccess(access, user.ID);

        public bool SetUserRoomAccess(UserRoomAccess access, int userID)
        {
            if (hasLeft)
            {
                throw new InvalidOperationException("Cannot clear message's stars when you have left the room.");
            }
            if (!Me.IsMod || !Me.IsRoomOwner)
            {
                throw new InvalidOperationException("Unable to change user's access level. You have " +
                    "insufficient privileges (must be a room owner or moderator).");
            }

            var action = new ChatAction(ActionType.KickMute, new Func<object>(() =>
            {
                while (true)
                {
                    var data = $"fkey={fkey}&aclUserId={userID}&userAccess=";

                    switch (access)
                    {
                        case UserRoomAccess.Normal:
                        {
                            data += "remove";
                            break;
                        }

                        case UserRoomAccess.ExplicitReadOnly:
                        {
                            data += "read-only";
                            break;
                        }

                        case UserRoomAccess.ExplicitReadWrite:
                        {
                            data += "read-write";
                            break;
                        }

                        case UserRoomAccess.Owner:
                        {
                            data += "owner";
                            break;
                        }
                    }

                    var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/rooms/setuseraccess/{ID}", data);

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return true;
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        #endregion



        private bool HandleThrottling(string res)
        {
            var msg = res.Trim().ToLowerInvariant();

            if (msg.StartsWith("you can perform this action again in") && !dispose)
            {
                var delay = new string(msg.Where(char.IsDigit).ToArray());
                throttleARE.WaitOne(int.Parse(delay) * 1000);

                return true;
            }

            return false;
        }

        private DuplicateMessageException CheckDupeMsg(string msg)
        {
            if (msg == lastMsg.Key && (DateTime.UtcNow - lastMsg.Value).TotalMinutes < 1)
            {
                return new DuplicateMessageException();
            }

            lastMsg = new KeyValuePair<string, DateTime>(msg, DateTime.UtcNow);

            return null;
        }

        #region Instantiation/event handling related methods.

        private User GetMe()
        {
            var html = RequestManager.Get(cookieKey, $"{chatRoot}/chats/join/favorite");

            if (string.IsNullOrEmpty(html))
            {
                throw new Exception("Unable to fetch requested user data.");
            }

            var dom = CQ.Create(html);
            var e = dom[".topbar-menu-links a"][0];
            var id = int.Parse(e.Attributes["href"].Split('/')[2]);

            return new User(Host, ID, id, cookieKey);
        }

        private void SetFkey()
        {
            var resContent = RequestManager.Get(cookieKey, $"{chatRoot}/rooms/{ID}");
            var ex = new Exception("Could not get fkey.");

            if (string.IsNullOrEmpty(resContent)) throw ex;

            var fk = CQ.Create(resContent).GetInputValue("fkey");

            if (string.IsNullOrEmpty(fk)) throw ex;

            fkey = fk;
        }

        private int GetGlobalEventCount()
        {
            var data = $"mode=Events&msgCount=0&fkey={fkey}";
            var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/chats/{ID}/events", data);

            if (string.IsNullOrEmpty(resContent))
            {
                throw new Exception($"Could not get 'eventtime' for room {ID} on {Host}.");
            }

            return JsonObject.Parse(resContent).Get<int>("time");
        }

        private string GetSocketURL(int eventTime)
        {
            var data = $"roomid={ID}&fkey={fkey}";
            var resContent = RequestManager.Post(cookieKey, $"{chatRoot}/ws-auth", data, $"{chatRoot}/rooms/{ID}", chatRoot);

            if (string.IsNullOrEmpty(resContent)) throw new Exception("Could not get WebSocket URL.");

            return JsonObject.Parse(resContent).Get<string>("url") + "?l=" + eventTime;
        }

        private void WSRecovery()
        {
            var lastData = DateTime.MaxValue;
            evMan.ConnectListener(EventType.DataReceived, new Action<string>(json =>
            {
                lastData = DateTime.UtcNow;
            }));

            while (!dispose)
            {
                if (AggressiveWebSocketRecovery && (DateTime.UtcNow - lastData).TotalSeconds > 30)
                {
                    SetFkey();
                    var count = GetGlobalEventCount();
                    var url = GetSocketURL(count);
                    InitialiseSocket(url);
                }

                aggWSRecMre.WaitOne(TimeSpan.FromSeconds(15));
            }
        }

        private void InitialiseSocket(string socketUrl)
        {
            if (socket != null) socket.Close(CloseStatusCode.Normal);

            socket = new WebSocket(socketUrl) { Origin = chatRoot };

            socket.OnMessage += (o, oo) =>
            {
                try
                {
                    HandleData(oo.Data);
                }
                catch (Exception ex)
                {
                    evMan.CallListeners(EventType.InternalException, false, ex);
                }
            };

            socket.OnError += (o, oo) => evMan.CallListeners(EventType.InternalException, false, oo.Exception);

            socket.OnClose += (o, oo) =>
            {
                // Beware, ugly code ahead...
                if (!AggressiveWebSocketRecovery && (!oo.WasClean || oo.Code != (ushort)CloseStatusCode.Normal))
                {
                    // The socket closed abnormally, probably best to restart it (at 15 second intervals).
                    for (var i = 0; i < socketRecTimeout.TotalMinutes * 4; i++)
                    {
                        if (dispose) return;

                        try
                        {
                            SetFkey();
                            var count = GetGlobalEventCount();
                            var url = GetSocketURL(count);
                            InitialiseSocket(url);
                            return;
                        }
                        catch (Exception ex)
                        {
                            evMan.CallListeners(EventType.InternalException, false, ex);
                        }

                        passWSRecMre.WaitOne(TimeSpan.FromSeconds(15));
                    }

                    // We failed to restart the socket; dispose of the object and log the error.
                    evMan.CallListeners(EventType.InternalException, false, new Exception("Could not restart WebSocket; now disposing this Room object."));
                    Dispose();
                }
            };

            socket.Connect();
        }

        private void HandleData(string json)
        {
            evMan.CallListeners(EventType.DataReceived, false, json);

            var obj = JsonObject.Parse(json);
            var data = obj.Get<Dictionary<string, List<Dictionary<string, object>>>>("r" + ID);

            if (!data.ContainsKey("e") || data["e"] == null) return;

            foreach (var message in data["e"])
            {
                var eventType = (EventType)int.Parse(message["event_type"].ToString());

                if (int.Parse(message["room_id"].ToString()) != ID) continue;

                evMan.HandleEvent(eventType, this, ref evMan, message);
            }
        }

        #endregion
    }
}
