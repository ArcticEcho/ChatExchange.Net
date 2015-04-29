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
        /// Meaningful (unparsed) data has been received via the WebSocket (as JSON).
        /// </summary>
        DataReceived = 0,

        /// <summary>
        /// A new message has been posted.
        /// </summary>
        MessagePosted = 1,

        /// <summary>
        /// A message has been edited.
        /// </summary>
        MessageEdited = 2,

        /// <summary>
        /// A user has entered the room.
        /// </summary>
        UserEntered = 3,

        /// <summary>
        /// A user has left the room.
        /// </summary>
        UserLeft = 4,

        /// <summary>
        /// The room's name (and/or description) has been changed.
        /// </summary>
        RoomNameChanged = 5,

        /// <summary>
        /// Someone has (un)starred a message.
        /// </summary>
        MessageStarToggled = 6,

        /// <summary>
        /// Still have no idea what this is...
        /// </summary>
        DebugMessage = 7,

        /// <summary>
        /// You have been mentioned (@Username) in a message.
        /// </summary>
        UserMentioned = 8,

        /// <summary>
        /// A message has been flagged as spam/offensive.
        /// </summary>
        MessageFlagged = 9,

        /// <summary>
        /// A message has been deleted.
        /// </summary>
        MessageDeleted = 10,

        /// <summary>
        /// 
        /// </summary>
        FileAdded = 11,

        /// <summary>
        /// A message has been flagged for moderator attention.
        /// </summary>
        ModeratorFlag = 12,

        /// <summary>
        /// 
        /// </summary>
        UserSettingsChanged = 13,

        /// <summary>
        /// 
        /// </summary>
        GlobalNotification = 14,

        /// <summary>
        /// A user's room access level has been changed.
        /// </summary>
        AccessLevelChanged = 15,

        /// <summary>
        ///  
        /// </summary>
        UserNotification = 16,

        /// <summary>
        /// You have been invited to join another room.
        /// </summary>
        Invitation = 17,

        /// <summary>
        /// Someone has posted a reply to one of your messages.
        /// </summary>
        MessageReply = 18,

        /// <summary>
        /// A room owner (or moderator) has moved messages (or a message) out of the room.
        /// </summary>
        MessageMovedOut = 19,

        /// <summary>
        /// A room owner (or moderator) has moved messages (or a message) into the room.
        /// </summary>
        MessageMovedIn = 20,

        /// <summary>
        /// 
        /// </summary>
        TimeBreak = 21,

        /// <summary>
        /// Occurs when a new feed ticker message appears. 
        /// </summary>
        FeedTicker = 22,

        /// <summary>
        /// A user has been suspended.
        /// </summary>
        UserSuspended = 29,

        /// <summary>
        /// Two user accounts have been merged.
        /// </summary>
        UserMerged = 30
    }
}
