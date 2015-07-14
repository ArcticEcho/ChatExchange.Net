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
using System.Linq;
using System.Text.RegularExpressions;

namespace ChatExchangeDotNet
{
    public class MessageBuilder
    {
        private const RegexOptions regOpts = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        private static readonly Regex tagReg = new Regex(@"[^\w\.\#_\-]", regOpts);
        private static readonly Regex chatMd = new Regex(@"[_*`\[\]]", regOpts);
        private readonly string newline = "\n";
        private string message = "";

        public MultiLineMessageType MultiLineType { get; private set; }

        public bool EscapeMarkdown { get; private set; }

        public string Message
        {
            get
            {
                return message.TrimEnd();
            }
        }



        public MessageBuilder(MultiLineMessageType type = MultiLineMessageType.None, bool escapeMarkdown = true)
        {
            newline += type == MultiLineMessageType.Quote ? "> " : type == MultiLineMessageType.Code ? "    " : "";
            message = type == MultiLineMessageType.None ? "" : newline;
            MultiLineType = type;
            EscapeMarkdown = escapeMarkdown;
        }



        public override string ToString()
        {
            return Message;
        }

        public void Clear()
        {
            message = "";
        }

        public void AppendText(string text, TextFormattingOptions formattingOptions = TextFormattingOptions.None, WhiteSpace appendWhiteSpace = WhiteSpace.None)
        {
            if (string.IsNullOrEmpty(text)) { throw new ArgumentException("'text' must not be null or empty.", "text"); }

            var msg = EscapeMarkdown ? chatMd.Replace(text, @"\$0") : text;
            message += FormatString(msg, formattingOptions);
            message = AppendWhiteSpace(message, appendWhiteSpace);
        }

        public void AppendLink(string text, string url, string onHoverText = null, TextFormattingOptions formattingOptions = TextFormattingOptions.None, WhiteSpace appendWhiteSpace = WhiteSpace.Space)
        {
            if (MultiLineType != MultiLineMessageType.None)
            {
                throw new InvalidOperationException("Cannot append an in-line link when this object's 'MultiLineType' is not set to 'MultiLineMessageType.None'.");
            }
            if (string.IsNullOrEmpty(url)) { throw new ArgumentException("'url' must not be null or empty.", "url"); }
            if (string.IsNullOrEmpty(text)) { throw new ArgumentException("'text' must not be null or empty.", "text"); }

            var urlSafe = (url.StartsWith("http://") ? url : "http://" + url).Trim();
            var textSafe = chatMd.Replace(text.Trim(), @"\$0");

            message += "[" + FormatString(textSafe, formattingOptions) + "]";
            message += "(" + urlSafe + (string.IsNullOrEmpty(onHoverText) ? "" : " \"" + onHoverText + "\"") + ")";
            message = AppendWhiteSpace(message, appendWhiteSpace);
        }

        public void AppendPing(User targetUser, WhiteSpace appendWhiteSpace = WhiteSpace.Space)
        {
            if (targetUser == null) { throw new ArgumentNullException("targetUser"); }

            AppendPing(targetUser.Name, appendWhiteSpace);
        }

        public void AppendPing(string targetUserName, WhiteSpace appendWhiteSpace = WhiteSpace.Space)
        {
            if (string.IsNullOrEmpty(targetUserName)) { throw new ArgumentException("'targetUserName' must not be null or empty.", "targetUser"); }

            message += "@" + targetUserName.Replace(" ", "");
            message = AppendWhiteSpace(message, appendWhiteSpace);
        }



        private string AppendWhiteSpace(string text, WhiteSpace option = WhiteSpace.None)
        {
            if (option == WhiteSpace.None) { return text; }

            return text + (option == WhiteSpace.Space ? " " : newline);
        }

        private string FormatString(string text, TextFormattingOptions formattingOptions)
        {
            if (MultiLineType == MultiLineMessageType.Code)
            {
                return text.Replace("\n", "\n    ");
            }

            if (MultiLineType == MultiLineMessageType.Quote)
            {
                return text.Replace("\n", "\n> ");
            }

            if (formattingOptions == TextFormattingOptions.None)
            {
                return text;
            }

            if (formattingOptions == TextFormattingOptions.Tag)
            {
                return "[tag:" + tagReg.Replace(text.Trim(), "-") + "]";
            }

            var mdChars = "";

            if ((formattingOptions & TextFormattingOptions.Strikethrough) == TextFormattingOptions.Strikethrough)
            {
                mdChars = "---";
            }

            if ((formattingOptions & TextFormattingOptions.Bold) == TextFormattingOptions.Bold)
            {
                mdChars += "**";
            }

            if ((formattingOptions & TextFormattingOptions.Italic) == TextFormattingOptions.Italic)
            {
                mdChars += "*";
            }

            if ((formattingOptions & TextFormattingOptions.InLineCode) == TextFormattingOptions.InLineCode)
            {
                mdChars += "`";
            }

            var mdCharsRev = new string(mdChars.Reverse().ToArray());

            var msg = text;
            var textLen = text.Length;

            var startOffset = textLen - text.TrimStart().Length;
            msg = msg.Insert(startOffset, mdChars);

            var endOffset = textLen - text.TrimEnd().Length;
            msg = msg.Insert((textLen - endOffset) + mdChars.Length, mdCharsRev);

            return msg;
        }
    }
}
