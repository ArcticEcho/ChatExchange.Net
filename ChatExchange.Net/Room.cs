using System;
using System.Collections.Generic;
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
		/// This action is called whenever a new message is posted.
		/// </summary>
		public Action<Message> NewMessageEvent { get; set; }

		/// <summary>
		/// Messages posted by all users from when this object was first instantiated.
		/// </summary>
		public List<Message> AllMessages { get; private set; }

		/// <summary>
		/// All successfully posted messages by the currently logged in user.
		/// </summary>
		public List<Message> MyMessages { get; private set; }



		/// <summary>
		/// WARNING! This class is not yet fully implemented!
		/// </summary>
		/// <param name="host">The host domain of the room (e.g., meta.stackexchange.com).</param>
		/// <param name="ID">The room's identification number.</param>
		public Room(string host, int ID)
		{
			if (String.IsNullOrEmpty(host)) { throw new ArgumentException("'host' can not be null or empty.", "host"); }
			if (ID < 0) { throw new ArgumentOutOfRangeException("ID", "'ID' can not be negative."); }

			Host = host;
			chatRoot = "http://chat." + Host;

			SetFkey();

			var eventTime = Join();

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
		/// Posts a new message in the room.
		/// </summary>
		/// <param name="message">The text to post.</param>
		/// <returns>Returns a new Message object if the message was successfully posted, otherwise null.</returns>
		public Message PostMessage(string message)
		{
			var data = "text=" + message + "&fkey=" + fkey; // TODO: Encode message.

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/new", data);

			if (res == null) { return null; }

			var resContent = RequestManager.GetResponseContent(res);

			var messageID = (int)JObject.Parse(resContent)["id"];

			var m = new Message(message, messageID, Me.Name, Me.ID);

			MyMessages.Add(m);
			AllMessages.Add(m);

			return m;
		}

		public bool EditMessage(Message oldMessage, string newMessage)
		{
			var data = "text=" + newMessage + "&fkey=" + fkey; // TODO: Encode message.

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + oldMessage.ID, data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if edit was successful.

			return true;
		}

		public bool EditMessage(int messageID, string newMessage)
		{
			var data = "text=" + newMessage + "&fkey=" + fkey; // TODO: Encode message.

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + messageID, data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if edit was successful.

			return true;
		}

		public bool DeleteMessage(Message message)
		{
			var data = "&fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + message.ID + "/delete", data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if delete was successful.

			return true;
		}

		public bool DeleteMessage(int messageID)
		{
			var data = "&fkey=" + fkey;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/messages/" + messageID + "/delete", data);

			if (res == null) { return false; }

			var resContent = RequestManager.GetResponseContent(res);

			//TODO: Check if delete was successful.

			return true;
		}


		public void Dispose()
		{
			if (disposed) { return; }

			disposed = true;

			socket.Close();

			GC.SuppressFinalize(this);
		}

		public static bool operator ==(Room a, Room b)
		{
			if (ReferenceEquals(a, b)) { return true; }

			if ((object)a == null || (object)b == null) { return false; }

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



		private int Join()
		{
			var data = "since=0&mode=Messages&msgCount=100&fkey=" + fkey;

			RequestManager.CookiesToPass = null;

			var res = RequestManager.SendPOSTRequest(chatRoot + "/chats/" + ID + "/events", data);

			if (res == null) { throw new Exception("Could not join room " + ID + " on " + Host + ". Do you have an active internet conection?"); }

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

			RequestManager.CookiesToPass = cookies;

			var res = RequestManager.SendPOSTRequest("http://chat." + Host + "/ws-auth", data); // Returns "Oops" page, rather than requested data.

			if (res == null) { throw new Exception("Could not get WebSocket URL. Do you haven an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			return (string)JObject.Parse(resContent)["url"] + "?l=" + eventTime;
		}

		private void InitialiseSocket(string socketURL)
		{
			// TODO: Do we need to pass cookies?

			socket = new WebSocket(socketURL, "", null, null, "", chatRoot);

			socket.MessageReceived += (o, oo) =>
			{
				var messageData = oo.Message;

				// TODO: Finish parsing received message data.

				var content = (string)JObject.Parse(messageData)["content"];
				var id = (int)JObject.Parse(messageData)["???"];
				var authorName = (string)JObject.Parse(messageData)["???"];
				var authorID = (int)JObject.Parse(messageData)["???"];
				var parentID = (int)JObject.Parse(messageData)["???"];

				if (NewMessageEvent == null || authorID == Me.ID) { return; }

				NewMessageEvent(new Message(content, id, authorName, authorID, parentID));
			};
		}

		private void SetFkey()
		{
			var res = RequestManager.SendGETRequest("http://" + Host + "/users/login?returnurl = %%2f");

			if (res == null) { throw new Exception("Could not get fkey. Do you have an active internet connection?"); }

			var resContent = RequestManager.GetResponseContent(res);

			var fk = CQ.Create(resContent).GetFkey();

			if (String.IsNullOrEmpty(fk)) { throw new Exception("Could not get fkey. Have logged in with the correct credentials and have an active internet connection?"); }

			fkey = fk;
		}

		/// <summary>
		/// WARNING! This method has not yet been fully tested!
		/// </summary>
		/// <returns>Returns all cookies associated with the room's host.</returns>
		private CookieCollection GetSiteCookies()
		{
			var allCookies = RequestManager.GlobalCookies.GetCookies();
			var siteCookies = new CookieCollection();

			foreach (var cookie in allCookies)
			{
				if (cookie != null && cookie.Domain == Host)
				{
					allCookies.Add(cookie);
				}
			}

			return siteCookies;
		}
	}
}
