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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ChatExchangeDotNet.EventListeners;

namespace ChatExchangeDotNet
{
    /// <summary>
    /// Provides a means of listening to chat events by "connecting listeners"
    /// (Delegates) to event types.
    /// </summary>
    public class EventManager : IDisposable
    {
        private readonly ConcurrentDictionary<EventType, IEventListener> events;
        private readonly ConcurrentDictionary<Guid, TrackedObject> trackDict;
        private bool disposed;

        /// <summary>
        /// Returns the current collection of connected Delegates.
        /// </summary>
        public ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>> ConnectedListeners { get; private set; }

        /// <summary>
        /// If true, actions by the currently logged in user will not raise any events. Default set to true.
        /// </summary>
        public bool IgnoreOwnEvents { get; set; } = true;



        internal EventManager()
        {
            events = new ConcurrentDictionary<EventType, IEventListener>();
            trackDict = new ConcurrentDictionary<Guid, TrackedObject>();

            var types = typeof(EventManager).GetTypeInfo().Assembly.GetTypes();
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

#pragma warning disable CS1591
        ~EventManager()
        {
            Dispose();
        }
#pragma warning restore CS1591



        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            if (ConnectedListeners != null)
                ConnectedListeners.Clear();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Registers a Delegate to the specified event type.
        /// </summary>
        /// <param name="eventType">The event type to listen to.</param>
        /// <param name="listener">The Delegate to invoke upon event activity.</param>
        /// <exception cref="System.ArgumentException">
        /// Thrown if the Delegate is already registered to the specified event type.
        /// </exception>
        public void ConnectListener(EventType eventType, Delegate listener)
        {
            if (disposed) return;
            var ex = events[eventType].CheckListener(listener);
            if (ex != null) throw ex;

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

        /// <summary>
        /// Unregisters a Delegate from the specified event type.
        /// </summary>
        /// <param name="eventType">
        /// The event type to which the Delegate was registered to.
        /// </param>
        /// <param name="listener">The Delegate to unregister.</param>
        public void DisconnectListener(EventType eventType, Delegate listener)
        {
            if (disposed) return;
            if (!ConnectedListeners.ContainsKey(eventType)) throw new KeyNotFoundException();
            if (!ConnectedListeners[eventType].Values.Contains(listener)) throw new KeyNotFoundException();

            var key = ConnectedListeners[eventType].Where(x => x.Value == listener).First().Key;
            Delegate temp;
            ConnectedListeners[eventType].TryRemove(key, out temp);
        }



        internal Guid TrackMessage(Message message, bool primaryOnly)
        {
            if (message == null) throw new ArgumentNullException("message");

            var id = Guid.NewGuid();
            var obj = new TrackedObject
            {
                Object = message,
                ID = id,
                Listeners = new Dictionary<EventType, Delegate>
                {
                    [EventType.MessageEdited] = new Action<Message>(m =>
                    {
                        if (m.ID == message.ID && !(message?.DisposeObject ?? true))
                        {
                            message.Content = m.Content;
                            message.EditCount++;
                        }
                    }),
                    [EventType.MessageDeleted] = new Action<User, int>((u, mID) =>
                    {
                        if (mID == message.ID && !(message?.DisposeObject ?? true))
                        {
                            message.IsDeleted = true;
                        }
                    })
                }
            };

            if (!primaryOnly)
            {
                obj.Listeners[EventType.MessageStarToggled] = new Action<Message, int, int>((m, s, p) =>
                {
                    if (m.ID == message.ID && !(message?.DisposeObject ?? true))
                    {
                        message.StarCount = s;
                        message.PinCount = p;
                    }
                });
            }

            trackDict[id] = obj;

            return id;
        }

        internal Guid TrackUser(User user)
        {
            if (user == null) throw new ArgumentNullException("user");

            var id = Guid.NewGuid();
            var obj = new TrackedObject
            {
                Object = user,
                ID = id,
                Listeners = new Dictionary<EventType, Delegate>
                {
                    [EventType.UserAccessLevelChanged] = new Action<User, User, UserRoomAccess>((granter, targetUser, newAccess) =>
                    {
                        if (targetUser.ID == user?.ID)
                        {
                            user.IsRoomOwner = newAccess == UserRoomAccess.Owner;
                        }
                    })
                }
            };

            trackDict[id] = obj;

            return id;
        }

        internal Guid TrackRoomMetaInfo(RoomMetaInfo meta)
        {
            if (meta == null) throw new ArgumentNullException("meta");

            var id = Guid.NewGuid();
            var obj = new TrackedObject
            {
                Object = meta,
                ID = id,
                Listeners = new Dictionary<EventType, Delegate>
                {
                    [EventType.MessagePosted] = new Action<Message>(msg =>
                    {
                        if (meta != null)
                        {
                            meta.AllTimeMessages++;
                            meta.LastMessage = DateTime.UtcNow;
                        }
                    }),
                    [EventType.MessageMovedOut] = new Action<Message>(msg =>
                    {
                        if (meta != null)
                        {
                            meta.AllTimeMessages--;
                        }
                    }),
                    [EventType.MessageMovedIn] = new Action<Message>(msg =>
                    {
                        if (meta != null)
                        {
                            meta.AllTimeMessages++;
                        }
                    }),
                    [EventType.RoomMetaChanged] = new Action<User, string, string, string[]>((u, n, d, t) =>
                    {
                        if (meta != null)
                        {
                            meta.Name = n;
                            meta.Description = d;
                            meta.Tags = t;
                        }
                    })
                }
            };

            trackDict[id] = obj;

            return id;
        }

        internal Guid TrackRoom(Room room)
        {
            if (room == null) throw new ArgumentNullException("room");

            var id = Guid.NewGuid();
            var obj = new TrackedObject
            {
                Object = room,
                ID = id,
                Listeners = new Dictionary<EventType, Delegate>
                {
                    [EventType.UserEntered] = new Action<User>(user =>
                    {
                        if (room != null)
                        {
                            if (!room.PingableUsers.Contains(user))
                            {
                                room.PingableUsers.Add(user);
                            }

                            if (!room.CurrentUsers.Contains(user))
                            {
                                room.CurrentUsers.Add(user);
                            }
                        }
                    }),
                    [EventType.UserLeft] = new Action<User>(user =>
                    {
                        if (room != null && room.CurrentUsers.Contains(user))
                        {
                            room.CurrentUsers.Remove(user);
                        }
                    }),
                    [EventType.UserAccessLevelChanged] = new Action<User, User, UserRoomAccess>((granter, targetUser, newAccess) =>
                    {
                        if (room != null)
                        {
                            if (room.RoomOwners.Contains(targetUser) && newAccess != UserRoomAccess.Owner)
                            {
                                room.RoomOwners.Remove(targetUser);
                            }

                            if (!room.RoomOwners.Contains(targetUser) && newAccess == UserRoomAccess.Owner)
                            {
                                room.RoomOwners.Add(targetUser);
                            }
                        }
                    })
                }
            };

            trackDict[id] = obj;

            return id;
        }

        internal void UntrackObject(Guid trackID)
        {
            if (trackID == null) throw new ArgumentNullException("trackID");
            if (!trackDict.ContainsKey(trackID)) return; // Just for now. Until I can find the bug responsible for this.

            TrackedObject temp;
            trackDict.TryRemove(trackID, out temp);
        }

        internal void CallListeners(EventType eventType, bool selfCaused, params object[] args)
        {
            if (disposed) return;

            // Notify relevant object listeners first (in a separate thread,
            // as we could cause a delay if there a mass of objects to notify).
            Task.Run(() =>
            {
                foreach (var obj in trackDict.Values)
                {
                    if (!obj.Listeners.ContainsKey(eventType)) continue;

                    Task.Run(() =>
                    {
                        InvokeListener(obj.Listeners[eventType], eventType, args);
                    });
                }
            });

            if (IgnoreOwnEvents && selfCaused) return;
            if (!ConnectedListeners.ContainsKey(eventType)) return;
            if (ConnectedListeners[eventType].Keys.Count == 0) return;

            foreach (var listener in ConnectedListeners[eventType].Values)
            {
                Task.Run(() =>
                {
                    InvokeListener(listener, eventType, args);
                });
            }
        }

        internal void HandleEvent(EventType eventType, Room room, ref EventManager evMan, Dictionary<string, object> data) =>
            events[eventType].Execute(room, ref evMan, data);



        private void InvokeListener(Delegate del, EventType ev, params object[] args)
        {
            try
            {
                del.DynamicInvoke(args);
            }
            catch (Exception ex)
            {
                if (ev == EventType.InternalException) throw ex; // Avoid infinite loop.
                CallListeners(EventType.InternalException, false, ex);
            }
        }
    }
}
