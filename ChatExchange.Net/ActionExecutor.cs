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
        private readonly ConcurrentDictionary<uint, ChatAction> queuedActions = new ConcurrentDictionary<uint, ChatAction>();
        private readonly Dictionary<ActionType, uint> queuePriority = new Dictionary<ActionType, uint>();
        private readonly Thread consumerThread;
        private bool dispose;
        private bool disposed;
        private delegate void ActionCompletedEventHandler(uint actionKey, object returnedData);
        private event ActionCompletedEventHandler ActionCompleted;



        public ActionExecutor(Dictionary<ActionType, uint> queueProcessingPriority = null)
        {
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

            var dataReady = false;
            var data = new object();
            ActionCompleted += (k, d) =>
            {
                if (k == key)
                {
                    data = d;
                    dataReady = true;
                }
            };

            queuedActions[key] = action;

            while (!dataReady) { Thread.Sleep(100); }

            return data;
        }



        private void ProcessQueue()
        {
            while (!dispose)
            {
                Thread.Sleep(100);

                if (queuedActions.IsEmpty) { continue; }
                //var key = queuedActions.Keys.Min();
                //var action = queuedActions[key];
                var action = GetNextAction();

                var data = action.Value.Action.DynamicInvoke();

                ActionCompleted(action.Key, data);

                ChatAction temp;
                queuedActions.TryRemove(action.Key, out temp);
            }
        }

        private KeyValuePair<uint, ChatAction> GetNextAction()
        {
            if (queuePriority.Count == 0)
            {
                var key = queuedActions.Keys.Min();
                var action = queuedActions[key];
                return new KeyValuePair<uint, ChatAction>(key, action);
            }
            else
            {
                var lowPrty = queuePriority.Values.Max();
                var highPrty = queuePriority.Values.Min();
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
                {
                    foreach (var a in queuedActions)
                    {
                        if (priorities[i] == a.Value.Type)
                        {
                            return a;
                        }
                    }
                }
            }

            // Something seriously went wrong.
            throw new Exception("You're so screwed.");
        }
    }
}
