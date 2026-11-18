using System;
using System.Text.RegularExpressions;
using KnockRush.Core.Events;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockRush.Tests.EditMode
{
    public sealed class EventBusTests
    {
        private readonly struct TestEvent
        {
            public readonly int Value;

            public TestEvent(int value)
            {
                Value = value;
            }
        }

        [SetUp]
        public void SetUp()
        {
            EventBus.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Reset();
        }

        [Test]
        public void Publish_InvokesSubscriberWithPayload()
        {
            var received = 0;
            EventBus.Subscribe<TestEvent>(e => received = e.Value);

            EventBus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var calls = 0;
            Action<TestEvent> handler = _ => calls++;
            EventBus.Subscribe(handler);
            EventBus.Unsubscribe(handler);

            EventBus.Publish(new TestEvent(1));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void DuplicateSubscribe_IsIgnored()
        {
            var calls = 0;
            Action<TestEvent> handler = _ => calls++;
            EventBus.Subscribe(handler);
            EventBus.Subscribe(handler);

            EventBus.Publish(new TestEvent(1));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, EventBus.SubscriberCount<TestEvent>());
        }

        [Test]
        public void UnsubscribeDuringPublish_DoesNotSkipOtherHandlers()
        {
            var secondCalled = false;
            Action<TestEvent> first = null;
            first = _ => EventBus.Unsubscribe(first);
            EventBus.Subscribe(first);
            EventBus.Subscribe<TestEvent>(_ => secondCalled = true);

            EventBus.Publish(new TestEvent(1));

            Assert.IsTrue(secondCalled);
            Assert.AreEqual(1, EventBus.SubscriberCount<TestEvent>());
        }

        [Test]
        public void ThrowingHandler_IsLogged_AndOthersStillRun()
        {
            var secondCalled = false;
            EventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            EventBus.Subscribe<TestEvent>(_ => secondCalled = true);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            EventBus.Publish(new TestEvent(1));

            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void Reset_RemovesAllSubscribers()
        {
            EventBus.Subscribe<TestEvent>(_ => { });

            EventBus.Reset();

            Assert.AreEqual(0, EventBus.SubscriberCount<TestEvent>());
        }
    }
}
