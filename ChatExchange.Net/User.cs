using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsQuery;



namespace ChatExchangeDotNet
{
	public class User
	{
		public string Name { get; private set; }
		public int ID { get; private set; }
		public bool IsMod { get; private set; }



		public User(string name, int id, bool isMod = false)
		{
			Name = name;
			ID = id;
			IsMod = isMod;
		}


		/// <summary>
		/// Returns whether the specified user is a moderator.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="userID"></param>
		/// <returns>True if the user is a moderator, otherwise false.</returns>
		public static bool IsModerator(string host, int userID)
		{
			if (String.IsNullOrEmpty(host)) { throw new ArgumentException("'host' can not be null or empty.", "host"); }
			if (userID < -1) { throw new ArgumentOutOfRangeException("userID", "'userID' can not be less than -1."); }

			var res = RequestManager.SendGETRequest("http://chat." + host + "/users/" + userID);

			if (res == null) { throw new Exception("Could not get user information. Do you have an active internet connection?");}

			var dom = CQ.Create(RequestManager.GetResponseContent(res));

			var t = dom[".user-status"].First().Text();

			return dom[".user-status"].First().Text().Contains('♦');
		}
	}
}
