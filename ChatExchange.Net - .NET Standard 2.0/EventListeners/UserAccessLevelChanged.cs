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
    internal class UserAccessLevelChanged : IEventListener
    {
        public Exception CheckListener(Delegate listener)
        {
            if (listener == null) return new ArgumentNullException("listener");

            var listenerParams = listener.GetMethodInfo().GetParameters();

            if (listenerParams == null || listenerParams.Length != 3 ||
                listenerParams[0].ParameterType != typeof(User) ||
                listenerParams[1].ParameterType != typeof(User) ||
                listenerParams[2].ParameterType != typeof(UserRoomAccess))
            {
                return new TargetException("This chat event takes three arguments of type (in order): 'User', 'User' & 'UserRoomAccess'.");
            }

            return null;
        }

        public void Execute(Room room, ref EventManager evMan, Dictionary<string, object> data)
        {
            var granterID = int.Parse(data["user_id"].ToString());
            var targetUserID = int.Parse(data["target_user_id"].ToString());
            var content = data["content"].ToString();
            content = content.Substring(1, content.Length - 2);
            var granter = room.GetUser(granterID);
            var targetUser = room.GetUser(targetUserID);
            var newAccessLevel = UserRoomAccess.Normal;

            switch (content)
            {
                case "Access now owner":
                {
                    newAccessLevel = UserRoomAccess.Owner;
                    break;
                }
                case "Access now read-write":
                {
                    newAccessLevel = UserRoomAccess.ExplicitReadWrite;
                    break;
                }
                case "Access now read-only":
                {
                    newAccessLevel = UserRoomAccess.ExplicitReadOnly;
                    break;
                }
            }

            evMan.CallListeners(EventType.UserAccessLevelChanged, granterID == room.Me.ID, granter, targetUser, newAccessLevel);
        }
    }
}
