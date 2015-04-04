using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace ChatExchangeDotNet
{
    public class EventManager
    {
        public ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>> ConnectedListeners { get; private set; }



        public EventManager()
        {
            ConnectedListeners = new ConcurrentDictionary<EventType, ConcurrentDictionary<int, Delegate>>();
        }



        internal void CallListeners(EventType eventType, params object[] args)
        {
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

        public void ConnectListener(EventType eventType, Delegate listener)
        {
            if (!ConnectedListeners.ContainsKey(eventType))
            {
                ConnectedListeners[eventType] = new ConcurrentDictionary<int, Delegate>();
                ConnectedListeners[eventType][0] = listener;
            }
            else
            {
                if (ConnectedListeners[eventType].Values.Contains(listener)) 
                { 
                    throw new Exception("'listener' has already been connected to this event type.");
                }
                var index = ConnectedListeners[eventType].Keys.Max() + 1;
                ConnectedListeners[eventType][index] = listener;
            }
        }

        public void UpdateListener(EventType eventType, Delegate oldListener, Delegate newListener)
        {
            if (!ConnectedListeners.ContainsKey(eventType)) { throw new KeyNotFoundException(); }
            if (!ConnectedListeners[eventType].Values.Contains(oldListener)) { throw new KeyNotFoundException(); }

            var index = ConnectedListeners[eventType].Where(kv => kv.Value == oldListener).First().Key;
            ConnectedListeners[eventType][index] = newListener;
        }

        public void DisconnectListener(EventType eventType, Delegate listener)
        {
            if (!ConnectedListeners.ContainsKey(eventType)) { throw new KeyNotFoundException(); }
            if (!ConnectedListeners[eventType].Values.Contains(listener)) { throw new KeyNotFoundException(); }

            var key = ConnectedListeners[eventType].Where(x => x.Value == listener).First().Key;
            Delegate temp;
            ConnectedListeners[eventType].TryRemove(key, out temp);
        }
    }
}
