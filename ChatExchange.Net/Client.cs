using System;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;



namespace ChatExchangeDotNet
{
    public class Client
    {
	    private readonly ScriptRuntime runtime;
	    private readonly dynamic clientPY;

	    public dynamic client
	    {
		    get
		    {
				return clientPY;
		    }
	    }



		public Client(string host = "stackexchange.com", string email = null, string password = null)
		{
			runtime = Python.CreateRuntime();
			dynamic file = runtime.UseFile("client.py");
		    clientPY = file.client(host, email, password);
	    }

	    public ~Client()
	    {
		    runtime.Shutdown();
	    }



		public dynamic GetMessgae(int messageID)
		{
			return clientPY.get_message(messageID);
		}

		public dynamic GetRoom(int roomID)
		{
			return clientPY.get_room(roomID);
		}

		public dynamic GetUser(int userID)
		{
			return clientPY.get_user(userID);
		}

	    public dynamic GetMe()
	    {
		    return clientPY.get_me();
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

		    clientPY.login(email, password);
	    }

	    public void Logout()
	    {
		    clientPY.logout();
	    }
    }
}
