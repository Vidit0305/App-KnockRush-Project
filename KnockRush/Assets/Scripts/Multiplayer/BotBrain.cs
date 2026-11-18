using System.Collections.Generic;
using System.Linq;
using KnockRush.Core;
using KnockRush.Maps;
using KnockRush.Movement;
using UnityEngine;

namespace KnockRush.Multiplayer
{
    /// <summary>
    /// Server-side bot driver. Produces the same MotorInput a human would, so bots obey exactly
    /// the same movement rules (no cheating). Race maps: run through checkpoints to the finish,
    /// jumping obstacles and gaps. Survival maps: wander between safe points near the centre.
    /// Phase 6 adds hand-authored bot paths per map for smarter routes.
    /// </summary>
    public static class BotBrain
    {
        private sealed class CourseCache
        {
            public MapController Map;
            public Vector3[] Targets;
            public Vector3 Center;
        }

        private static CourseCache cache;

        public static MotorInput Compute(NetworkCharacter bot, int tick, float deltaTime)
        {
            var course = GetCourse();
            if (course == null)
            {
                return MotorInput.None;
            }

            var seed = bot.Object.Id.Raw * 0.6180339f;
            var position = bot.Position;
            var state = bot.State;

            Vector3 target;
            var definition = course.Map.Definition;
            if (definition != null && definition.Category != MapCategory.Race && course.Targets.Length == 0)
            {
                // Survival: orbit around the centre with a per-bot offset.
                var angle = seed * 10f + tick * deltaTime * 0.25f;
                target = course.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
            }
            else
            {
                var next = Mathf.Clamp(bot.CheckpointOrder + 1, 0, course.Targets.Length - 1);
                target = course.Targets[next];
                if (bot.CheckpointOrder + 1 >= course.Targets.Length)
                {
                    target = course.Targets[course.Targets.Length - 1];
                }
            }

            var toTarget = target - position;
            toTarget.y = 0f;
            var lateralNoise = Mathf.Sin(tick * deltaTime * 0.9f + seed) * 0.35f;
            var forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            var steer = (forward + right * lateralNoise).normalized;

            var input = new MotorInput { Move = new Vector2(steer.x, steer.z) };

            // Jump obstacles / ledges in front and gaps below.
            var physics = bot.Runner.GetPhysicsScene();
            var knee = position + Vector3.up * 0.4f;
            var blocked = physics.Raycast(knee, steer, out _, 1.4f, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            var ahead = position + steer * 1.6f + Vector3.up * 0.5f;
            var gapAhead = state.IsGrounded && !physics.Raycast(ahead, Vector3.down, out _, 2.5f, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
            var hop = Mathf.Repeat(tick * deltaTime + seed * 3f, 4.5f) < deltaTime * 1.5f;

            if (blocked || gapAhead || hop)
            {
                input.JumpPressed = state.IsGrounded;
                input.JumpHeld = true;
            }

            // Dive across gaps at the top of the jump.
            if (!state.IsGrounded && state.Velocity.y < 0.5f && state.Mode == MotorMode.Airborne)
            {
                var below = position + Vector3.up * 0.2f;
                var noGroundBelow = !physics.Raycast(below, Vector3.down, out _, 3f, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);
                input.DivePressed = noGroundBelow && Mathf.Repeat(seed * 7f, 1f) > 0.35f;
            }

            return input;
        }

        private static CourseCache GetCourse()
        {
            var map = MapController.Current;
            if (map == null)
            {
                cache = null;
                return null;
            }

            if (cache != null && cache.Map == map)
            {
                return cache;
            }

            var targets = new List<Vector3>();
            targets.AddRange(Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)
                .OrderBy(c => c.Order)
                .Select(c => c.transform.position));
            var finish = Object.FindFirstObjectByType<FinishLine>();
            if (finish != null)
            {
                targets.Add(finish.transform.position);
            }

            var center = Vector3.zero;
            for (var i = 0; i < map.SpawnPointCount; i++)
            {
                center += map.GetSpawnPoint(i).position;
            }

            if (map.SpawnPointCount > 0)
            {
                center /= map.SpawnPointCount;
            }

            cache = new CourseCache { Map = map, Targets = targets.ToArray(), Center = center };
            return cache;
        }
    }
}
