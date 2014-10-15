using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;



namespace ChatExchangeDotNet
{
	public class User
	{
		public string Name { get; private set; }

		public string About { get; private set; }

		public bool IsModerator { get; private set; }

		public int MessageCount { get; private set; }

		public int RoomCount { get; private set; }



		public User(int ID, Client client)
		{
			//var data = client.Browser.GetProfile(ID);

			//self.name = data['name']
			//self.is_moderator = data['is_moderator']
			//self.message_count = data['message_count']
			//self.room_count = data['room_count']
		}
	}
}
