namespace ChatExchangeDotNet
{
	public class Message
	{
		public string Content { get; private set; }
		public int ID { get; private set; }
		public string Author { get; private set; }
		public int AuthorID { get; private set; }
		public int ParentID { get; private set; }



		public Message(string content, int ID, string author, int authorID, int parentID = -1)
		{
			Content = content;
			this.ID = ID;
			Author = author;
			AuthorID = authorID;
			ParentID = parentID;
		}
	}
}
