using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChatExchangeDotNet
{
	public enum MessageParsingOption
	{
		/// <summary>
		/// Leaves the message in its raw state (HTML tags are left).
		/// </summary>
		LeaveRaw,
		/// <summary>
		/// Removes all HTML markdown tags.
		/// </summary>
		StripMarkdown,
		/// <summary>
		/// Encodes all HTML tags to SE markdown.
		/// </summary>
		EncodeToSEMarkdown
	}
}
