using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
	public class Message
	{
		private readonly ScriptRuntime runtime;
	    private readonly dynamic messagePY;

		public int Room
		{
			get
			{
				return messagePY.room();
			}
		}

		public string Content
		{
			get
			{
				return messagePY.content();
				
			}
		}
    content = _utils.LazyFrom('scrape_transcript')
    owner = _utils.LazyFrom('scrape_transcript')
    _parent_message_id = _utils.LazyFrom('scrape_transcript')
    stars = _utils.LazyFrom('scrape_transcript')
    starred_by_you = _utils.LazyFrom('scrape_transcript')
    pinned = _utils.LazyFrom('scrape_transcript')

    content_source = _utils.LazyFrom('scrape_history')
    editor = _utils.LazyFrom('scrape_history')
    edited = _utils.LazyFrom('scrape_history')
    edits = _utils.LazyFrom('scrape_history')
    pins = _utils.LazyFrom('scrape_history')
    pinners = _utils.LazyFrom('scrape_history')
    time_stamp = _utils.LazyFrom('scrape_history')

		public Message()
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("message.py");
		    messagePY = file.client();
	    }

	    public ~Message()
	    {
		    runtime.Shutdown();
	    }
	}
}
