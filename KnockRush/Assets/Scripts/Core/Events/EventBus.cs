using System;
using System.Collections.Generic;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Core.Events
{
    /// <summary>
    /// Typed, synchronous, allocation-free (on publish) event bus for decoupled
    /// cross-system notifications (e.g. gameplay -> UI / audio / VFX).
    ///
    /// Events are structs. Publishing iterates an immutable snapshot, so handlers may
    /// subscribe/unsubscribe during dispatch. An exception in one handler is logged and
    /// does not stop the remaining handlers.
    ///
    /// NOTE: This is for LOCAL presentation-side signals. Authoritative networked game state
    /// (eliminations, qualification, scores) is replicated by the network layer (Phase 4+),
    /// never sent through this bus as a source of truth.
    /// </summary>
    public static class EventBus
    {
        private static readonly List<Action> ChannelResetters = new List<Action>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Channel<T>.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            Channel<T>.Remove(handler);
        }

        public static void Publish<T>(T eventData) where T : struct
        {
            Channel<T>.Publish(eventData);
        }

        public static int SubscriberCount<T>() where T : struct
        {
            return Channel<T>.Count;
        }

        /// <summary>Clears every subscriber on every channel. Used on shutdown and in tests.</summary>
        public static void Reset()
        {
            lock (ChannelResetters)
            {
                for (var i = 0; i < ChannelResetters.Count; i++)
                {
                    ChannelResetters[i].Invoke();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            Reset();
        }

        private static class Channel<T> where T : struct
        {
            private static readonly List<Action<T>> Handlers = new List<Action<T>>();
            private static Action<T>[] snapshot = Array.Empty<Action<T>>();
            private static bool dirty;

            static Channel()
            {
                lock (ChannelResetters)
                {
                    ChannelResetters.Add(Clear);
                }
            }

            public static int Count => Handlers.Count;

            public static void Add(Action<T> handler)
            {
                if (Handlers.Contains(handler))
                {
                    return; // Ignore duplicate subscriptions of the same delegate.
                }

                Handlers.Add(handler);
                dirty = true;
            }

            public static void Remove(Action<T> handler)
            {
                if (Handlers.Remove(handler))
                {
                    dirty = true;
                }
            }

            public static void Publish(T eventData)
            {
                if (dirty)
                {
                    snapshot = Handlers.ToArray();
                    dirty = false;
                }

                var current = snapshot;
                for (var i = 0; i < current.Length; i++)
                {
                    try
                    {
                        current[i].Invoke(eventData);
                    }
                    catch (Exception exception)
                    {
                        Log.Exception(exception);
                    }
                }
            }

            private static void Clear()
            {
                Handlers.Clear();
                snapshot = Array.Empty<Action<T>>();
                dirty = false;
            }
        }
    }
}
