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

#pragma warning disable CS1591



using System;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Represents errors that occur during authentication.
    /// </summary>
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

    /// <summary>
    /// Represents errors that occur when an attempted action requires more reputation to execute.
    /// </summary>
    public class InsufficientReputationException : Exception
    {
        /// <summary>
        /// The amount of reputation required to successfully execute the action.
        /// </summary>
        public int ReputationRequired { get; }



        public InsufficientReputationException() : base("You need more reputation to execute this action.")
        {

        }

        public InsufficientReputationException(int reputationRequired) : base("You need more reputation to execute this action.")
        {
            ReputationRequired = reputationRequired;
        }

        public InsufficientReputationException(string message) : base(message)
        {

        }

        public InsufficientReputationException(string message, int reputationRequired) : base(message)
        {
            ReputationRequired = reputationRequired;
        }

        public InsufficientReputationException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public InsufficientReputationException(string message, int reputationRequired, Exception innerException) : base(message, innerException)
        {
            ReputationRequired = reputationRequired;
        }
    }

    public class InsufficientPermissionException : Exception
    {
        /// <summary>
        /// The lowest permission level required to execute this action.
        /// </summary>
        public UserRoomAccess ReputationPermission { get; }



        public InsufficientPermissionException() : base("You need greater permission to execute this action.")
        {
            
        }

        public InsufficientPermissionException(UserRoomAccess reputationPermission) : base("You need greater permission to execute this action.")
        {
            ReputationPermission = reputationPermission;
        }

        public InsufficientPermissionException(string message) : base(message)
        {

        }

        public InsufficientPermissionException(string message, UserRoomAccess reputationPermission) : base(message)
        {
            ReputationPermission = reputationPermission;
        }

        public InsufficientPermissionException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public InsufficientPermissionException(string message, UserRoomAccess reputationPermission, Exception innerException) : base(message, innerException)
        {
            ReputationPermission = reputationPermission;
        }
    }


    /// <summary>
    /// Represents an error that occurs when a requested message is not found.
    /// </summary>
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

    /// <summary>
    /// Represents an error that occurs when an attempt to post a duplicate message is made.
    /// </summary>
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

    /// <summary>
    /// Represents an error that occurs when an attempt to post a duplicate message is made.
    /// </summary>
    public class UserNotFoundException : Exception
    {

        /// <summary>
        /// The ID of the user that does not exist.
        /// </summary>
        public int UserID { get; }



        public UserNotFoundException() : base("The specified user was not found.")
        {

        }

        public UserNotFoundException(string message) : base(message)
        {

        }

        public UserNotFoundException(string message, int userId) : base(message)
        {
            UserID = userId;
        }

        public UserNotFoundException(string message, Exception innerException) : base(message, innerException)
        {

        }

        public UserNotFoundException(string message, int userId, Exception innerException) : base(message, innerException)
        {
            UserID = userId;
        }
    }
}
