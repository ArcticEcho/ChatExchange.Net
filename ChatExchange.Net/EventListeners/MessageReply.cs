﻿/*
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
using System.Reflection;

namespace ChatExchangeDotNet.EventListeners
{
    internal class MessageReply : IEventListener
    {
        public Exception CheckListener(Delegate listener)
        {
            if (listener == null) { return new ArgumentNullException("listener"); }

            var listenerParams = listener.Method.GetParameters();

            if (listenerParams == null || listenerParams.Length != 1 || listenerParams[0].ParameterType != typeof(Message))
            {
                return new TargetException("This chat event takes a single argument of type 'Message'.");
            }

            return null;
        }
    }
}