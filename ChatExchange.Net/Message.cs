using System;
using CsQuery;



//TODO: Add functionality to reply to message.



namespace ChatExchangeDotNet
{
	public class Message
	{
		public string Content { get; private set; }
		public int ID { get; private set; }
		public string AuthorName { get; private set; }
		public int AuthorID { get; private set; }
		public int ParentID { get; private set; }
		public string Host { get; private set; }

		/// <summary>
		/// WARNING! This property has not yet been fully tested!
		/// </summary>
		public int StarCount
		{
			get
			{
				var res = RequestManager.SendGETRequest("http://chat." + Host + "/messages/" + ID + "/history");

				if (res == null) { return -1; }

				var dom = CQ.Create(RequestManager.GetResponseContent(res));
				var count = 0;

				if (dom[".stars"] != null)
				{
					if (dom[".stars"][".times"] != null && !String.IsNullOrEmpty(dom[".stars"][".times"].First().Text()))
					{
						count = int.Parse(dom[".stars"][".times"].First().Text());
					}
					else
					{
						count = 1;
					}
				}

				return count;
			}
		}

		/// <summary>
		/// WARNING! This property has not yet been fully tested!
		/// </summary>
		public int PinCount 
		{
			get
			{
				var res = RequestManager.SendGETRequest("http://chat." + Host + "/messages/" + ID + "/history");

				if (res == null) { return -1; }

				var dom = CQ.Create(RequestManager.GetResponseContent(res)).Select(".monologue").First();
				var count = 0;

				foreach (var e in dom["#content p"]/*.Where(e => e[".stars.owner-star"] == null)*/)
				{
					count++;
				}

				return count;		
			}
		}



		public Message(string host, string content, int ID, string authorName, int authorID, int parentID = -1)
		{
			if (String.IsNullOrEmpty(host)) { throw new ArgumentException("'host' can not be null or empty.", "host"); }
			if (String.IsNullOrEmpty(content)) { throw new ArgumentException("'content' can not be null or empty.", "content"); }
			if (ID < 0) { throw new ArgumentOutOfRangeException("ID", "'ID' can not be less than 0."); }
			if (String.IsNullOrEmpty(authorName)) { throw new ArgumentException("'authorName' can not be null or empty.", "authorName"); }
			if (authorID < -1) { throw new ArgumentOutOfRangeException("authorID", "'authorID' can not be less than -1."); }

			Host = host;
			Content = content;
			this.ID = ID;
			AuthorName = authorName;
			AuthorID = authorID;
			ParentID = parentID;
		}



		public static bool operator ==(Message a, Message b)
		{
			if ((object)a == null || (object)b == null) { return false; }

			if (ReferenceEquals(a, b)) { return true; }

			return a.GetHashCode() == b.GetHashCode();
		}

		public static bool operator !=(Message a, Message b)
		{
			return !(a == b);
		}

		public bool Equals(Message message)
		{
			if (message == null) { return false; }

			return message.GetHashCode() == GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (obj == null) { return false; }

			if (!(obj is Message)) { return false; }

			return obj.GetHashCode() == GetHashCode();
		}

		public override int GetHashCode()
		{
			return ID;
		}
	}
}
