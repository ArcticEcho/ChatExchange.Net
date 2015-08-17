using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    public class DupelicateMessageException : Exception
    {
        public DupelicateMessageException() : base("An attempt to post a dupelicate message has been made.")
        {

        }

        public DupelicateMessageException(string message) : base(message)
        {

        }

        public DupelicateMessageException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

}
