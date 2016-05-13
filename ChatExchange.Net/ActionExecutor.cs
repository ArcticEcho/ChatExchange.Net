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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace ChatExchangeDotNet
{
    using ActionPair = KeyValuePair<long, ChatAction>;

    internal class ActionExecutor : IDisposable
    {
        private readonly ConcurrentDictionary<long, ChatAction> queuedActions = new ConcurrentDictionary<long, ChatAction>();
        private readonly ManualResetEvent consumerClosed = new ManualResetEvent(false);
        private readonly Thread consumerThread;
        private readonly EventManager evMan;
        private Action<long, object> ActionCompleted;
        private bool disposed;



        public ActionExecutor(ref EventManager evMan)
        {
            this.evMan = evMan;

            consumerThread = new Thread(ProcessQueue) { IsBackground = true };
            consumerThread.Start();
        }

        ~ActionExecutor()
        {
            Dispose();
        }



        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (consumerClosed != null)
            {
                consumerClosed.WaitOne();
                consumerClosed.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public object ExecuteAction(ChatAction action)
        {
            if (disposed) return null;

            var key = queuedActions.Keys.Count == 0 ? 0 : queuedActions.Keys.Max() + 1;
            var mre = new ManualResetEvent(false);
            object data = null;
            var evFunc = new Action<long, object>((k, d) =>
            {
                if (k == key)
                {
                    data = d;
                    // Action completed, notify the waiting thread.
                    mre.Set();
                }
            });

            ActionCompleted += evFunc;

            // Add the action to the queue for processing.
            queuedActions[key] = action;

            // Wait for the action to be completed.
            mre.WaitOne();

            // The action's been processed; remove it from the queue.
            ChatAction temp;
            queuedActions.TryRemove(key, out temp);
            ActionCompleted -= evFunc;

            return data;
        }



        private void ProcessQueue()
        {
            while (!disposed)
            {
                Thread.Sleep(50);

                if (queuedActions.IsEmpty) continue;

                var action = new ActionPair(long.MinValue, null);
                object data = null;

                try
                {
                    action = GetNextAction();
                    data = action.Value.Action.DynamicInvoke();
                }
                catch (Exception ex)
                {
                    evMan.CallListeners(EventType.InternalException, false, ex);
                }
                finally
                {
                    NotifyCaller(action, data);
                }
            }

            consumerClosed.Set();
        }

        private void NotifyCaller(ActionPair action, object data)
        {
            if (action.Key == long.MinValue) // A catastrophic failure occurred, clear the queue and log the error.
            {
                foreach (var item in queuedActions)
                {
                    ActionCompleted(item.Key, null);
                }

                evMan.CallListeners(EventType.InternalException, false, new Exception("An unknown error has occurred; all queued actions have been cleared."));
            }
            else
            {
                ActionCompleted(action.Key, data);
            }
        }

        private ActionPair GetNextAction()
        {
            var key = queuedActions.Keys.Min();
            var action = queuedActions[key];

            return new ActionPair(key, action);
        }
    }
}
