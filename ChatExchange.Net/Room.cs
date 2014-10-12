using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
	public class Room
	{
		private readonly ScriptRuntime runtime;
	    private readonly dynamic roomPY;

		public dynamic room
		{
			get
			{
				return roomPY;
			}
		}

		public string TextDescription
		{
			get
			{
				return roomPY.text_description;
			}
		}



		public Room(int ID, dynamic client)
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("room.py");
		    roomPY = file.room(ID, client);
	    }

	    public ~Room()
	    {
		    runtime.Shutdown();
	    }



		public void ScrapeInfo()
		{
			roomPY.scrape_info();
		}

		public dynamic Join()
		{
			return roomPY.join();
		}
	}
}
