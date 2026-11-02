using System;
using Morsoul.Core;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class EventBusTests
    {
        struct Hit { public int Value; }
        struct Other { }

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            var bus = new EventBus();
            int received = 0;
            bus.Subscribe<Hit>(e => received = e.Value);
            bus.Publish(new Hit { Value = 7 });
            Assert.AreEqual(7, received);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceiveInOrder()
        {
            var bus = new EventBus();
            string order = "";
            bus.Subscribe<Hit>(e => order += "a");
            bus.Subscribe<Hit>(e => order += "b");
            bus.Publish(new Hit());
            Assert.AreEqual("ab", order);
        }

        [Test]
        public void Publish_OnlyMatchingType_Receives()
        {
            var bus = new EventBus();
            bool hit = false, other = false;
            bus.Subscribe<Hit>(e => hit = true);
            bus.Subscribe<Other>(e => other = true);
            bus.Publish(new Hit());
            Assert.IsTrue(hit);
            Assert.IsFalse(other);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            Action<Hit> handler = e => count++;
            bus.Subscribe(handler);
            bus.Publish(new Hit());
            bus.Unsubscribe(handler);
            bus.Publish(new Hit());
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Publish_NoSubscribers_DoesNotThrow()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new Hit { Value = 1 }));
        }

        [Test]
        public void Publish_HandlerThrows_OthersStillRun_ThenAggregates()
        {
            var bus = new EventBus();
            bool secondRan = false;
            bus.Subscribe<Hit>(e => throw new InvalidOperationException());
            bus.Subscribe<Hit>(e => secondRan = true);
            Assert.Throws<AggregateException>(() => bus.Publish(new Hit()));
            Assert.IsTrue(secondRan);
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            var bus = new EventBus();
            int count = 0;
            bus.Subscribe<Hit>(e => count++);
            bus.Clear();
            bus.Publish(new Hit());
            Assert.AreEqual(0, count);
        }
    }
}
