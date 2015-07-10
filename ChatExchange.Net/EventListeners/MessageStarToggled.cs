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
    internal class MessageStarToggled : IEventListener
    {
        public Exception CheckListener(Delegate listener)
        {
            if (listener == null) { return new ArgumentNullException("listener"); }

            var listenerParams = listener.Method.GetParameters();

            if (listenerParams == null || listenerParams.Length != 4 ||
                listenerParams[0].ParameterType != typeof(Message) ||
                listenerParams[1].ParameterType != typeof(User) ||
                listenerParams[2].ParameterType != typeof(int) ||
                listenerParams[3].ParameterType != typeof(int))
            {
                return new TargetException("This chat event takes four arguments of type (in order): 'Message', 'User', 'int' & 'int'.");
            }

            return null;
        }

        public void Execute(Room room, ref EventManager evMan, Dictionary<string, object> data)
        {
            // No point parsing all this data if no one's listening.
            if (!evMan.ConnectedListeners.ContainsKey(EventType.MessageStarToggled)) { return; }

            var starrerID = int.Parse(data["user_id"].ToString());

            if (starrerID == room.Me.ID && room.IgnoreOwnEvents) { return; }

            var id = int.Parse(data["message_id"].ToString());
            var starCount = 0;
            var pinCount = 0;

            if (data.ContainsKey("message_stars") && data["message_stars"] != null)
            {
                starCount = int.Parse(data["message_stars"].ToString());
            }

            if (data.ContainsKey("message_owner_stars") && data["message_owner_stars"] != null)
            {
                pinCount = int.Parse(data["message_owner_stars"].ToString());
            }

            var message = room[id];
            var user = room.GetUser(starrerID);

            evMan.CallListeners(EventType.MessageStarToggled, message, user, starCount, pinCount);
        }
    }
}
