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
		public int RoomID { get; private set; }
		public string Host { get; private set; }



		public User(string name, int id, int roomID, string host)
		{
			Name = name;
			ID = id;
			RoomID = roomID;
			Host = host;

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
	}
}
