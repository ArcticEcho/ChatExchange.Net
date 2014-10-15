using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;



namespace ChatExchangeDotNet
{
	public class Room
	{
		public string TextDescription
		{
			get { return; }
		}



		public Room(int ID, Client client)
		{

	    }

	    ~Room()
	    {

	    }



		public void ScrapeInfo()
		{
			
		}

		public void Join()
		{
			
		}

		public void SendMessage(string text)
		{
			
		}

		public void Watch(Action eventCallback)
		{
			
		}

		public void WatchPolling()
		{
			throw new NotImplementedException();
		}

		public void WatchSocket()
		{
			throw new NotImplementedException();
		}

		public void NewEvents()
		{
			
		}
	}
}
