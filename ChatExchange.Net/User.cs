using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsQuery;
using Newtonsoft.Json.Linq;



namespace ChatExchangeDotNet
{
	public class User
	{
		public string Name { get; private set; }
		public int ID { get; private set; }
		public bool IsMod { get; private set; }
		public bool IsRoomOwner { get; private set; }
		public int Reputation { get; private set; }



		public User(string name, int id, int roomID, string host)
		{
			Name = name;
			ID = id;

			var res = RequestManager.SendPOSTRequest("http://chat." + host + "/user/info", "ids=" + id + "&roomid=" + roomID);

			if (res == null)
			{
				Reputation = -1;
			}
			else
			{
				var resContent = RequestManager.GetResponseContent(res);

				var json = JObject.Parse(resContent);

				var isMod = json["users"][0]["is_moderator"];
				var isOwner = json["users"][0]["is_owner"];
				var rep = json["users"][0]["reputation"];

				IsMod = isMod != null && isMod.Type == JTokenType.Boolean && (bool)isMod;
				IsRoomOwner = isOwner != null && isOwner.Type == JTokenType.Boolean && (bool)isOwner;
				Reputation = rep == null || rep.Type != JTokenType.Integer ? 1 : (int)rep;
			}
		}


		/// <summary>
		/// Returns whether the specified user is a moderator.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="userID"></param>
		/// <returns>True if the user is a moderator, otherwise false.</returns>
		private static bool IsModerator(string host, int userID)
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
