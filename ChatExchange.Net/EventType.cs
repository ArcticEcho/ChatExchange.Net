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





namespace ChatExchangeDotNet
{
    /// <summary>
    /// An enumeration of all known chat events.
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// An exception has been raised from within the library.
        /// </summary>
        InternalException = -1,

        /// <summary>
        /// Raw data (string) has been received via the WebSocket (as JSON).
        /// </summary>
        DataReceived,

        /// <summary>
        /// A new message has been posted.
        /// </summary>
        MessagePosted,

        /// <summary>
        /// A message has been edited.
        /// </summary>
        MessageEdited,

        /// <summary>
        /// A user has entered the room.
        /// </summary>
        UserEntered,

        /// <summary>
        /// A user has left the room.
        /// </summary>
        UserLeft,

        /// <summary>
        /// The room's name (and/or description) has been changed.
        /// </summary>
        //RoomNameChanged,

        /// <summary>
        /// Someone has (un)starred a message.
        /// </summary>
        MessageStarToggled = 6,

        /// <summary>
        /// Still have no idea what this is...
        /// </summary>
        //DebugMessage,

        /// <summary>
        /// You have been mentioned (@Username) in a message.
        /// </summary>
        UserMentioned = 8,

        /// <summary>
        /// A message has been flagged as spam/offensive.
        /// </summary>
        //MessageFlagged,

        /// <summary>
        /// A message has been deleted.
        /// </summary>
        MessageDeleted = 10,

        /// <summary>
        /// 
        /// </summary>
        //FileAdded,

        /// <summary>
        /// A message has been flagged for moderator attention.
        /// </summary>
        //ModeratorFlag,

        /// <summary>
        /// 
        /// </summary>
        //UserSettingsChanged,

        /// <summary>
        /// 
        /// </summary>
        //GlobalNotification,

        /// <summary>
        /// A user's room access level has been changed.
        /// </summary>
        UserAccessLevelChanged = 15,

        /// <summary>
        ///  
        /// </summary>
        //UserNotification,

        /// <summary>
        /// You have been invited to join another room.
        /// </summary>
        //Invitation,

        /// <summary>
        /// Someone has posted a reply to one of your messages.
        /// </summary>
        MessageReply = 18,

        /// <summary>
        /// A room owner (or moderator) has moved messages (or a message) out of the room.
        /// </summary>
        //MessageMovedOut,

        /// <summary>
        /// A room owner (or moderator) has moved messages (or a message) into the room.
        /// </summary>
        //MessageMovedIn,

        /// <summary>
        /// 
        /// </summary>
        //TimeBreak,

        /// <summary>
        /// Occurs when a new feed ticker message is posted.
        /// </summary>
        //FeedTicker,

        /// <summary>
        /// A user has been suspended.
        /// </summary>
        //UserSuspended = 29,

        /// <summary>
        /// Two user accounts have been merged.
        /// </summary>
        //UserMerged
    }
}
