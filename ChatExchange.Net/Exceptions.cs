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

namespace ChatExchangeDotNet
{
    public class AuthenticationException : Exception
    {
        public AuthenticationException()
        {

        }

        public AuthenticationException(string message) : base(message)
        {

        }

        public AuthenticationException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    public class MessageNotFoundException : Exception
    {
        public MessageNotFoundException() : base("The requested message was not found.")
        {

        }

        public MessageNotFoundException(string message) : base(message)
        {

        }

        public MessageNotFoundException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    public class DuplicateMessageException : Exception
    {
        public DuplicateMessageException() : base("An attempt to post a duplicate message has been made.")
        {

        }

        public DuplicateMessageException(string message) : base(message)
        {

        }

        public DuplicateMessageException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

}
