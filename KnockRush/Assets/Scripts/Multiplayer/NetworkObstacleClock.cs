using Fusion;
using KnockRush.Obstacles;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Drives every time-driven obstacle from the network tick instead of local time, so the
    /// server and all predicting clients evaluate exactly the same obstacle pose for each tick
    /// (including during resimulation). Lives on the NetworkRunner's GameObject.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class NetworkObstacleClock : SimulationBehaviour
    {
        private static int lastPosedTick = int.MinValue;
        private static NetworkRunner lastRunner;

        /// <summary>Tick at which obstacle time is zero (set by the round controller in Phase 5).</summary>
        public static int RoundStartTick { get; set; }

        /// <summary>Poses all obstacles for the runner's current tick once (idempotent per tick).</summary>
        public static void EnsurePosed(NetworkRunner runner)
        {
            var tick = runner.Tick.Raw;
            if (tick == lastPosedTick && runner == lastRunner && !runner.IsResimulation)
            {
                return;
            }

            lastPosedTick = tick;
            lastRunner = runner;
            ObstacleSimulation.ExternallyDriven = true;
            ObstacleSimulation.RoundStartTime = 0d;
            ObstacleSimulation.PoseAll((tick - RoundStartTick) * (double)runner.DeltaTime);
        }

        public override void FixedUpdateNetwork()
        {
            EnsurePosed(Runner);
        }

        public override void Render()
        {
            // Smooth visuals between ticks (no physics sync needed for rendering).
            var time = Runner.LocalRenderTime - RoundStartTick * (double)Runner.DeltaTime;
            var all = ObstacleSimulation.All;
            for (var i = 0; i < all.Count; i++)
            {
                all[i].ApplyTime(time);
            }
        }

        private void OnDestroy()
        {
            ObstacleSimulation.ExternallyDriven = false;
            lastPosedTick = int.MinValue;
            lastRunner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastPosedTick = int.MinValue;
            lastRunner = null;
            RoundStartTick = 0;
        }
    }
}
