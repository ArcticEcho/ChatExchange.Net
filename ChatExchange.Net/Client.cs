using System;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
    public class Client
    {
	    private readonly ScriptRuntime runtime;

		public PythonClass ClientPY { get; private set; }



		public Client(string host = "stackexchange.com", string email = null, string password = null)
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("client.py");
		    ClientPY.Class = file.client(host, email, password);
	    }

	    public Client(PythonClass client)
	    {
		    
	    }

	    public ~Client()
	    {
		    runtime.Shutdown();
	    }



		public Message GetMessgae(int messageID)
		{
			return ClientPY.Class.get_message(messageID);
		}

		public Room GetRoom(int roomID)
		{
			return ClientPY.Class.get_room(roomID);
		}

		public User GetUser(int userID)
		{
			return ClientPY.Class.get_user(userID);
		}

	    public User GetMe()
	    {
			return ClientPY.Class.get_me();
	    }

	    public void Login(string email, string password)
		{
			if (String.IsNullOrEmpty(email))
			{
				throw new ArgumentException("Email can not be null or empty.", "email");
			}

			if (String.IsNullOrEmpty(password))
			{
				throw new ArgumentException("Password can not be null or empty.", "password");
			}

			ClientPY.Class.login(email, password);
	    }

	    public void Logout()
	    {
			ClientPY.Class.logout();
	    }
    }
}
