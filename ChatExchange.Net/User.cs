using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



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
	}
}
