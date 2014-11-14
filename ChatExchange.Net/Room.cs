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
		/// Called whenever a new message is posted. Returns the newly posted message.
		/// </summary>
		public Action<Message> NewMessageEvent { get; set; }

		/// <summary>
		/// Called whenever a message is edited. Returns the newly edited message.
		/// </summary>
		public Action<Message> MessageEditedEvent { get; set; }

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
			throw new NotImplementedException();

			//TODO: Finish off implementation.

			var res = RequestManager.SendGETRequest(chatRoot + "/messages/" + ID + "/history");

			if (res == null) { throw new Exception("Could not retrieve data of message " + ID + ". Do you have an active internet connection?"); }

			var histDom = CQ.Create(RequestManager.GetResponseContent(res));
			var lastestDom = CQ.Create(RequestManager.GetResponseContent(res)).Select(".monologue").First();

			/*
			 * 
			 * latest_content = str(latest_soup.select('.content')[0]).partition('>')[2].rpartition('<')[0].strip()
			 * 
			 */

			var authorName = lastestDom[".username a"].First().Text();
			var authorID = int.Parse(lastestDom[".username a"].First()["href"].Text().Split('/')[2]);

			var starCount = 0;

			if (lastestDom[".stars"] != null)
			{
				if (lastestDom[".stars"][".times"] != null && !String.IsNullOrEmpty(lastestDom[".stars"][".times"].First().Text()))
				{
					starCount = int.Parse(lastestDom[".stars"][".times"].First().Text());
				}
				else
				{
					starCount = 1;
				}
			}

			var pinCount = 0;

			foreach (var e in histDom["#content p"]/*.Where(e => e[".stars.owner-star"] == null)*/)
			{
				pinCount++;
			}

			return new Message("", ID, authorName, authorID, -1, starCount, pinCount);
		}

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

			var m = new Message(message, messageID, Me.Name, Me.ID);

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

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + messageID, data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if edit was successful.

			return true;
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
			var data = "&fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + messageID + "/delete", data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if delete was successful.

			return true;
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

			if (res == null) { return false; }

			//TODO: Check res if starring was successfully.

			return true;
		}

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
			var data = "fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/messages/" + messageID + "/owner-star", data);

			if (res == null) { return false; }

			//TODO: Check res if pinning was successfully.

			return true;
		}


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



		private int GetEventTime()
		{
			var data = "since=0&mode=Events&msgCount=1&fkey=" + fkey; /*fkey={fkey}&r{room id}={time stap}*/

			RequestManager.CookiesToPass = null;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/events", data);

			if (res == null) { throw new Exception("Could not get eventtime for room " + ID + " on " + Host + ". Do you have an active internet conection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			return (int)JObject.Parse(resContent)["time"];
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		/// <param name="eventTime"></param>
		/// <returns></returns>
		private string GetSocketURL(int eventTime)
		{
			var data = "roomid=" + ID + "&fkey=" + fkey;

			var cookies = new CookieContainer();

			cookies.Add(GetSiteCookies());

			RequestManager.CookiesToPass = /*RequestManager.GlobalCookies;*/cookies;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/ws-auth", data/*, true, chatRoot + "/rooms/" + ID, chatRoot*/); // Returns "Oops" page, rather than requested data.

			if (res == null) { throw new Exception("Could not get WebSocket URL. Do you haven an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			return (string)JObject.Parse(resContent)["url"] + "?l=" + eventTime;
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		/// <param name="socketURL"></param>
		private void InitialiseSocket(string socketURL)
		{
			// TODO: Do we need to pass cookies?

			socket = new WebSocket(socketURL, "", null, null, "", chatRoot);

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
			var isMod = User.IsModerator(Host, id);

			return new User(name, id, isMod);
		}

		private void SetFkey()
		{
			var res = RequestManager.SendGETRequest("http://" + Host + "/users/login?returnurl = %%2f");

			if (res == null) { throw new Exception("Could not get fkey. Do you have an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			var fk = CQ.Create(resContent).GetFkey();

			if (String.IsNullOrEmpty(fk)) { throw new Exception("Could not get fkey. Have Do you have an active internet connection?"); }

			fkey = fk;
		}

		private CookieCollection GetSiteCookies()
		{
			var allCookies = RequestManager.GlobalCookies.GetCookies();
			var siteCookies = new CookieCollection();

			foreach (var cookie in allCookies)
			{
				var cookieDomain = cookie.Domain.StartsWith(".") ? cookie.Domain.Substring(1) : cookie.Domain;

				if ((cookieDomain == Host && cookie.Name.ToLowerInvariant().Contains("usr")) || cookie.Name == "csr" || cookie.Name == "sl")
				{
					siteCookies.Add(cookie);
				}
			}

			return siteCookies;
		}

		# region Incoming message handling methods.

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		private void HandleData(JObject json)
		{
			var eventType = (EventType)(int)(json["event_type"] ?? 0);

			if ((int)(json["roomid"] ?? -1) != ID) { return; }

			switch (eventType)
			{
				case EventType.MessagePosted | EventType.MessageReply | EventType.UserMentioned:
				{
					HandleNewMessage(json);

					break;
				}

				case EventType.MessageEdited:
				{
					HandleEdit(json);

					break;
				}

				case EventType.UserEntered:
				{
					HandleUserJoin(json);

					break;
				}

				case EventType.UserLeft:
				{
					HandleUserLeave(json);

					break;
				}
			}
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleNewMessage(JObject json)
		{
			var content = (string)json["content"];
			var id = (int)json["message_id"];
			var authorName = (string)json["user_name"];
			var authorID = (int)json["user_id"];
			var parentID = (int)(json["parent_id"] ?? -1);

			var message = new Message(content, id, authorName, authorID, parentID);

			AllMessages.Add(message);

			if (NewMessageEvent == null || authorID == Me.ID) { return; }

			NewMessageEvent(message);
		}

		/// <summary>
		/// WARNING! This method has noy yet been fully tested!
		/// </summary>
		private void HandleEdit(JObject json)
		{
			var content = (string)json["content"];
			var id = (int)json["message_id"];
			var authorName = (string)json["user_name"];
			var authorID = (int)json["user_id"];
			var parentID = (int)(json["parent_id"] ?? -1); //TODO: Does this even exist?

			var message = new Message(content, id, authorName, authorID, parentID);

			AllMessages.Remove(this[id]);
			AllMessages.Add(message);

			if (MessageEditedEvent == null || authorID == Me.ID) { return; }

			MessageEditedEvent(message);
		}

		private void HandleUserJoin(JObject json)
		{
			throw new NotImplementedException();

			var userName = (string)json["user_name"];
			var userID = (int)json["user_id"];
		}

		private void HandleUserLeave(JObject json)
		{
			throw new NotImplementedException();

			var userName = (string)json["user_name"];
			var userID = (int)json["user_id"];


		}

		# endregion
	}
}
