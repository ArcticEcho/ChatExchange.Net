using System.Collections.Generic;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using IronPython.Runtime;



namespace ChatExchangeDotNet
{
	public class Message
	{
		private readonly ScriptRuntime runtime;
	    private readonly PythonClass messagePY = new PythonClass();

		public int Room
		{
			get
			{
				return messagePY.Class.room();
			}
		}

		public string Content
		{
			get
			{
				return messagePY.Class.content();
				
			}
		}

		public string Owner
		{
			get
			{
				return messagePY.Class.owner();
			}
		}

		public Dictionary<string, dynamic> Stars
		{
			get
			{
				var d = new Dictionary<string, dynamic>();

				PythonDictionary pyD = messagePY.Class.stars();

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
				return (bool)messagePY.Class.starred_by_you();
				
			}
		}

		public bool Pinned
		{
			get
			{
				return (bool)messagePY.Class.pinned();		
			}
		}

		public string ContentSource
		{
			get
			{
				return (string)messagePY.Class.content_source();
				
			}
		}

		public User Editor
		{
			get
			{
				var editor = messagePY.Class.editor();
				// TODO:
			}
		}

	//editor = _utils.LazyFrom('scrape_history')
	//edited = _utils.LazyFrom('scrape_history')
	//edits = _utils.LazyFrom('scrape_history')
	//pins = _utils.LazyFrom('scrape_history')
	//pinners = _utils.LazyFrom('scrape_history')
	//time_stamp = _utils.LazyFrom('scrape_history')

		public Message(int ID, PythonClass client)
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("message.py");
		    messagePY.Class = file.client(ID, client.Class);
	    }

		public Message(PythonClass message)
		{
			
		}

	    public ~Message()
	    {
		    runtime.Shutdown();
	    }
	}
}
