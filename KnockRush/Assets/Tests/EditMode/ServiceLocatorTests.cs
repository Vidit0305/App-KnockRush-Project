using System;
using KnockRush.Core.Services;
using NUnit.Framework;

namespace KnockRush.Tests.EditMode
{
    public sealed class ServiceLocatorTests
    {
        private interface IFakeService
        {
        }

        private sealed class FakeService : IFakeService
        {
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Reset();
        }

        [Test]
        public void Register_ThenGet_ReturnsSameInstance()
        {
            var service = new FakeService();
            ServiceLocator.Register<IFakeService>(service);

            Assert.AreSame(service, ServiceLocator.Get<IFakeService>());
        }

        [Test]
        public void Register_Twice_Throws()
        {
            ServiceLocator.Register<IFakeService>(new FakeService());

            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Register<IFakeService>(new FakeService()));
        }

        [Test]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ServiceLocator.Register<IFakeService>(null));
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<IFakeService>());
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(ServiceLocator.TryGet<IFakeService>(out var service));
            Assert.IsNull(service);
        }

        [Test]
        public void Unregister_RemovesService()
        {
            ServiceLocator.Register<IFakeService>(new FakeService());

            Assert.IsTrue(ServiceLocator.Unregister<IFakeService>());
            Assert.IsFalse(ServiceLocator.IsRegistered<IFakeService>());
        }

        [Test]
        public void Services_AreKeyedByRegisteredType()
        {
            ServiceLocator.Register<IFakeService>(new FakeService());

            Assert.IsTrue(ServiceLocator.IsRegistered<IFakeService>());
            Assert.IsFalse(ServiceLocator.IsRegistered<FakeService>());
        }
    }
}
