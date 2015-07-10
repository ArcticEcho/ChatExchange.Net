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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ChatExchangeDotNet.EventListeners;

namespace ChatExchangeDotNet
{
    public class EventManager : IDisposable
    {
        private readonly ConcurrentDictionary<EventType, IEventListener> events;
        private bool disposed;

        public ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>> ConnectedListeners { get; private set; }



        public EventManager()
        {
            events = new ConcurrentDictionary<EventType, IEventListener>();

            var types = Assembly.GetExecutingAssembly().GetTypes();
            var eventTypes = types.Where(t => t.Namespace == "ChatExchangeDotNet.EventListeners");

            foreach (EventType chatEvent in Enum.GetValues(typeof(EventType)))
            {
                var eventName = Enum.GetName(typeof(EventType), chatEvent);
                var type = eventTypes.First(t => t.Name == eventName);
                var instance = (IEventListener)Activator.CreateInstance(type);

                events[chatEvent] = instance;
            }

            ConnectedListeners = new ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>>();
        }

        ~EventManager()
        {
            Dispose();
        }



        public void Dispose()
        {
            if (disposed) { return; }

            disposed = true;
            if (ConnectedListeners != null)
            {
                ConnectedListeners.Clear();
            }
            GC.SuppressFinalize(this);
        }

        public void ConnectListener(EventType eventType, Delegate listener)
        {
            if (disposed) { return; }
            var ex = events[eventType].CheckListener(listener);
            if (ex != null)
            {
                throw ex;
            }

            if (!ConnectedListeners.ContainsKey(eventType))
            {
                ConnectedListeners[eventType] = new ConcurrentDictionary<int, Delegate>();
            }
            else if (ConnectedListeners[eventType].Values.Contains(listener))
            {
                throw new ArgumentException("'listener' has already been connected to this event type.", "listener");
            }

            if (ConnectedListeners[eventType].Count == 0)
            {
                ConnectedListeners[eventType][0] = listener;
            }
            else
            {
                var index = ConnectedListeners[eventType].Keys.Max() + 1;
                ConnectedListeners[eventType][index] = listener;
            }
        }

        public void DisconnectListener(EventType eventType, Delegate listener)
        {
            if (disposed) { return; }
            if (!ConnectedListeners.ContainsKey(eventType)) { throw new KeyNotFoundException(); }
            if (!ConnectedListeners[eventType].Values.Contains(listener)) { throw new KeyNotFoundException(); }

            var key = ConnectedListeners[eventType].Where(x => x.Value == listener).First().Key;
            Delegate temp;
            ConnectedListeners[eventType].TryRemove(key, out temp);
        }



        internal void TrackMessage(Message message)
        {
            if (message == null) { throw new ArgumentNullException("message"); }

            ConnectListener(EventType.MessageEdited, new Action<Message>(m =>
            {
                if (m.ID == message.ID)
                {
                    message.UpdateContent(m.Content);
                }
            }));
        }

        internal void TrackUser(User user)
        {
            if (user == null) { throw new ArgumentNullException("user"); }

            ConnectListener(EventType.UserAccessLevelChanged, new Action<User, User, UserRoomAccess>((granter, targetUser, newAccess) =>
            {
                if (targetUser.ID == user.ID)
                {
                    user.UpdateAccessLevel(newAccess);
                }
            }));
        }

        internal void CallListeners(EventType eventType, params object[] args)
        {
            if (disposed) { return; }
            if (!ConnectedListeners.ContainsKey(eventType)) { return; }
            if (ConnectedListeners[eventType].Keys.Count == 0) { return; }

            foreach (var listener in ConnectedListeners[eventType].Values)
            {
                try
                {
                    Task.Factory.StartNew(() => listener.DynamicInvoke(args));
                }
                catch (Exception ex)
                {
                    if (eventType == EventType.InternalException) { continue; } // Avoid infinite loop.
                    CallListeners(EventType.InternalException, ex);
                }
            }
        }

        internal void HandleEvent(EventType eventType, Room room, ref EventManager evMan, Dictionary<string, object> data)
        {
            events[eventType].Execute(room, ref evMan, data);
        }
    }
}
