using System;
using System.Net;
using CsQuery;



namespace ChatExchangeDotNet
{
    public class Message
    {
        private bool stripMention;
        private string content;

        public int ID { get; private set; }
        public string AuthorName { get; private set; }
        public int AuthorID { get; private set; }
        public int ParentID { get; private set; }
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
                var res = RequestManager.SendGETRequest("", "http://chat." + Host + "/messages/" + ID + "/history");

                if (res == null) { return -1; }

                var dom = CQ.Create(RequestManager.GetResponseContent(res))[".stars"];
                var count = 0;

                if (dom != null && dom.Length != 0)
                {
                    if (dom[".times"] != null && !String.IsNullOrEmpty(dom[".times"].First().Text()))
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
                var res = RequestManager.SendGETRequest("", "http://chat." + Host + "/messages/" + ID + "/history");

                if (res == null) { return -1; }

                var dom = CQ.Create(RequestManager.GetResponseContent(res))[".owner-star"];
                var count = 0;

                if (dom != null && dom.Length != 0)
                {
                    if (dom[".times"] != null && !String.IsNullOrEmpty(dom[".times"].First().Text()))
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



        public Message(string host, int roomID, int messageID, string authorName, int authorID, bool stripMention = true, int parentID = -1)
        {
            EventManager temp = null;
            var ex = Initialise(host, roomID, messageID, authorName, authorID, stripMention, parentID, ref temp);

            if (ex != null)
            {
                throw ex;
            }
        }

        public Message(ref EventManager evMan, string host, int roomID, int messageID, string authorName, int authorID, bool stripMention = true, int parentID = -1)
        {
            var ex = Initialise(host, roomID, messageID, authorName, authorID, stripMention, parentID, ref evMan);

            if (ex != null)
            {
                throw ex;
            }
        }



        public static string GetMessageContent(string host, int messageID, bool stripMention = true)
        {
            var res = RequestManager.SendGETRequest("", "http://chat." + host + "/message/" + messageID + "?plain=true");

            if (res == null || res.StatusCode != HttpStatusCode.OK) { return null; }

            var content = RequestManager.GetResponseContent(res);

            return String.IsNullOrEmpty(content) ? null : WebUtility.HtmlDecode(stripMention ? content.StripMention() : content);
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



        private Exception Initialise(string host, int roomID, int messageID, string authorName, int authorID, bool stripMention, int parentID, ref EventManager evMan)
        {
            if (String.IsNullOrEmpty(host)) { return new ArgumentException("'host' can not be null or empty.", "host"); }
            if (messageID < 0) { return new ArgumentOutOfRangeException("messageID", "'ID' can not be less than 0."); }
            if (String.IsNullOrEmpty(authorName)) { return new ArgumentException("'authorName' can not be null or empty.", "authorName"); }
            if (authorID < -1) { return new ArgumentOutOfRangeException("authorID", "'authorID' can not be less than -1."); }

            this.stripMention = stripMention;
            content = GetMessageContent(host, messageID, stripMention);
            Host = host;
            RoomID = roomID;
            ID = messageID;
            AuthorName = authorName;
            AuthorID = authorID;
            ParentID = parentID;

            if (evMan != null)
            {
                evMan.ConnectListener(EventType.MessageEdited, new Action<Message, Message>((oldMessage, newMessage) =>
                {
                    if (oldMessage.ID == messageID)
                    {
                        content = newMessage.Content;
                    }
                }));
            }

            return null;
        }
    }
}
