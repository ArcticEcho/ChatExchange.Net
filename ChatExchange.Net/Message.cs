using System;



namespace ChatExchangeDotNet
{
	public class Message
	{
		public string Content { get; private set; }
		public int ID { get; private set; }
		public string AuthorName { get; private set; }
		public int AuthorID { get; private set; }
		public int ParentID { get; private set; }
		public int StarCount { get; set; }
		public int PinCount { get; set; }



		public Message(string content, int ID, string authorName, int authorID, int parentID = -1, int starCount = 0, int pinCount = 0)
		{
			if (String.IsNullOrEmpty(content)) { throw new ArgumentException("'content' can not be null or empty.", "content"); }
			if (ID < 0) { throw new ArgumentOutOfRangeException("ID", "'ID' can not be less than 0."); }
			if (String.IsNullOrEmpty(authorName)) { throw new ArgumentException("'authorName' can not be null or empty.", "authorName"); }
			if (authorID < -1) { throw new ArgumentOutOfRangeException("authorID", "'authorID' can not be less than -1."); }
			if (starCount < 0) { throw new ArgumentOutOfRangeException("starCount", "'starCount' can not be less than 0."); }
			if (pinCount < 0) { throw new ArgumentOutOfRangeException("pinCount", "'pinCount' can not be less than 0."); }

			Content = content;
			this.ID = ID;
			AuthorName = authorName;
			AuthorID = authorID;
			ParentID = parentID;
			StarCount = starCount;
			PinCount = pinCount;
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
