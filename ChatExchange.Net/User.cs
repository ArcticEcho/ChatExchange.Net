using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
	public class User
	{
		private readonly ScriptRuntime runtime;
	    private readonly dynamic userPY;

		public string Name
		{
			get
			{
				return (string)userPY.Class.name();
				
			}
		}

	//	name = _utils.LazyFrom('scrape_profile')
	//about = _utils.LazyFrom('scrape_profile')
	//is_moderator = _utils.LazyFrom('scrape_profile')
	//message_count = _utils.LazyFrom('scrape_profile')
	//room_count =

	    public dynamic user
	    {
		    get
		    {
				return userPY;
		    }
	    }



		public User(int ID, PythonClass client)
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
			dynamic file = runtime.UseFile("user.py");
		    userPY = file.client(ID, client.Class);
		}

		public User(PythonClass user)
		{
			userPY = user.Class;
		}

	    ~User()
	    {
		    if (runtime != null)
		    {
				runtime.Shutdown();
		    }
	    }
	}
}
