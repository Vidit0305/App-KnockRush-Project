using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnockRush.Core.Services
{
    /// <summary>
    /// Minimal typed service registry. Services are registered once by <see cref="AppRoot"/>
    /// (usually under an interface type) and resolved by gameplay / UI code.
    ///
    /// Rules:
    /// - Register under the interface (e.g. ISceneLoader), not the concrete class.
    /// - Resolve services in Start/OnEnable or lazily, never in static constructors.
    /// - Do not use this for per-player or per-match objects; those live in the match scope.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            var key = typeof(T);
            if (Services.ContainsKey(key))
            {
                throw new InvalidOperationException($"Service '{key.Name}' is already registered.");
            }

            Services.Add(key, service);
        }

        public static bool Unregister<T>() where T : class
        {
            return Services.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException(
                $"Service '{typeof(T).Name}' is not registered. Has AppRoot finished initialising? " +
                "Await AppRoot.InitializationTask before resolving services.");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var value))
            {
                service = (T)value;
                return true;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class
        {
            return Services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Removes every registration. Called by AppRoot on shutdown and by tests.
        /// Does NOT dispose services; the owner (AppRoot) is responsible for that.
        /// </summary>
        public static void Reset()
        {
            Services.Clear();
        }

        // Supports "Enter Play Mode Options" with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnPlay()
        {
            Services.Clear();
        }
    }
}
