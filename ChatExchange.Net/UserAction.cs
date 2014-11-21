using System;



namespace ChatExchangeDotNet
{
	internal class UserAction
	{
		public Action Callback { get; private set; }
		public string PostURL { get; private set; }
		public string Data { get; private set; }
		public UserActionType Type { get; private set; }



		public UserAction(Action callback, string postURL, string data, UserActionType type)
		{
			Callback = callback;
			PostURL = postURL;
			Data = data;
			Type = type;
		}
	}
}
