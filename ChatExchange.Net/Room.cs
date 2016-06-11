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
using Jil;
using RestSharp;
using WebSocketSharp;
using static ChatExchangeDotNet.RequestManager;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides access to chat room functions, such as: message posting/editing/deleting/starring/pinning,
    /// user kick-muting/access level changing, basic message/user retrieval and the ability to subscribe to events.
    /// </summary>
    public class Room : IDisposable
    {
        private static readonly Regex findUsers = new Regex(@"id:\s?(\d+),\sname", Extensions.RegexOpts);
        private static readonly Regex getId = new Regex(@"id: (?<id>\d+)", Extensions.RegexOpts);
        private readonly ManualResetEvent throttleARE = new ManualResetEvent(false);
        private readonly ManualResetEvent pingableUsersSyncMre = new ManualResetEvent(false);
        private readonly ManualResetEvent wsRecMre = new ManualResetEvent(false);
        private readonly ActionExecutor actEx;
        private readonly Guid trackingToken;
        private readonly string proxyUrl;
        private readonly string proxyUsername;
        private readonly string proxyPassword;
        private readonly string chatRoot;
        private readonly string cookieKey;
        private bool dispose;
        private bool hasLeft;
        private string fkey;
        private KeyValuePair<string, DateTime> lastMsg;
        private WebSocket socket;
        private EventManager evMan;

        # region Public properties/indexer.

        /// <summary>
        /// If true, removes (@Username) mentions and the message
        /// reply prefix (:012345) from all messages. Default set to false.
        /// </summary>
        public bool StripMention { get; set; }

        /// <summary>
        /// If true, all Message instances will NOT initialise/update
        /// their StarCount and PinCount properties.
        /// (Setting to "true" can help increase performance.)
        /// Default set to false.
        /// </summary>
        public bool InitialisePrimaryContentOnly { get; set; } = false;

        /// <summary>
        /// A list of all users that are currently able to receive "ping"s.
        /// </summary>
        public HashSet<User> PingableUsers { get; private set; } = new HashSet<User>();

        /// <summary>
        /// A list of all users that are currently in the room.
        /// (Pawcrafted by ProgramFOX.)
        /// </summary>
        public HashSet<User> CurrentUsers { get; private set; } = new HashSet<User>();

        /// <summary>
        /// A list of users with the "room owner" privilege.
        /// </summary>
        public HashSet<User> RoomOwners { get; private set; } = new HashSet<User>();

        /// <summary>
        /// Returns the currently logged in user.
        /// </summary>
        public User Me { get; private set; }

        /// <summary>
        /// Returns an object containing meta data of the room.
        /// </summary>
        public RoomMetaInfo Meta { get; private set; }

        /// <summary>
        /// Provides a means to (dis)connect chat event listeners (Delegates).
        /// </summary>
        public EventManager EventManager => evMan;

        /// <summary>
        /// Retrieves a message from the room.
        /// </summary>
        /// <param name="messageID">The ID of the message to fetch.</param>
        /// <returns>The Message object associated with the specified ID.</returns>
        /// <exception cref="MessageNotFoundException">Thrown if the message could not be found.</exception>
        /// <exception cref="WebException">Thrown if an unexpected network issue was encountered.</exception>
        /// <exception cref="IndexOutOfRangeException">Thrown if the message ID was less than 0.</exception>
        public Message this[int messageID]
        {
            get
            {
                if (messageID < 0) throw new IndexOutOfRangeException();

                return GetMessage(messageID);
            }
        }

        # endregion.



        internal Room(string cookieKey, string host, int id, string proxyUrl, string proxyUsername, string proxyPassword, bool loadUsersAsync)
        {
            if (string.IsNullOrEmpty(cookieKey)) throw new ArgumentNullException("cookieKey"); 
            if (string.IsNullOrEmpty(host)) throw new ArgumentNullException("'host' must not be null or empty.", "host"); 
            if (id < 0) throw new ArgumentOutOfRangeException("id", "'id' must not be negative.");

            this.cookieKey = cookieKey;
            this.proxyUrl = proxyUrl;
            this.proxyUsername = proxyUsername;
            this.proxyPassword = proxyPassword;
            chatRoot = $"http://chat.{host}";

            evMan = new EventManager();
            actEx = new ActionExecutor(ref evMan);
            Meta = new RoomMetaInfo(host, id);
            evMan.TrackRoomMetaInfo(Meta);
            SetFkey();
            Me = GetMe();

            var count = GetGlobalEventCount();
            var url = GetSocketURL(count);

            InitialiseSocket(url);

            if (loadUsersAsync)
            {
                Task.Factory.StartNew(() =>
                {
                    InitialisePingableUsers();
                    InitialiseRoomOwners();
                    InitialiseCurrentUsers();
                });
            }
            else
            {
                InitialisePingableUsers();
                InitialiseRoomOwners();
                InitialiseCurrentUsers();
            }

            trackingToken = evMan.TrackRoom(this);

            Task.Factory.StartNew(() => WSRecovery());
            Task.Factory.StartNew(() => SyncPingableUsers());
        }

        ~Room()
        {
            Dispose();
        }



        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (GetHashCode() == obj.GetHashCode()) return true;

            return false;
        }

        public override int GetHashCode() => Meta?.ID ?? -1;

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString() => Meta?.Name ?? base.ToString();

        /// <summary>
        /// Releases all resources used by the current instance.
        /// </summary>
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

            throttleARE?.Set(); // Release any threads currently being throttled.
            throttleARE?.Dispose();

            pingableUsersSyncMre?.Set();
            pingableUsersSyncMre?.Dispose();

            wsRecMre?.Set();
            wsRecMre?.Dispose();

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
            hasLeft = true;

            var req = GenerateRequest(Method.POST, $"{chatRoot}/chats/leave/{Meta.ID}");
            req = req.AddData("quiet", "true"); // No idea what this param does.
            req = req.AddData("fkey", fkey);

            SendRequest(cookieKey, req);

            Dispose();
        }

        /// <summary>
        /// Retrieves a message from the room.
        /// </summary>
        /// <param name="messageID">The ID of the message to fetch.</param>
        /// <returns>A Message object representing the requested message.</returns>
        /// <exception cref="MessageNotFoundException">Thrown if the message could not be found.</exception>
        /// <exception cref="WebException">Thrown if an unexpected network issue is encountered.</exception>
        public Message GetMessage(int messageID)
        {
            string resContent;

            try
            {
                resContent = SendRequest(cookieKey, GenerateRequest(Method.GET, $"{chatRoot}/messages/{messageID}/history")).Content;
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
                throw new MessageNotFoundException();
            }

            var lastestDom = CQ.Create(resContent).Select(".monologue").Last();
            var authorID = int.Parse(lastestDom[".username a"].First().Attr("href").Split('/')[2]);
            var message = new Message(this, ref evMan, messageID, authorID);

            return message;
        }

        /// <summary>
        /// Fetches user data for the specified user ID.
        /// </summary>
        /// <param name="userID">The user ID to look up.</param>
        public User GetUser(int userID)
        {
            var u = PingableUsers.SingleOrDefault(x => x.ID == userID);

            if (u == null)
            {
                u = new User(Meta, userID, cookieKey);
                PingableUsers.Add(u);
            }

            evMan.TrackUser(u);

            return u;
        }

        #region Normal user chat commands.

        /// <summary>
        /// Posts a new message in the room.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <returns>
        /// A Message object representing the newly posted message
        /// (if successful), otherwise returns null.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the message is null or empty
        /// upon calling the argument's .ToString() method.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to post a message.
        /// </exception>
        public Message PostMessage(object message)
        {
            if (string.IsNullOrEmpty(message?.ToString()))
            {
                throw new ArgumentException("'message' cannot be null or return an empty/null string upon calling the object's .ToString() method.", nameof(message));
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (Me.Reputation < 20)
            {
                throw new InsufficientReputationException(20);
            }

            var ex = CheckDupeMsg(message.ToString());
            if (ex != null) throw ex;

            var action = new ChatAction(ActionType.PostMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/chats/{Meta.ID}/messages/new");

                    req = req.AddData("text", Uri.EscapeDataString(message.ToString()).Replace("%5Cn", "%0A"), false);
                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent) || dispose) return null;
                    if (HandleThrottling(resContent)) continue;

                    var json = JSON.Deserialize<Dictionary<string, object>>(resContent);
                    var messageID = -1;
                    if (json.ContainsKey("id"))
                    {
                        messageID = int.Parse(json["id"].ToString());
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
        /// Posts a new message in the room without parsing the
        /// action's response or throwing any exceptions.
        /// (Use this method for greater performance.)
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <returns>True if the message was successfully posted, otherwise false.</returns>
        public bool PostMessageLight(object message)
        {
            if (message == null || string.IsNullOrEmpty(message.ToString()) || dispose ||
                CheckDupeMsg(message.ToString()) != null || Me.Reputation < 20)
            {
                return false;
            }

            var action = new ChatAction(ActionType.PostMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/chats/{Meta.ID}/messages/new");

                    req = req.AddData("text", Uri.EscapeDataString(message.ToString()).Replace("%5Cn", "%0A"), false);
                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent) || dispose) return false;
                    if (HandleThrottling(resContent)) continue;

                    return JSON.Deserialize<Dictionary<string, object>>(resContent).ContainsKey("id");
                }

                return false;
            }));

            return (bool)actEx.ExecuteAction(action);
        }

        /// <summary>
        /// Posts a chat message in reply to another
        /// (this will "ping" the target message's author).
        /// </summary>
        /// <param name="targetMessageID">The ID of the message to reply to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>
        /// A Message object representing the newly posted message
        /// (if successful), otherwise returns null.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the message is null or empty
        /// upon calling the argument's .ToString() method.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the target message's ID is less than 0
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to post a message.
        /// </exception>
        public Message PostReply(int targetMessageID, object message)
        {
            if (string.IsNullOrEmpty(message?.ToString()))
            {
                throw new ArgumentException("'message' cannot be null or return an empty/null string upon calling the object's .ToString() method.", nameof(message));
            }
            if (targetMessageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetMessageID), "The message's ID must be more than 0.");
            }

            return PostMessage($":{targetMessageID} {message}");
        }

        /// <summary>
        /// Posts a chat message in reply to another
        /// (this will "ping" the target message's author).
        /// </summary>
        /// <param name="targetMessage">The message to reply to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>
        /// A Message object representing the newly posted message
        /// (if successful), otherwise returns null.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the message is null or empty
        /// upon calling the argument's .ToString() method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the target message is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to post a message.
        /// </exception>
        public Message PostReply(Message targetMessage, object message)
        {
            if (string.IsNullOrEmpty(message?.ToString()))
            {
                throw new ArgumentException("'message' cannot be null or return an empty/null string upon calling the object's .ToString() method.", nameof(message));
            }
            if (targetMessage == null)
            {
                throw new ArgumentNullException(nameof(targetMessage), "'targetMessage' cannot be null.");
            }

            return PostMessage($":{targetMessage.ID} {message}");
        }

        /// <summary>
        /// Posts a chat message in reply to another
        /// (this will "ping" the target message's author),
        /// without parsing the action's response or
        /// throwing any exceptions.
        /// (Use this method for greater performance.)
        /// </summary>
        /// <param name="targetMessageID">The ID of the message to reply to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>True if the message was successfully posted, otherwise false.</returns>
        public bool PostReplyLight(int targetMessageID, object message)
        {
            if (string.IsNullOrEmpty(message?.ToString()) || targetMessageID < 0)
            {
                return false;
            }

            return PostMessageLight($":{targetMessageID} {message}");
        }

        /// <summary>
        /// Posts a chat message in reply to another,
        /// this will "ping" the target message's author.
        /// (Use this method for greater performance.)
        /// </summary>
        /// <param name="targetMessage">The message to reply to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>True if the message was successfully posted, otherwise false.</returns>
        public bool PostReplyLight(Message targetMessage, object message)
        {
            if (string.IsNullOrEmpty(message?.ToString()) || (targetMessage?.ID ?? -1) < 0)
            {
                return false;
            }

            return PostMessageLight($":{targetMessage.ID} {message}");
        }

        /// <summary>
        /// Replaces the content of an existing chat message.
        /// (Users who aren't moderators have a 2 minute
        /// "grace period" which allows people to edit their messages,
        /// after which the message is "locked" and can not be changed.)
        /// </summary>
        /// <param name="message">A message to edit.</param>
        /// <param name="newContent">The new content of the message.</param>
        /// <returns>True if the message was successfully edited, otherwise false.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the new content is null or empty
        /// upon calling the argument's .ToString() method.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'message' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to edit a message.
        /// </exception>
        public bool EditMessage(Message message, object newContent)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return EditMessage(message.ID, newContent);
        }

        /// <summary>
        /// Replaces the content of an existing chat message.
        /// (Users who aren't moderators have a 2 minute
        /// "grace period" which allows people to edit their messages,
        /// after which the message is "locked" and can not be changed.)
        /// </summary>
        /// <param name="messageID">The ID of a message to edit.</param>
        /// <param name="newContent">The new content of the message.</param>
        /// <returns>
        /// True if the message was successfully edited, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the message is null or empty
        /// upon calling the argument's .ToString() method.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if the target message's ID is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to edit a message.
        /// </exception>
        public bool EditMessage(int messageID, object newContent)
        {
            if (string.IsNullOrEmpty(newContent?.ToString()))
            {
                throw new ArgumentException("'newContent' cannot be null or return an " +
                    "empty/null string upon calling .ToString().", nameof(newContent));
            }
            if (messageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (Me.Reputation < 20)
            {
                throw new InsufficientReputationException(20);
            }

            var action = new ChatAction(ActionType.EditMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/messages/{messageID}");

                    req = req.AddData("text", Uri.EscapeDataString(newContent.ToString()).Replace("%5Cn", "%0A"), false);
                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent) || dispose) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        /// <summary>
        /// Deletes the specified message.
        /// (Users who aren't moderators have a 2 minute
        /// "grace period" which allows people to delete their messages,
        /// after which the message is "locked" and can not be changed.)
        /// </summary>
        /// <param name="message">The message to delete.</param>
        /// <returns>
        /// True if the message was successfully deleted, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'message' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to delete a message.
        /// </exception>
        public bool DeleteMessage(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return DeleteMessage(message.ID);
        }

        /// <summary>
        /// Deletes the specified message.
        /// (Users who aren't moderators have a 2 minute
        /// "grace period" which allows people to delete their messages,
        /// after which the message is "locked" and can not be changed.)
        /// </summary>
        /// <param name="messageID">
        /// The unique identification number of the message to delete.
        /// </param>
        /// <returns>
        /// True if the message was successfully deleted, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'messageID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to delete a message.
        /// </exception>
        public bool DeleteMessage(int messageID)
        {
            if (messageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (Me.Reputation < 20)
            {
                throw new InsufficientReputationException(20);
            }

            var action = new ChatAction(ActionType.DeleteMessage, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/messages/{messageID}/delete");

                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent) || dispose) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        /// <summary>
        /// Stars or unstars a message.
        /// </summary>
        /// <param name="message">The message to star/unstar.</param>
        /// <returns>
        /// True if toggling the message's star was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException"> 
        /// Thrown if 'message' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to toggle stars on a message.
        /// </exception>
        public bool ToggleStar(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return ToggleStar(message.ID);
        }

        /// <summary>
        /// Stars or unstars a message.
        /// </summary>
        /// <param name="messageID">
        /// The unique identification number of the message to star/unstar.
        /// </param>
        /// <returns>
        /// True if toggling the message's star was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'messageID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientReputationException">
        /// Thrown if the current user does not have enough
        /// reputation (20) to toggle stars on a message.
        /// </exception>
        public bool ToggleStar(int messageID)
        {
            if (messageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (Me.Reputation < 20)
            {
                throw new InsufficientReputationException(20);
            }

            var action = new ChatAction(ActionType.ToggleMessageStar, new Func<object>(() =>
            {
                while (!dispose)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/messages/{messageID}/star");

                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent) || dispose) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }

                return false;
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        #endregion

        #region Owner chat commands.

        /// <summary>
        /// Removes all stars from a message.
        /// </summary>
        /// <param name="message">
        /// The message to un-star.
        /// </param>
        /// <returns>
        /// True if removing the message's stars was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'message' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool ClearMessageStars(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return ClearMessageStars(message.ID);
        }

        /// <summary>
        /// Removes all stars from a message.
        /// </summary>
        /// <param name="messageID">
        /// The unique identification number of the message to un-star.
        /// </param>
        /// <returns>
        /// True if removing the message's stars was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'messageID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool ClearMessageStars(int messageID)
        {
            if (messageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (!Me.IsMod && !Me.IsRoomOwner)
            {
                throw new InsufficientPermissionException(UserRoomAccess.Owner);
            }

            var action = new ChatAction(ActionType.ClearMessageStars, new Func<object>(() =>
            {
                while (true)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/messages/{messageID}/unstar");

                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        /// <summary>
        /// Pins a message to the top of the "starboard" for 14 days (unless manually unpinned).
        /// </summary>
        /// <param name="message">
        /// The message to pin.
        /// </param>
        /// <returns>
        /// True if pinning the message was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'message' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool TogglePin(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return TogglePin(message.ID);
        }

        /// <summary>
        /// Pins a message to the top of the "starboard" for 14 days (unless manually unpinned).
        /// </summary>
        /// <param name="messageID">
        /// The unique identification number of the message to pin.
        /// </param>
        /// <returns>
        /// True if pinning the message was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'messageID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool TogglePin(int messageID)
        {
            if (messageID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageID), "'messageID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (!Me.IsMod && !Me.IsRoomOwner)
            {
                throw new InsufficientPermissionException(UserRoomAccess.Owner);
            }

            var action = new ChatAction(ActionType.ToggleMessagePin, new Func<object>(() =>
            {
                while (true)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/messages/{messageID}/owner-star");

                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent == "\"ok\"";
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        /// <summary>
        /// Kicks a user out of the room for a certain period of time
        /// (the period increases with each successive kick).
        /// After kicking a user three times, a moderator will be informed.
        /// </summary>
        /// <param name="user">
        /// The user to kick.
        /// </param>
        /// <returns>
        /// True if kicking the user was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'user' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool KickMute(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return KickMute(user.ID);
        }

        /// <summary>
        /// Kicks a user out of the room for a certain period of time
        /// (the period increases with each successive kick).
        /// After kicking a user three times, a moderator will be informed.
        /// </summary>
        /// <param name="userID">
        /// The unique identification number of the user to kick.
        /// </param>
        /// <returns>
        /// True if kicking the user was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'userID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool KickMute(int userID)
        {
            if (userID < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(userID), "'userID' cannot be less than 0.");
            }
            if (dispose)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            if (!Me.IsMod && !Me.IsRoomOwner)
            {
                throw new InsufficientPermissionException(UserRoomAccess.Owner);
            }

            var action = new ChatAction(ActionType.KickMute, new Func<object>(() =>
            {
                while (true)
                {
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/rooms/kickmute/{Meta.ID}");

                    req = req.AddData("userID", userID);
                    req = req.AddData("fkey", fkey);

                    var resContent = SendRequest(cookieKey, req).Content;

                    if (string.IsNullOrEmpty(resContent)) return false;
                    if (HandleThrottling(resContent)) continue;

                    return resContent?.Contains("has been kicked");
                }
            }));

            return (bool?)actEx.ExecuteAction(action) ?? false;
        }

        /// <summary>
        /// Changes a users access level to the room.
        /// </summary>
        /// <param name="user">
        /// The user to grant a new permission level to.
        /// </param>
        /// <param name="access">
        /// The new access level to grant to the user.
        /// </param>
        /// <returns>
        /// True if changing the user's access level was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if 'user' is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
        public bool SetUserRoomAccess(UserRoomAccess access, User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return SetUserRoomAccess(access, user.ID);
        }

        /// <summary>
        /// Changes a users access level to the room.
        /// </summary>
        /// <param name="userID">
        /// The unique identification number of the user to grant a new permission level to.
        /// </param>
        /// <param name="access">
        /// The new access level to grant to the user.
        /// </param>
        /// <returns>
        /// True if changing the user's access level was successful, otherwise false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if 'userID' is less than 0.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the current instance has been disposed.
        /// </exception>
        /// <exception cref="InsufficientPermissionException">
        /// Thrown if the current user does not have enough permission to
        /// execute this action (must be a room owner or moderator).
        /// </exception>
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
                    var req = GenerateRequest(Method.POST, $"{chatRoot}/rooms/setuseraccess/{Meta.ID}");

                    req = req.AddData("aclUserId", userID);
                    req = req.AddData("fkey", fkey);

                    var data = $"fkey={fkey}&aclUserId={userID}&userAccess=";

                    switch (access)
                    {
                        case UserRoomAccess.Normal:
                        {
                            req = req.AddData("userAccess", "remove");
                            break;
                        }

                        case UserRoomAccess.ExplicitReadOnly:
                        {
                            req = req.AddData("userAccess", "read-only");
                            break;
                        }

                        case UserRoomAccess.ExplicitReadWrite:
                        {
                            req = req.AddData("userAccess", "read-write");
                            break;
                        }

                        case UserRoomAccess.Owner:
                        {
                            req = req.AddData("userAccess", "owner");
                            break;
                        }
                    }

                    var resContent = SendRequest(cookieKey, req).Content;

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

        private void SyncPingableUsers()
        {
            while (!dispose)
            {
                InitialisePingableUsers();
                pingableUsersSyncMre.WaitOne(TimeSpan.FromDays(1));
            }
        }

        private void InitialisePingableUsers()
        {
            var json = SendRequest(cookieKey, GenerateRequest(Method.GET, $"http://chat.{Meta.Host}/rooms/pingable/{Meta.ID}")).Content;

            if (string.IsNullOrEmpty(json)) throw new Exception("Unable to initialise PingableUsers.");

            var data = JSON.Deserialize<HashSet<List<object>>>(json);
            var users = new HashSet<User>();

            foreach (var id in data)
            {
                Thread.Sleep(1000);
                var userID = int.Parse(id[0].ToString());
                var user = new User(Meta, userID, true);
                evMan.TrackUser(user);
                users.Add(user);
            }

            PingableUsers = users;
        }

        private void InitialiseCurrentUsers()
        {
            var html = SendRequest(cookieKey, GenerateRequest(Method.GET, $"http://chat.{Meta.Host}/rooms/{Meta.ID}/")).Content;
            var doc = CQ.CreateDocument(html);
            var obj = doc.Select("script")[3];
            var ids = findUsers.Matches(obj.InnerText);

            var users = new HashSet<User>();
            foreach (Match id in ids)
            {
                var userID = -1;

                if (int.TryParse(id.Groups[1].Value, out userID))
                {
                    var u = PingableUsers.SingleOrDefault(x => x.ID == userID);
                    if (u == null)
                    {
                        Thread.Sleep(1000);
                        users.Add(GetUser(userID));
                    }
                    else
                    {
                        evMan.TrackUser(u);
                        users.Add(u);
                    }
                }
            }

            CurrentUsers = users;
        }

        private void InitialiseRoomOwners()
        {
            var dom = CQ.CreateFromUrl($"{chatRoot}/rooms/info/{Meta.ID}");
            var ros = new HashSet<User>();

            foreach (var user in dom["[id^=owner-user]"])
            {
                var id = -1;

                if (int.TryParse(new string(user.Id.Where(char.IsDigit).ToArray()), out id))
                {
                    var u = PingableUsers.SingleOrDefault(x => x.ID == id);
                    if (u == null)
                    {
                        Thread.Sleep(1000);
                        ros.Add(GetUser(id));
                    }
                    else
                    {
                        evMan.TrackUser(u);
                        ros.Add(u);
                    }
                }
            }

            RoomOwners = ros;
        }

        private User GetMe()
        {
            var html = SendRequest(cookieKey, GenerateRequest(Method.GET, $"{chatRoot}/chats/join/favorite")).Content;

            if (string.IsNullOrEmpty(html))
            {
                throw new Exception("Unable to fetch requested user data.");
            }

            var dom = CQ.Create(html);
            var e = dom[".topbar-menu-links a"][0];
            var id = int.Parse(e.Attributes["href"].Split('/')[2]);
            var user = new User(Meta, id, cookieKey);

            evMan.TrackUser(user);

            return user;
        }

        private void SetFkey()
        {
            var resContent = SendRequest(cookieKey, GenerateRequest(Method.GET, $"{chatRoot}/rooms/{Meta.ID}")).Content;

            var ex = new Exception("Could not get fkey.");

            if (string.IsNullOrEmpty(resContent)) throw ex;

            var fk = CQ.Create(resContent).GetInputValue("fkey");

            if (string.IsNullOrEmpty(fk)) throw ex;

            fkey = fk;
        }

        private int GetGlobalEventCount()
        {
            var req = GenerateRequest(Method.POST, $"{chatRoot}/chats/{Meta.ID}/events");
            req = req.AddData("mode", "events");
            req = req.AddData("msgCount", 0);
            req = req.AddData("fkey", fkey);

            var resContent = SendRequest(cookieKey, req).Content;

            if (string.IsNullOrEmpty(resContent))
            {
                throw new Exception($"Could not get 'eventtime' for room {Meta.ID} on {Meta.Host}.");
            }

            var timeObj = JSON.Deserialize<Dictionary<string, object>>(resContent)["time"];

            return int.Parse(timeObj.ToString());
        }

        private string GetSocketURL(int eventTime)
        {
            var data = $"roomid={Meta.ID}&fkey={fkey}";

            var req = GenerateRequest(Method.POST, $"{chatRoot}/ws-auth", $"{chatRoot}/rooms/{Meta.ID}", chatRoot);
            req = req.AddData("roomid", Meta.ID);
            req = req.AddData("fkey", fkey);

            var resContent = SendRequest(cookieKey, req).Content;

            if (string.IsNullOrEmpty(resContent)) throw new Exception("Could not get WebSocket URL.");

            var baseUrl = JSON.Deserialize<Dictionary<string, string>>(resContent)["url"];

            return baseUrl + "?l=" + eventTime;
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
                try
                {
                    if ((DateTime.UtcNow - lastData).TotalSeconds > 30)
                    {
                        SetFkey();
                        var count = GetGlobalEventCount();
                        var url = GetSocketURL(count);
                        InitialiseSocket(url);
                    }
                }
                catch (Exception ex)
                {
                    evMan.CallListeners(EventType.InternalException, false, ex);
                }

                wsRecMre.WaitOne(TimeSpan.FromSeconds(15));
            }
        }

        private void InitialiseSocket(string socketUrl)
        {
            if (socket != null) socket.Close(CloseStatusCode.Normal);

            socket = new WebSocket(socketUrl) { Origin = chatRoot };

            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                socket.SetProxy(proxyUrl, proxyUsername, proxyPassword);
            }

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

            socket.Connect();
        }

        private void HandleData(string json)
        {
            evMan.CallListeners(EventType.DataReceived, false, json);

            var obj = JSON.Deserialize<Dictionary<string, object>>(json);
            var r = JSON.Deserialize<Dictionary<string, object>>(obj["r" + Meta.ID].ToString());

            if (!r.ContainsKey("e") || r["e"] == null) return;

            var msgs = JSON.Deserialize<Dictionary<string, object>[]>(r["e"].ToString());

            foreach (var message in msgs)
            {
                var eventType = (EventType)int.Parse(message["event_type"].ToString());

                if (int.Parse(message["room_id"].ToString()) != Meta.ID) continue;

                evMan.HandleEvent(eventType, this, ref evMan, message);
            }
        }

        #endregion
    }
}
