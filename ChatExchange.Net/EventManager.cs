using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ChatExchangeDotNet
{
    public class EventManager
    {
        public ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>> EventDirectory { get; private set; }



        public EventManager()
        {
            EventDirectory = new ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>>();
        }



        internal void CallListeners(EventType eventType, params object[] args)
        {
            if (!EventDirectory.ContainsKey(eventType)) { return; }
            if (EventDirectory[eventType].Keys.Count == 0) { return; }

            foreach (var listener in EventDirectory[eventType].Values)
            {
                try
                {
                    listener.DynamicInvoke(args);
                }
                catch (Exception ex)
                {
                    if (eventType == EventType.InternalException) { continue; } // Avoid infinite loop.
                    CallListeners(EventType.InternalException, ex);
                }
            }
        }

        public void ConnectListener(EventType eventType, Delegate listener)
        {
            if (!EventDirectory.ContainsKey(eventType))
            {
                EventDirectory[eventType] = new ConcurrentDictionary<int, Delegate>();
                EventDirectory[eventType][0] = listener;
            }
            else
            {
                if (EventDirectory[eventType].Values.Contains(listener)) 
                { 
                    throw new Exception("'listener' has already been connected to this event type.");
                }
                var index = EventDirectory[eventType].Keys.Max() + 1;
                EventDirectory[eventType][index] = listener;
            }
        }

        public void UpdateListener(EventType eventType, Delegate oldListener, Delegate newListener)
        {
            if (!EventDirectory.ContainsKey(eventType)) { throw new KeyNotFoundException(); }
            if (!EventDirectory[eventType].Values.Contains(oldListener)) { throw new KeyNotFoundException(); }

            var index = EventDirectory[eventType].Where(kv => kv.Value == oldListener).First().Key;
            EventDirectory[eventType][index] = newListener;
        }

        public void DisconnectListener(EventType eventType, Delegate listener)
        {
            if (!EventDirectory.ContainsKey(eventType)) { throw new KeyNotFoundException(); }
            if (!EventDirectory[eventType].Values.Contains(listener)) { throw new KeyNotFoundException(); }

            var key = EventDirectory[eventType].Where(x => x.Value == listener).First().Key;
            Delegate temp;
            EventDirectory[eventType].TryRemove(key, out temp);
        }
    }
}
