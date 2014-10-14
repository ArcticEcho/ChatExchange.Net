using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
			var options = new Dictionary<string, object>();
			options["Frames"] = true;
			options["FullFrames"] = true;

			var engine = Python.CreateEngine(options);

			var paths = engine.GetSearchPaths();
			paths.Add(@"C:\Python27\Lib\");
			paths.Add(@"C:\Python27\Lib\site-packages\");
			paths.Add(Directory.GetParent((new Uri(Assembly.GetExecutingAssembly().CodeBase)).AbsolutePath).FullName);
			engine.SetSearchPaths(paths);

			runtime = engine.Runtime;
			dynamic file = runtime.UseFile("room.py");
		    RoomPY.Class = file.room(ID, client.Class);
	    }

		public Room(PythonClass room)
		{
			RoomPY = room.Class;
		}

	    ~Room()
	    {
		    if (runtime != null)
		    {
				runtime.Shutdown();
		    }
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
