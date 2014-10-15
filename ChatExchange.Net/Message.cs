using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;



namespace ChatExchangeDotNet
{
	public class Message
	{
		public int Room
		{
			get { return; }
		}

		public string Content
		{
			get { return; }
		}

		public string Owner
		{
			get { return; }
		}

		public Dictionary<string, dynamic> Stars
		{
			get { return; }
		}

		public bool StarredByYou
		{
			get { return; }
		}

		public bool Pinned
		{
			get { return; }
		}

		public string ContentSource
		{
			get { return; }
		}

		public User Editor
		{
			get { return; }
		}

	//edited = _utils.LazyFrom('scrape_history')
	//edits = _utils.LazyFrom('scrape_history')
	//pins = _utils.LazyFrom('scrape_history')
	//pinners = _utils.LazyFrom('scrape_history')
	//time_stamp = _utils.LazyFrom('scrape_history')

		public Message(int ID, Client client)
		{
			

	    }

	    ~Message()
	    {

	    }
	}
}
