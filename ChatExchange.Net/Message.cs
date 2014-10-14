using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using IronPython.Runtime;



namespace ChatExchangeDotNet
{
	public class Message
	{
		private readonly ScriptRuntime runtime;

		public PythonClass MessagePY { get; private set; }

		public int Room
		{
			get
			{
				return (int)MessagePY.Class.room();
			}
		}

		public string Content
		{
			get
			{
				return (string)MessagePY.Class.content();
				
			}
		}

		public string Owner
		{
			get
			{
				return (string)MessagePY.Class.owner();
			}
		}

		public Dictionary<string, dynamic> Stars
		{
			get
			{
				var d = new Dictionary<string, dynamic>();

				PythonDictionary pyD = MessagePY.Class.stars();

				foreach (var pair in pyD)
				{
					d.Add((string)pair.Key, pair.Value);
				}

				return d;
			}
		}

		public bool StarredByYou
		{
			get
			{
				return (bool)MessagePY.Class.starred_by_you();
				
			}
		}

		public bool Pinned
		{
			get
			{
				return (bool)MessagePY.Class.pinned();		
			}
		}

		public string ContentSource
		{
			get
			{
				return (string)MessagePY.Class.content_source();
			}
		}

		public User Editor
		{
			get
			{
				var editor = new PythonClass { Class = MessagePY.Class.editor() };

				return new User(editor);
			}
		}

	//edited = _utils.LazyFrom('scrape_history')
	//edits = _utils.LazyFrom('scrape_history')
	//pins = _utils.LazyFrom('scrape_history')
	//pinners = _utils.LazyFrom('scrape_history')
	//time_stamp = _utils.LazyFrom('scrape_history')

		public Message(int ID, PythonClass client)
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
			dynamic file = runtime.UseFile("message.py");
			MessagePY.Class = file.client(ID, client.Class);
	    }

		public Message(PythonClass message)
		{
			MessagePY = message.Class;
		}

	    ~Message()
	    {
		    if (runtime != null)
			{
				runtime.Shutdown();		    
		    }
	    }
	}
}
