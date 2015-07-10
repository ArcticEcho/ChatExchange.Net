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
using System.Reflection;

namespace ChatExchangeDotNet.EventListeners
{
    internal class MessageReply : IEventListener
    {
        public Exception CheckListener(Delegate listener)
        {
            if (listener == null) { return new ArgumentNullException("listener"); }

            var listenerParams = listener.Method.GetParameters();

            if (listenerParams == null || listenerParams.Length != 2 ||
                listenerParams[0].ParameterType != typeof(Message) ||
                listenerParams[1].ParameterType != typeof(Message))
            {
                return new TargetException("This chat event takes two arguments (both) of type 'Message'.");
            }

            return null;
        }

        public void Execute(Room room, ref EventManager evMan, Dictionary<string, object> data)
        {
            // No point parsing all this data if no one's listening.
            if (!evMan.ConnectedListeners.ContainsKey(EventType.MessageReply)) { return; }

            var authorID = int.Parse(data["user_id"].ToString());

            if (authorID == room.Me.ID && room.IgnoreOwnEvents) { return; }

            var id = int.Parse(data["message_id"].ToString());
            var parentID = int.Parse(data["parent_id"].ToString());
            var parent = room[parentID];
            var message = new Message(room.Host, room.ID, id, room.GetUser(authorID), room.StripMentionFromMessages, parentID);

            evMan.TrackMessage(message);
            evMan.CallListeners(EventType.MessageReply, parent, message);
        }
    }
}
