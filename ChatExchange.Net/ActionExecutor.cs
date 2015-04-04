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
        private readonly ConcurrentDictionary<int, ChatAction> queuedActions = new ConcurrentDictionary<int, ChatAction>();
        private readonly Dictionary<ActionType, int> queuePriority = new Dictionary<ActionType, int>();
        private readonly Thread consumerThread;
        private bool dispose;
        private bool disposed;
        private delegate void ActionCompletedEventHandler(int actionKey, object returnedData);
        private event ActionCompletedEventHandler ActionCompleted;



        public ActionExecutor(Dictionary<ActionType, int> queueProcessingPriority = null)
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
                var key = queuedActions.Keys.Min();
                var action = queuedActions[key];

                var data = action.Action.DynamicInvoke();

                ActionCompleted(key, data);

                ChatAction temp;
                queuedActions.TryRemove(key, out temp);
            }
        }
    }
}
