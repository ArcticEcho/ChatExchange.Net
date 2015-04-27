using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;



namespace ChatExchangeDotNet
{
    internal class ActionExecutor : IDisposable
    {
        private readonly ConcurrentDictionary<long, ChatAction> queuedActions = new ConcurrentDictionary<long, ChatAction>();
        private readonly Dictionary<ActionType, uint> queuePriority = new Dictionary<ActionType, uint>();
        private readonly Thread consumerThread;
        private readonly EventManager evMan;
        private bool dispose;
        private bool disposed;
        private delegate void ActionCompletedEventHandler(long actionKey, object returnedData);
        private event ActionCompletedEventHandler ActionCompleted;



        public ActionExecutor(ref EventManager evMan, Dictionary<ActionType, uint> queueProcessingPriority = null)
        {
            this.evMan = evMan;

            if (queueProcessingPriority != null && queueProcessingPriority.Keys.Count != 0)
            {
                queuePriority = queueProcessingPriority;
            }

            consumerThread = new Thread(ProcessQueue) { IsBackground = true };
            consumerThread.Start();
        }

        ~ActionExecutor()
        {
            if (!disposed)
            {
                Dispose();
            }
        }



        public void Dispose()
        {
            if (disposed) { return; }

            GC.SuppressFinalize(this);
            dispose = true;
            while (consumerThread.IsAlive) { Thread.Sleep(100); }
            disposed = true;
        }

        public object ExecuteAction(ChatAction action)
        {
            if (dispose || disposed) { return null; }

            var key = queuedActions.Keys.Count == 0 ? 0 : queuedActions.Keys.Max() + 1;

            var reset = new ManualResetEvent(false);
            var data = new object();
            ActionCompleted += (k, d) =>
            {
                if (k == key)
                {
                    data = d;
                    reset.Set();
                }
            };

            queuedActions[key] = action;
            reset.WaitOne();
            ChatAction temp;
            queuedActions.TryRemove(key, out temp);
            return data;
        }



        private void ProcessQueue()
        {
            while (!dispose)
            {
                Thread.Sleep(100);

                if (queuedActions.IsEmpty) { continue; }

                var action = new KeyValuePair<long, ChatAction>(long.MinValue, null);
                object data = null;
                try
                {
                    action = GetNextAction();
                    data = action.Value.Action.DynamicInvoke();
                }
                catch (Exception ex)
                {
                    evMan.CallListeners(EventType.InternalException, ex);
                }
                finally
                {
                    if (action.Key == long.MinValue)
                    {
                        // Something really (REALLY) bad happened, clear the queue and log the error.
                        foreach (var item in queuedActions)
                        {
                            ActionCompleted(item.Key, null);
                        }

                        evMan.CallListeners(EventType.InternalException, new Exception("An unknown has occurred; all queued actions have been cleared."));
                    }
                    else
                    {
                        ActionCompleted(action.Key, data);
                    }
                }
            }
        }

        private KeyValuePair<long, ChatAction> GetNextAction()
        {
            if (queuePriority.Count == 0)
            {
                var key = queuedActions.Keys.Min();
                var action = queuedActions[key];
                return new KeyValuePair<long, ChatAction>(key, action);
            }
            else
            {
                var priorities = new List<ActionType>();

                foreach (var p in queuePriority)
                {
                    if (priorities.Count == 0 || p.Key > priorities[0])
                    {
                        priorities.Add(p.Key);
                    }
                    else
                    {
                        priorities.IndexOf(p.Key, 0);
                    }
                }

                for (var i = 0; i < priorities.Count; i++)
                foreach (var a in queuedActions)
                {
                    if (priorities[i] == a.Value.Type) { return a; }
                }
            }

            // Something seriously went wrong.
            throw new Exception("Congratulations! You've broke my library!.");
        }
    }
}
