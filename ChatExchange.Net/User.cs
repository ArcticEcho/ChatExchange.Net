using System;
using System.Collections.Generic;
using System.Linq;
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
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("user.py");
		    userPY = file.client(ID, client.Class);
		}

		public User(PythonClass user)
		{
			
		}

	    public ~User()
	    {
		    runtime.Shutdown();
	    }
	}
}
