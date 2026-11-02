using System;
using System.Collections.Generic;

namespace Morsoul.Core
{
    /// <summary>
    /// Minimal synchronous event bus. Publish runs handlers in subscription
    /// order on the calling thread; a handler that throws does not prevent
    /// remaining handlers from running (exceptions are collected and thrown
    /// as an aggregate after delivery).
    /// </summary>
    public class EventBus : IEventBus
    {
        readonly Dictionary<Type, Delegate> _subscribers =
            new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var key = typeof(T);
            _subscribers.TryGetValue(key, out var existing);
            _subscribers[key] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var key = typeof(T);
            if (!_subscribers.TryGetValue(key, out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _subscribers.Remove(key);
            else _subscribers[key] = remaining;
        }

        public void Publish<T>(T eventData)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var handlers)) return;
            List<Exception> errors = null;
            foreach (Action<T> handler in ((Action<T>)handlers).GetInvocationList())
            {
                try
                {
                    handler(eventData);
                }
                catch (Exception e)
                {
                    (errors ??= new List<Exception>()).Add(e);
                }
            }
            if (errors != null)
                throw new AggregateException("Event handler(s) threw.", errors);
        }

        public void Clear() => _subscribers.Clear();
    }
}
