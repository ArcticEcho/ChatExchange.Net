using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsQuery;



namespace ChatExchangeDotNet
{
	public class Room
	{
		private readonly RequestManager reqManager = new RequestManager();
		private readonly Regex roomIDParser = new Regex(@".*/rooms/|/.*");

		public int ID { get; private set; }
		
		public Action<Message> NewMessageEvent { get; set; }


		
		public Room(string roomUrl, string email = "", string password = "")
		{
			if (String.IsNullOrEmpty(roomUrl)) { throw new ArgumentException("roomUrl must not be null or empty.", "roomUrl"); }

			ID = int.Parse(roomIDParser.Replace(roomUrl, ""));

			if (String.IsNullOrEmpty(email) || String.IsNullOrEmpty(password))
			{
				// Don't login.
			}
			else
			{
				Login(email, password);
			}
		}



		private void Login(string email, string password)
		{
			var req = reqManager.GetResponseContent(reqManager.SendGETRequest("http://stackexchange.com/users/chat-login", "", "", true));

			var dom = CQ.Create(req);

			var authToken = dom["input"].First(e => e.Attributes["name"] != null && e.Attributes["name"] == "fkey").Attributes["value"];

			// TODO: Finish off implementation...
		}
	}
}
