using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;



namespace ChatExchangeDotNet.MessageParser
{
	public static class Decoder
	{
		private static Regex stripTags = new Regex("<a href=\".*?\" rel=\"nofollow\">|</a>|</?b>|</?i>|</?code>|<div.*?>(<a .*?>)?|</div>|<img src=\"|\" class=\"user-image\" alt=\"user image\" />", RegexOptions.Compiled | RegexOptions.CultureInvariant); 
		//TODO: Finish off implementation.

		public static string Decode(MessageParsingOption option, string message)
		{
			var decoded = "";

			switch (option)
			{
				case MessageParsingOption.StripMarkdown:
				{
					return stripTags.Replace(message, "");
				}
			}

			return "";
		}
	}
}
