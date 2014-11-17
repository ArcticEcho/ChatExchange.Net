using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CsQuery;
using Newtonsoft.Json.Linq;
using WebSocket4Net;



namespace ChatExchangeDotNet
{
	public class Room : IDisposable
	{
		private bool disposed;
		private WebSocket socket;
		private readonly string chatRoot;
		private string fkey;

		/// <param name="newMessage">The newly posted message.</param>
		public delegate void NewMessageEventHandler(Message newMessage);

		/// <param name="oldMessage">The previous state of the message.</param>
		/// <param name="newMessage">The current state of the message.</param>
		public delegate void MessageEditedEventHandler(Message oldMessage, Message newMessage);

		/// <param name="user">The user that has joined/entered the room.</param>
		public delegate void UserJoinEventHandler(User user);

		/// <param name="user">The user that has left the room.</param>
		public delegate void UserLeftEventHandler(User user);

		/// <summary>
		/// Occurs when a new message is posted. Returns the newly posted message.
		/// </summary>
		public event NewMessageEventHandler NewMessage;

		/// <summary>
		/// Occurs when a message is edited.
		/// </summary>
		public event MessageEditedEventHandler MessageEdited;

		/// <summary>
		/// Occurs when a user joins/enters the room.
		/// </summary>
		public event UserJoinEventHandler UserJoind;

		/// <summary>
		/// Occurs when a user leaves the room.
		/// </summary>
		public event UserLeftEventHandler UserLeft;



		/// <summary>
		/// Gets/sets the currently used MessageParingOption.
		/// </summary>
		public MessageParsingOption ParsingOption { get; set; }

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

		/// <summary>
		/// Messages posted by all users from when this object was first instantiated.
		/// </summary>
		public List<Message> AllMessages { get; private set; }

		/// <summary>
		/// All successfully posted messages by the currently logged in user.
		/// </summary>
		public List<Message> MyMessages { get; private set; }

		/// <summary>
		/// WARNING! This property has not yet been fully tested! Gets the Message object associated with the specified message ID.
		/// </summary>
		/// <param name="messageID"></param>
		/// <returns>The Message object associated with the specified ID.</returns>
		public Message this[int messageID]
		{
			get
			{
				if (messageID < 0) { throw new IndexOutOfRangeException(); }

				if (AllMessages.Any(m => m.ID == messageID))
				{
					return AllMessages.First(m => m.ID == messageID);
				}

				var message = GetMessage(messageID);

				AllMessages.Add(message);

				return message;		
			}
		}



		/// <summary>
		/// WARNING! This class is not yet fully implemented/tested!
		/// </summary>
		/// <param name="host">The host domain of the room (e.g., meta.stackexchange.com).</param>
		/// <param name="ID">The room's identification number.</param>
		/// <param name="parsingOption">The MessageParsingOption to be used when parsing received messages.</param>
		public Room(string host, int ID, MessageParsingOption parsingOption = MessageParsingOption.StripMarkdown)
		{
			if (String.IsNullOrEmpty(host)) { throw new ArgumentException("'host' can not be null or empty.", "host"); }
			if (ID < 0) { throw new ArgumentOutOfRangeException("ID", "'ID' can not be negative."); }

			this.ID = ID;
			Host = host;
			ParsingOption = parsingOption;
			AllMessages = new List<Message>();
			MyMessages = new List<Message>();
			chatRoot = "http://chat." + Host;
			Me = GetMe();

			SetFkey();

			//var testFkey = CQ.Create(RequestManager.GetResponseContent(RequestManager.SendGETRequest(chatRoot + "/rooms/" + ID))).GetFkey();

			var eventTime = GetEventTime();

			var url = GetSocketURL(eventTime);

			InitialiseSocket(url);
		}

		~Room()
		{
			if (socket != null && !disposed)
			{
				socket.Close();
			}
		}



		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		/// <param name="ID"></param>
		/// <returns></returns>
		public Message GetMessage(int ID)
		{
			var res = RequestManager.SendGETRequest(chatRoot + "/messages/" + ID + "/history");

			if (res == null) { throw new Exception("Could not retrieve data of message " + ID + ". Do you have an active internet connection?"); }

			var lastestDom = CQ.Create(RequestManager.GetResponseContent(res)).Select(".monologue").Last();
			
			var contentData = lastestDom[".content"].ToList();

			var content = contentData.Count == 2 ? contentData.Last().InnerText : contentData[1].InnerText;
			content = content.Substring(22, content.Length - 55);

			var parentID = content.StartsWith(":") ? int.Parse(content.Substring(1, content.IndexOf(' '))) : -1;
			var authorName = lastestDom[".username a"].First().Text();
			var authorID = int.Parse(lastestDom[".username a"].First().Attr("href").Split('/')[2]);

			return new Message(Host, content, ID, authorName, authorID, parentID);
		}

		# region Normal user chat commands.

		/// <summary>
		/// Posts a new message in the room.
		/// </summary>
		/// <param name="message">The text to post.</param>
		/// <returns>Returns a new Message object if the message was successfully posted, otherwise null.</returns>
		public Message PostMessage(string message)
		{
			var data = "text=" + message + "&fkey=" + fkey; // TODO: Encode message.

			RequestManager.CookiesToPass = RequestManager.GlobalCookies;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/new", data);

			if (res == null) { return null; }

			var resContent = RequestManager.GetResponseContent(res);

			var messageID = (int)JObject.Parse(resContent)["id"];

			var m = new Message(Host, message, messageID, Me.Name, Me.ID);

			MyMessages.Add(m);
			AllMessages.Add(m);

			return m;
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool EditMessage(Message oldMessage, string newMessage)
		{
			return EditMessage(oldMessage.ID, newMessage);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool EditMessage(int messageID, string newMessage)
		{
			var data = "text=" + newMessage + "&fkey=" + fkey; // TODO: Encode message.

			var res = RequestManager.SendPOSTRequest(chatRoot + "/messages/" + messageID, data);

			return res != null && RequestManager.GetResponseContent(res) == "ok";
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool DeleteMessage(Message message)
		{
			return DeleteMessage(message.ID);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool DeleteMessage(int messageID)
		{
			var data = "fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/messages/" + messageID + "/delete", data);

			return res != null && RequestManager.GetResponseContent(res) != "ok";
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool ToggleStarring(Message message)
		{
			return ToggleStarring(message.ID);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool ToggleStarring(int messageID)
		{
			var data = "fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/messages/" + messageID + "/star", data);

			return res != null /*&& RequestManager.GetResponseContent(res) != "ok"*/;
		}

		

		# endregion

		#region Owner chat commands.

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool TogglePinning(Message message)
		{
			return TogglePinning(message.ID);
		}
		
		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool TogglePinning(int messageID)
		{
			if (!Me.IsMod && !Me.IsRoomOwner) { return false; }

			var data = "fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/messages/" + messageID + "/owner-star", data);

			if (res == null) { return false; }

			//TODO: Check res if pinning was successfully.

			return true;
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool KickMute(User user)
		{
			return KickMute(user.ID);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool KickMute(int userID)
		{
			if (!Me.IsMod && !Me.IsRoomOwner) { return false; }

			var data = "userID=" + userID + "&fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/rooms/kickute/" + ID, data);

			return res != null && RequestManager.GetResponseContent(res).Contains("The user has been kicked and cannot return");
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool SetUserRoomAccess(UserRoomAccess access, User user)
		{
			return SetUserRoomAccess(access, user.ID);
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		public bool SetUserRoomAccess(UserRoomAccess access, int userID)
		{
			if (!Me.IsMod && !Me.IsRoomOwner) { return false; }

			var data = "fkey=" + fkey + "&aclUserId=" + userID + "&userAccess=";

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

			var res = RequestManager.SendPOSTRequest(chatRoot + "/rooms/setuseraccess/" + ID, data);

			return res != null;
		}

		#endregion

		#region Inherited methods.

		public void Dispose()
		{
			if (disposed) { return; }

			socket.Close();

			GC.SuppressFinalize(this);

			disposed = true;
		}

		public static bool operator ==(Room a, Room b)
		{
			if ((object)a == null || (object)b == null) { return false; }

			if (ReferenceEquals(a, b)) { return true; }

			return a.GetHashCode() == b.GetHashCode();
		}

		public static bool operator !=(Room a, Room b)
		{
			return !(a == b);
		}

		public bool Equals(Room room)
		{
			if (room == null) { return false; }

			return room.GetHashCode() == GetHashCode();
		}

		public bool Equals(string host, int id)
		{
			if (String.IsNullOrEmpty(host) || id < 0) { return false; }

			return String.Equals(host, Host, StringComparison.InvariantCultureIgnoreCase) && ID == id;
		}

		public override bool Equals(object obj)
		{
			if (obj == null) { return false; }

			if (!(obj is Room)) { return false; }

			return obj.GetHashCode() == GetHashCode();
		}

		public override int GetHashCode()
		{
			return Host.GetHashCode() + ID.GetHashCode();
		}

		#endregion



		private int GetEventTime()
		{
			var data = "mode=Events&msgCount=0&fkey=" + fkey;

			RequestManager.CookiesToPass = null;//GetSiteCookies();

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/events", data);

			if (res == null) { throw new Exception("Could not get eventtime for room " + ID + " on " + Host + ". Do you have an active internet conection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			return (int)JObject.Parse(resContent)["time"];
		}

		private string GetSocketURL(int eventTime)
		{
			var data = "roomid=" + ID + "&fkey=" + fkey;

			RequestManager.CookiesToPass = GetSiteCookies();

			var res = RequestManager.SendPOSTRequest(chatRoot + "/ws-auth", data, true, chatRoot + "/rooms/" + ID, chatRoot); // Returns "Oops" page, rather than requested data.

			if (res == null) { throw new Exception("Could not get WebSocket URL. Do you haven an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			return (string)JObject.Parse(resContent)["url"] + "?l=" + eventTime;
		}

		private void InitialiseSocket(string socketURL)
		{
			// TODO: Do we need to pass cookies?

			socket = new WebSocket(socketURL/*, "", null, null, "", chatRoot*/);

			socket.MessageReceived += (o, oo) =>
			{
				var json = JObject.Parse(oo.Message);

				HandleData(json);
			};
		}

		private User GetMe()
		{
			var res = RequestManager.SendGETRequest(chatRoot + "/chats/join/favorite");

			if (res == null) { throw new Exception("Could not get user information. Do you have an active internet connection?"); }

			var dom = CQ.Create(RequestManager.GetResponseContent(res));

			var e = dom[".topbar-menu-links a"].First();

			var name = e.Text();
			var id = int.Parse(e.Attr("href").Split('/')[2]);

			return new User(name, id, ID, Host);
		}

		private void SetFkey()
		{
			var res = RequestManager.SendGETRequest(chatRoot + "/rooms/" + ID);

			if (res == null) { throw new Exception("Could not get fkey. Do you have an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			var fk = CQ.Create(resContent).GetFkey();

			if (String.IsNullOrEmpty(fk)) { throw new Exception("Could not get fkey. Have Do you have an active internet connection?"); }

			fkey = fk;
		}

		private CookieContainer GetSiteCookies()
		{
			var allCookies = RequestManager.GlobalCookies.GetCookies();
			var siteCookies = new CookieCollection();

			foreach (var cookie in allCookies)
			{
				var cookieDomain = cookie.Domain.StartsWith(".") ? cookie.Domain.Substring(1) : cookie.Domain;

				if ((cookieDomain == Host && cookie.Name.ToLowerInvariant().Contains("usr")) || cookie.Name == "csr")
				{
					siteCookies.Add(cookie);
				}
			}

			var cookies = new CookieContainer();

			cookies.Add(siteCookies);

			return cookies;
		}

		# region Incoming message handling methods.

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		private void HandleData(JObject json)
		{
			var data = json["r" + ID]["e"][0];

			if (data.Type == JTokenType.Null) { return; }

			var eventType = (EventType)(int)(data["event_type"]);

			if ((int)(data["roomid"].Type != JTokenType.Integer ? -1 : data["roomid"]) != ID) { return; }

			switch (eventType)
			{
				case EventType.MessagePosted:
				{
					HandleNewMessage(data);

					break;
				}

				case EventType.MessageReply:
				{
					HandleNewMessage(data);

					break;
				}

				case EventType.UserMentioned:
				{
					HandleNewMessage(data);

					break;
				}

				case EventType.MessageEdited:
				{
					HandleEdit(data);

					break;
				}

				case EventType.UserEntered:
				{
					HandleUserJoin(data);

					break;
				}

				case EventType.UserLeft:
				{
					HandleUserLeave(data);

					break;
				}
			}
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleNewMessage(JToken json)
		{
			var content = (string)json["content"];
			var id = (int)json["message_id"];
			var authorName = (string)json["user_name"];
			var authorID = (int)json["user_id"];
			var parentID = (int)(json["parent_id"].Type == JTokenType.Null ? -1 : json["parent_id"]);

			var message = new Message(Host, content, id, authorName, authorID, parentID);

			AllMessages.Add(message);

			if (NewMessage == null || authorID == Me.ID) { return; }

			NewMessage(message);
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleEdit(JToken json)
		{
			var content = (string)json["content"];
			var id = (int)json["message_id"];
			var authorName = (string)json["user_name"];
			var authorID = (int)json["user_id"];
			var parentID = (int)(json["parent_id"].Type == JTokenType.Null ? -1 : json["parent_id"]);

			var currentMessage = new Message(Host, content, id, authorName, authorID, parentID);
			var oldMessage = this[id];

			AllMessages.Remove(oldMessage);
			AllMessages.Add(currentMessage);

			if (MessageEdited == null || authorID == Me.ID) { return; }

			MessageEdited(oldMessage, currentMessage);
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleUserJoin(JToken json)
		{
			var userName = (string)json["user_name"];
			var userID = (int)json["user_id"];

			var user = new User(userName, userID, ID, Host);

			if (UserJoind == null || userID == Me.ID) { return; }

			UserJoind(user);
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleUserLeave(JToken json)
		{
			var userName = (string)json["user_name"];
			var userID = (int)json["user_id"];

			var user = new User(userName, userID, ID, Host);

			if (UserLeft == null || userID == Me.ID) { return; }

			UserLeft(user);
		}

		# endregion
	}
}
