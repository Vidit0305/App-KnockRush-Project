using System.Collections.Generic;
using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// Central driver for every time-driven obstacle.
    ///
    /// Offline, a hidden driver poses all obstacles at the fixed timestep (for physics) and again
    /// every frame at render time (for smooth visuals). In multiplayer (Phase 4) the network
    /// simulation sets <see cref="ExternallyDriven"/> and calls <see cref="PoseAll"/> with the
    /// tick time before each (re)simulated tick, so clients predict obstacles exactly.
    /// </summary>
    public static class ObstacleSimulation
    {
        private static readonly List<TimeDrivenObstacle> Obstacles = new List<TimeDrivenObstacle>();
        private static GameObject driverObject;

        /// <summary>Time offset so obstacle time starts at 0 when a round starts.</summary>
        public static double RoundStartTime { get; set; }

        /// <summary>When true, the offline driver stops and the owner (network runner) poses obstacles.</summary>
        public static bool ExternallyDriven { get; set; }

        public static int Count => Obstacles.Count;

        public static IReadOnlyList<TimeDrivenObstacle> All => Obstacles;

        public static void Register(TimeDrivenObstacle obstacle)
        {
            if (!Obstacles.Contains(obstacle))
            {
                Obstacles.Add(obstacle);
            }

            EnsureDriver();
        }

        public static void Unregister(TimeDrivenObstacle obstacle)
        {
            Obstacles.Remove(obstacle);
        }

        /// <summary>Poses every obstacle for <paramref name="absoluteTime"/> and syncs physics.</summary>
        public static void PoseAll(double absoluteTime)
        {
            var time = absoluteTime - RoundStartTime;
            for (var i = 0; i < Obstacles.Count; i++)
            {
                Obstacles[i].ApplyTime(time);
            }

            if (Obstacles.Count > 0)
            {
                Physics.SyncTransforms();
            }
        }

        /// <summary>Restarts obstacle time at zero (called when a round begins).</summary>
        public static void ResetClock(double absoluteNow)
        {
            RoundStartTime = absoluteNow;
        }

        private static void EnsureDriver()
        {
            if (driverObject != null || !Application.isPlaying)
            {
                return;
            }

            driverObject = new GameObject("[ObstacleSimulation]");
            driverObject.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(driverObject);
            driverObject.AddComponent<ObstacleSimulationDriver>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Obstacles.Clear();
            driverObject = null;
            RoundStartTime = 0d;
            ExternallyDriven = false;
        }
    }

    /// <summary>Offline driver: physics pose at fixed time, visual pose at render time.</summary>
    [DefaultExecutionOrder(-100)]
    internal sealed class ObstacleSimulationDriver : MonoBehaviour
    {
        private void FixedUpdate()
        {
            if (!ObstacleSimulation.ExternallyDriven)
            {
                ObstacleSimulation.PoseAll(Time.fixedTimeAsDouble);
            }
        }

        private void Update()
        {
            if (!ObstacleSimulation.ExternallyDriven)
            {
                var all = ObstacleSimulation.All;
                var time = Time.timeAsDouble - ObstacleSimulation.RoundStartTime;
                for (var i = 0; i < all.Count; i++)
                {
                    all[i].ApplyTime(time);
                }
            }
        }
    }
}
