using System;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
	public class Room
	{
		private readonly ScriptRuntime runtime;

		public PythonClass RoomPY { get; private set; }

		public string TextDescription
		{
			get
			{
				return RoomPY.Class.text_description;
			}
		}



		public Room(int ID, PythonClass client)
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("room.py");
		    RoomPY.Class = file.room(ID, client.Class);
	    }

		public Room(PythonClass room)
		{
			
		}

	    public ~Room()
	    {
		    runtime.Shutdown();
	    }



		public void ScrapeInfo()
		{
			RoomPY.Class.scrape_info();
		}

		public void Join()
		{
			RoomPY.Class.join();
		}

		public void SendMessage(string text)
		{
			RoomPY.Class.send_message(text);
		}

		public void Watch(Action eventCallback)
		{
			RoomPY.Class.watch(eventCallback);
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
