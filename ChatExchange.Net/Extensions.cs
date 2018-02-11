/*
 * ChatExchange.Net. A .Net (4.0) API for interacting with Stack Exchange chat.
 * Copyright © 2015, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AngleSharp.Dom.Html;

namespace ChatExchangeDotNet
{
	public static class Extensions
    {
        private static readonly Regex isReply = new Regex(@"^:\d+\s\S", RegexOpts);

        internal static RegexOptions RegexOpts { get; } = RegexOptions.CultureInvariant;



		internal static string GetFKey(this IHtmlDocument dom)
		{
			return dom.QuerySelector("[name=fkey]").Attributes["value"].Value;
		}

		internal static char[] Where(this string str, Func<char, bool> pred)
		{
			return str.ToCharArray().Where(pred).ToArray();
		}

		internal static IEnumerable<T> Where<T>(this IEnumerable<T> items, Func<T, bool> pred)
		{
			var filteredItems = new List<T>();
			foreach (var item in items)
			{
				if (pred(item))
				{
					filteredItems.Add(item);
				}
			}
			return filteredItems;
		}

        //internal static string GetInputValue(this CQ input, string elementName)
        //{
        //    var fkeyE = input["input"].FirstOrDefault(e => e.Attributes["name"] == elementName);
        //    return fkeyE?.Attributes["value"];
        //}

        public static IEnumerable<Message> GetMessagesByUser(this IEnumerable<Message> messages, User user)
        {
            var id = user?.ID ?? throw new ArgumentNullException(nameof(user));
            return messages.GetMessagesByUser(id);
        }

        public static IEnumerable<Message> GetMessagesByUser(this IEnumerable<Message> messages, int userID)
        {
            return (messages ?? throw new ArgumentNullException(nameof(messages)))
                .Where(m => m.Author.ID == userID);
        }

        public static bool IsReply(this string message)
        {
            return !string.IsNullOrEmpty(message) && isReply.IsMatch(message);
        }
    }
}
