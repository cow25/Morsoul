using System;

namespace Morsoul.Core
{
    /// <summary>
    /// Typed publish/subscribe contract shared by all lanes. Events are plain
    /// structs/classes; subscribers register per event type.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Registers a handler for event type <typeparamref name="T"/>.</summary>
        void Subscribe<T>(Action<T> handler);
        /// <summary>Removes a previously registered handler.</summary>
        void Unsubscribe<T>(Action<T> handler);
        /// <summary>Delivers the event to every subscriber of <typeparamref name="T"/>.</summary>
        void Publish<T>(T eventData);
        /// <summary>Drops all subscriptions of all types (scene/teardown reset).</summary>
        void Clear();
    }
}
