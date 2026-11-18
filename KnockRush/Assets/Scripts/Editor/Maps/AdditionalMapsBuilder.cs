using System.Collections.Generic;
using KnockRush.Maps;
using KnockRush.Obstacles;
using UnityEngine;
using static KnockRush.EditorTools.Scenes.SceneBuildKit;

namespace KnockRush.EditorTools.Maps
{
    /// <summary>
    /// Generates the remaining seven original maps (Phase 6). Each is a content-only scene with a
    /// MapController + MapBootstrap, built from the shared obstacle library.
    /// </summary>
    public static class AdditionalMapsBuilder
    {
        public static void BuildAll()
        {
            SlimeMountain();
            SkyHigh();
            SpinAttack();
            FallingTiles();
            PendulumPanic();
            CrownClimber();
            LastPlatform();
        }

        // =========================================================================================
        // RACE 2: SLIME MOUNTAIN: slippery climb with rolling balls, bounce-pad ledges, conveyor
        // =========================================================================================
        public static void SlimeMountain()
        {
            var c = MapBuildContext.Begin("SlimeMountain", "slime_mountain", "Slime Mountain",
                "Slip up gooey slopes, dodge rolling boulders and bounce your way to the summit.",
                MapCategory.Race, FallRule.RespawnAtCheckpoint, 3, 200f, new Color32(0x8B, 0xE0, 0x3C, 0xFF), Quaternion.Euler(45f, -40f, 0f));
            var slime = c.Mat("Slime", new Color32(0x8B, 0xE0, 0x3C, 0xFF));
            var rock = c.Mat("Rock", new Color32(0xC9, 0x8A, 0x4B, 0xFF));
            var start = c.Mat("Start", new Color32(0x1F, 0x8A, 0x5B, 0xFF));
            var goo = c.Mat("Goo", new Color32(0x6E, 0xE7, 0xC0, 0xFF));
            var finish = c.Mat("Finish", new Color32(0xFF, 0xD1, 0x2B, 0xFF));

            c.Deck("Start", new Vector3(0f, 0f, -1f), new Vector3(26f, 1f, 22f), start);
            var spawns = SpawnGrid(c.Gameplay, new Vector3(0f, 0f, -2f), 8, 4, 2.6f, 2.2f, 0f);

            var ramp = Ramp(c.Level, "SlimeRamp", new Vector3(0f, 0f, 10f), 14f, 30f, 12f, slime);
            MapBuildContext.Surface(ramp, 0.3f, Vector3.zero);
            var rampTop = 30f * Mathf.Sin(12f * Mathf.Deg2Rad);
            var rampEnd = 10f + 30f * Mathf.Cos(12f * Mathf.Deg2Rad);
            for (var i = 0; i < 3; i++)
            {
                var ball = c.Obstacle("RollingBall", new Vector3(-4f + i * 4f, rampTop + 1.6f, rampEnd - 0.5f), name: $"Boulder_{i}");
                MapBuildContext.Set(ball.GetComponent<LoopingMover>(), "offset", new Vector3(0f, -rampTop, -(rampEnd - 10f)));
                c.SetPhase(ball, i * 1.7f);
            }

            c.Deck("Plateau", new Vector3(0f, rampTop, 47f), new Vector3(16f, 1f, 16f), rock);
            AddCheckpoint(c.Gameplay, 1, new Vector3(0f, rampTop + 1.5f, 41f), new Vector3(16f, 4f, 2f), 12f);

            var ledges = new[] { (z: 62f, y: rampTop + 2.5f), (z: 74f, y: rampTop + 5f) };
            c.Obstacle("BouncePad", new Vector3(-4f, rampTop, 53f));
            c.Obstacle("BouncePad", new Vector3(4f, rampTop, 53f));
            foreach (var ledge in ledges)
            {
                c.Deck($"Ledge_{ledge.z}", new Vector3(0f, ledge.y, ledge.z), new Vector3(14f, 1f, 10f), rock);
                c.Obstacle("BouncePad", new Vector3(-3f, ledge.y, ledge.z + 3f));
                c.Obstacle("BouncePad", new Vector3(3f, ledge.y, ledge.z + 3f));
            }

            var plateauY = rampTop + 7.5f;
            var conveyor = c.Deck("GooConveyor", new Vector3(0f, plateauY, 100f), new Vector3(16f, 1f, 40f), goo);
            MapBuildContext.Surface(conveyor, 0.5f, new Vector3(0f, 0f, -2.5f));
            AddCheckpoint(c.Gameplay, 2, new Vector3(0f, plateauY + 1.5f, 82f), new Vector3(16f, 4f, 2f), 12f);
            c.Obstacle("Bumper", new Vector3(-4f, plateauY, 95f));
            c.Obstacle("Bumper", new Vector3(4f, plateauY, 105f));
            c.Obstacle("Bumper", new Vector3(0f, plateauY, 112f));
            AddCheckpoint(c.Gameplay, 3, new Vector3(0f, plateauY + 1.5f, 118f), new Vector3(16f, 4f, 2f), 12f);

            var finalRamp = Ramp(c.Level, "FinalSlime", new Vector3(0f, plateauY, 120f), 12f, 20f, 10f, slime);
            MapBuildContext.Surface(finalRamp, 0.4f, Vector3.zero);
            var summitY = plateauY + 20f * Mathf.Sin(10f * Mathf.Deg2Rad);
            var summitZ = 120f + 20f * Mathf.Cos(10f * Mathf.Deg2Rad);
            var lastBall = c.Obstacle("RollingBall", new Vector3(0f, summitY + 1.6f, summitZ - 0.5f), name: "Boulder_Final");
            MapBuildContext.Set(lastBall.GetComponent<LoopingMover>(), "offset", new Vector3(0f, -(summitY - plateauY), -(summitZ - 120f)));
            MapBuildContext.Set(lastBall.GetComponent<LoopingMover>(), "travelSeconds", 4f);

            c.Deck("Summit", new Vector3(0f, summitY, summitZ + 7f), new Vector3(14f, 1f, 15f), finish);
            AddFinishLine(c.Gameplay, new Vector3(0f, summitY + 2f, summitZ + 9f), new Vector3(14f, 4f, 1f));

            c.Finish(spawns, -10f, new Bounds(new Vector3(0f, 5f, 75f), new Vector3(30f, 20f, 170f)),
                c.Mat("IslandTop", new Color32(0x8B, 0xE0, 0x3C, 0xFF)), c.Mat("IslandRock", new Color32(0xA8, 0x83, 0x62, 0xFF)), 11);
        }

        // =========================================================================================
        // RACE 3: SKY HIGH: floating platforms, disappearing bridge, moving platforms, risky beam
        // =========================================================================================
        public static void SkyHigh()
        {
            var c = MapBuildContext.Begin("SkyHigh", "sky_high", "Sky High",
                "Hop across floating islands in the clouds. Bridges vanish, platforms drift and the shortcut is a long way down.",
                MapCategory.Race, FallRule.RespawnAtCheckpoint, 3, 180f, new Color32(0x2D, 0x9C, 0xFF, 0xFF), Quaternion.Euler(50f, -30f, 0f));
            var cloud = c.Mat("CloudDeck", new Color32(0xF2, 0xF6, 0xFF, 0xFF));
            var sky = c.Mat("SkyBlue", new Color32(0x2D, 0x9C, 0xFF, 0xFF));
            var beam = c.Mat("Beam", new Color32(0xFF, 0x4F, 0x9A, 0xFF));
            var finish = c.Mat("Finish", new Color32(0xFF, 0xD1, 0x2B, 0xFF));

            c.Deck("Start", new Vector3(0f, 0f, -1f), new Vector3(26f, 1f, 22f), sky);
            var spawns = SpawnGrid(c.Gameplay, new Vector3(0f, 0f, -2f), 8, 4, 2.6f, 2.2f, 0f);

            var hops = new[] { new Vector3(-4f, 0f, 14f), new Vector3(4f, 0f, 22f), new Vector3(-4f, 0f, 30f), new Vector3(4f, 0f, 38f) };
            for (var i = 0; i < hops.Length; i++)
            {
                c.Deck($"Hop_{i}", hops[i], new Vector3(5f, 0.8f, 5f), cloud);
            }

            AddCheckpoint(c.Gameplay, 1, new Vector3(0f, 1.5f, 22f), new Vector3(20f, 4f, 2f), 4f);

            c.Deck("BridgeStart", new Vector3(0f, 0f, 43f), new Vector3(6f, 0.8f, 4f), cloud);
            for (var i = 0; i < 8; i++)
            {
                var tile = c.Obstacle("CyclingTile", new Vector3(0f, 0f, 46.5f + i * 3f), name: $"Bridge_{i}");
                c.SetPhase(tile, i * 0.6f);
            }

            c.Deck("BridgeEnd", new Vector3(0f, 0f, 75f), new Vector3(10f, 0.8f, 10f), cloud);
            AddCheckpoint(c.Gameplay, 2, new Vector3(0f, 1.5f, 72f), new Vector3(10f, 4f, 2f), 7f);

            for (var i = 0; i < 3; i++)
            {
                var platform = c.Obstacle("MovingPlatform", new Vector3(-4f, 0f, 85f + i * 7f), name: $"Drifter_{i}");
                MapBuildContext.Set(platform.GetComponent<OscillatingObstacle>(), "period", 4f + i * 0.5f);
                c.SetPhase(platform, i * 1.3f);
            }

            c.Deck("Junction", new Vector3(0f, 0f, 110f), new Vector3(16f, 0.8f, 14f), cloud);
            AddCheckpoint(c.Gameplay, 3, new Vector3(0f, 1.5f, 105f), new Vector3(16f, 4f, 2f), 12f);

            // Safe route (left): stepping islands. Risky shortcut (right): narrow beam with bumpers.
            for (var i = 0; i < 4; i++)
            {
                c.Deck($"SafeStep_{i}", new Vector3(-6f, 0f, 122f + i * 8f), new Vector3(4.5f, 0.8f, 4.5f), cloud);
            }

            c.Deck("RiskyBeam", new Vector3(5f, 0f, 134f), new Vector3(1.2f, 0.6f, 32f), beam);
            c.Obstacle("Bumper", new Vector3(5f, 0f, 128f));
            c.Obstacle("Bumper", new Vector3(5f, 0f, 140f));

            c.Deck("Finish", new Vector3(0f, 0f, 156f), new Vector3(18f, 1f, 12f), finish);
            AddFinishLine(c.Gameplay, new Vector3(0f, 2f, 158f), new Vector3(18f, 4f, 1f));

            c.Finish(spawns, -12f, new Bounds(new Vector3(0f, 0f, 80f), new Vector3(30f, 10f, 180f)),
                c.Mat("IslandTop", new Color32(0xF2, 0xF6, 0xFF, 0xFF)), c.Mat("IslandRock", new Color32(0xB8, 0xC8, 0xE8, 0xFF)), 23);
        }

        // =========================================================================================
        // SURVIVAL 1: SPIN ATTACK: circular arena with two sweeping bars and rim bumpers
        // =========================================================================================
        public static void SpinAttack()
        {
            var c = MapBuildContext.Begin("SpinAttack", "spin_attack", "Spin Attack",
                "Two giant spinning bars sweep the arena. Jump, dodge and don't get knocked off the edge.",
                MapCategory.Survival, FallRule.Eliminate, 2, 75f, new Color32(0x7A, 0x5C, 0xFF, 0xFF), Quaternion.Euler(55f, -20f, 0f));
            var arena = c.Mat("Arena", new Color32(0x7A, 0x5C, 0xFF, 0xFF));
            var ring = c.Mat("Ring", new Color32(0xFF, 0xD1, 0x2B, 0xFF));

            c.Disc("Arena", Vector3.zero, 15f, 1f, arena);
            SceneBuildKitDecorRing(c, 15.05f, ring);
            var big = c.Obstacle("ArenaSweeper", Vector3.zero, name: "Sweeper_Big");
            var small = c.Obstacle("SweeperBar", Vector3.zero, Quaternion.Euler(0f, 45f, 0f), "Sweeper_Fast");
            MapBuildContext.Set(small.GetComponent<RotatingObstacle>(), "degreesPerSecond", -110f);
            for (var i = 0; i < 4; i++)
            {
                var direction = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward;
                c.Obstacle("Bumper", direction * 12f, name: $"RimBumper_{i}");
            }

            var spawns = c.RingSpawns(Vector3.zero, 8f, 11.5f);
            c.Finish(spawns, -8f, new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f)),
                c.Mat("IslandTop", new Color32(0x9C, 0x84, 0xFF, 0xFF)), c.Mat("IslandRock", new Color32(0x5B, 0x47, 0x9C, 0xFF)), 31);
            _ = big;
        }

        // =========================================================================================
        // SURVIVAL 2: FALLING TILES: two layers of tiles that drop after being stepped on
        // =========================================================================================
        public static void FallingTiles()
        {
            var c = MapBuildContext.Begin("FallingTiles", "falling_tiles", "Falling Tiles",
                "Every tile you touch drops away. Keep moving, plan your path and be the last on the boards.",
                MapCategory.Survival, FallRule.Eliminate, 2, 120f, new Color32(0xFF, 0xD1, 0x2B, 0xFF), Quaternion.Euler(60f, -30f, 0f));

            foreach (var layerY in new[] { 0f, -10f })
            {
                for (var x = 0; x < 8; x++)
                {
                    for (var z = 0; z < 8; z++)
                    {
                        c.Obstacle("FallingTile", new Vector3((x - 3.5f) * 3f, layerY, (z - 3.5f) * 3f), name: $"Tile_{layerY}_{x}_{z}");
                    }
                }
            }

            var spawns = SpawnGrid(c.Gameplay, new Vector3(0f, 0f, 4.5f), 8, 4, 2.6f, 2.2f, 180f);
            c.Finish(spawns, -18f, new Bounds(new Vector3(0f, -5f, 0f), new Vector3(30f, 20f, 30f)),
                c.Mat("IslandTop", new Color32(0xFF, 0xE0, 0x7A, 0xFF)), c.Mat("IslandRock", new Color32(0xC9, 0x8A, 0x4B, 0xFF)), 41);
        }

        // =========================================================================================
        // SURVIVAL 3: PENDULUM PANIC: platform grid, swinging hammers, collapsing tiles, gaps
        // =========================================================================================
        public static void PendulumPanic()
        {
            var c = MapBuildContext.Begin("PendulumPanic", "pendulum_panic", "Pendulum Panic",
                "Giant pendulums swing across a grid of platforms. Some floors collapse. Mind the gaps!",
                MapCategory.Survival, FallRule.Eliminate, 3, 90f, new Color32(0xE8, 0x3A, 0x3A, 0xFF), Quaternion.Euler(50f, -35f, 0f));
            var deck = c.Mat("Deck", new Color32(0xFF, 0xB2, 0x3F, 0xFF));

            var platforms = new List<Vector3>();
            for (var x = -2; x <= 2; x++)
            {
                for (var z = -2; z <= 2; z++)
                {
                    var center = new Vector3(x * 5.5f, 0f, z * 5.5f);
                    var collapsing = Mathf.Abs(x) == 1 && Mathf.Abs(z) == 1;
                    if (collapsing)
                    {
                        var tile = c.Obstacle("FallingTile", center, name: $"Collapse_{x}_{z}");
                        MapBuildContext.Set(tile.GetComponent<FallingTile>(), "respawnDelay", 5f);
                    }
                    else
                    {
                        c.Deck($"Platform_{x}_{z}", center, new Vector3(4f, 0.8f, 4f), deck);
                        platforms.Add(center);
                    }
                }
            }

            var hammers = new[] { new Vector3(-5.5f, 0f, -8.25f), new Vector3(5.5f, 0f, -2.75f), new Vector3(-5.5f, 0f, 2.75f), new Vector3(5.5f, 0f, 8.25f), new Vector3(0f, 0f, -2.75f), new Vector3(0f, 0f, 8.25f) };
            for (var i = 0; i < hammers.Length; i++)
            {
                var hammer = c.Obstacle("HangingHammer", hammers[i], name: $"Pendulum_{i}");
                c.SetPhase(hammer, i * 0.45f);
            }

            var spawnRoot = new GameObject("SpawnPoints").transform;
            spawnRoot.SetParent(c.Gameplay, false);
            var spawns = new List<Transform>();
            for (var i = 0; spawns.Count < 32; i++)
            {
                var platform = platforms[i % platforms.Count];
                var offset = new Vector3(i / platforms.Count == 0 ? -0.9f : 0.9f, 0.05f, i / platforms.Count < 2 ? -0.9f : 0.9f);
                var point = new GameObject($"Spawn_{spawns.Count:00}").transform;
                point.SetParent(spawnRoot, false);
                point.localPosition = platform + offset;
                spawns.Add(point);
            }

            c.Finish(spawns.ToArray(), -10f, new Bounds(Vector3.zero, new Vector3(30f, 10f, 30f)),
                c.Mat("IslandTop", new Color32(0xFF, 0x8A, 0x6A, 0xFF)), c.Mat("IslandRock", new Color32(0x8C, 0x4A, 0x3A, 0xFF)), 53);
        }

        // =========================================================================================
        // FINAL 1: CROWN CLIMBER: spiral tower climb to the crown
        // =========================================================================================
        public static void CrownClimber()
        {
            var c = MapBuildContext.Begin("CrownClimber", "crown_climber", "Crown Climber",
                "Race up the spiral tower past vanishing steps, bumpers and swinging hammers. First to grab the crown is champion!",
                MapCategory.Final, FallRule.RespawnAtCheckpoint, 4, 240f, new Color32(0xFF, 0xD1, 0x2B, 0xFF), Quaternion.Euler(40f, -45f, 0f));
            var stone = c.Mat("Stone", new Color32(0x9A, 0xA4, 0xB8, 0xFF));
            var step = c.Mat("Step", new Color32(0xFF, 0x6A, 0x1A, 0xFF));
            var gold = c.Mat("Gold", new Color32(0xFF, 0xD1, 0x2B, 0xFF));
            var baseMat = c.Mat("Base", new Color32(0x5B, 0x2E, 0xFF, 0xFF));

            c.Disc("Base", Vector3.zero, 13f, 1f, baseMat);
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.SetParent(c.Level, false);
            pillar.transform.localPosition = new Vector3(0f, 15.5f, 0f);
            pillar.transform.localScale = new Vector3(6f, 15.5f, 6f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = stone;

            const int steps = 30;
            const float radius = 7f;
            const float rise = 1.0f;
            var checkpointOrder = 1;
            for (var i = 0; i < steps; i++)
            {
                var angle = i * 24f;
                var rotation = Quaternion.Euler(0f, angle, 0f);
                var position = rotation * new Vector3(0f, 0f, radius) + Vector3.up * (rise * (i + 1));
                if (i > 0 && i % 8 == 0)
                {
                    c.Obstacle("CyclingTile", position, rotation, $"VanishingStep_{i}");
                }
                else
                {
                    var block = c.Deck($"Step_{i}", position, new Vector3(4f, 0.6f, 3.5f), step);
                    block.transform.localRotation = rotation;
                }

                if (i % 7 == 3)
                {
                    c.Obstacle("Bumper", position + rotation * new Vector3(0f, 0f, 1.2f), name: $"StepBumper_{i}");
                }

                if (i > 0 && i % 7 == 0)
                {
                    var checkpoint = AddCheckpoint(c.Gameplay, checkpointOrder++, position + Vector3.up * 1.5f, new Vector3(4f, 4f, 3.5f), 2f);
                    checkpoint.transform.localRotation = rotation;
                }
            }

            foreach (var hammerStep in new[] { 12, 24 })
            {
                var rotation = Quaternion.Euler(0f, hammerStep * 24f, 0f);
                var position = rotation * new Vector3(0f, 0f, radius) + Vector3.up * (rise * (hammerStep + 1));
                c.Obstacle("HangingHammer", position, rotation * Quaternion.Euler(0f, 90f, 0f), $"TowerHammer_{hammerStep}");
            }

            var topY = rise * steps + 1f;
            c.Disc("CrownPlatform", new Vector3(0f, topY, 0f), 5f, 1f, gold);
            Decor(c.Decor, PrimitiveType.Cylinder, "CrownBand", new Vector3(0f, topY + 0.8f, 0f), new Vector3(2.4f, 0.4f, 2.4f), gold);
            for (var i = 0; i < 5; i++)
            {
                var direction = Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward;
                Decor(c.Decor, PrimitiveType.Sphere, $"CrownJewel_{i}", new Vector3(0f, topY + 1.6f, 0f) + direction * 1.1f, Vector3.one * 0.6f, step);
            }

            AddFinishLine(c.Gameplay, new Vector3(0f, topY + 1.5f, 0f), new Vector3(4f, 3f, 4f));
            var spawns = c.RingSpawns(Vector3.zero, 9.5f, 11.8f);
            c.Finish(spawns, -10f, new Bounds(new Vector3(0f, 15f, 0f), new Vector3(30f, 40f, 30f)),
                c.Mat("IslandTop", new Color32(0xFF, 0xE0, 0x7A, 0xFF)), c.Mat("IslandRock", new Color32(0x9A, 0xA4, 0xB8, 0xFF)), 67);
        }

        // =========================================================================================
        // FINAL 2: LAST PLATFORM: shrinking arena of collapsing tiles with a sweeper
        // =========================================================================================
        public static void LastPlatform()
        {
            var c = MapBuildContext.Begin("LastPlatform", "last_platform", "Last Platform",
                "The arena crumbles from the outside in while a sweeper spins. Last bean standing wins!",
                MapCategory.Final, FallRule.Eliminate, 4, 120f, new Color32(0xE8, 0x3A, 0x3A, 0xFF), Quaternion.Euler(55f, -25f, 0f));

            const float spacing = 2.6f;
            const float maxRadius = 13.5f;
            var random = new System.Random(77);
            for (var x = -5; x <= 5; x++)
            {
                for (var z = -5; z <= 5; z++)
                {
                    var position = new Vector3(x * spacing, 0f, z * spacing);
                    var distance = position.magnitude;
                    if (distance > maxRadius)
                    {
                        continue;
                    }

                    var tile = c.Obstacle("CollapseTile", position, name: $"Tile_{x}_{z}");
                    var collapseAt = distance < 3.5f ? 9999f : 12f + (maxRadius - distance) / 10f * 55f + (float)(random.NextDouble() * 6d - 3d);
                    MapBuildContext.Set(tile.GetComponent<TimedCollapse>(), "collapseAt", collapseAt);
                }
            }

            var sweeper = c.Obstacle("ArenaSweeper", Vector3.zero, name: "CenterSweeper");
            MapBuildContext.Set(sweeper.GetComponent<RotatingObstacle>(), "degreesPerSecond", 60f);

            var spawns = c.RingSpawns(Vector3.zero, 6f, 10f);
            c.Finish(spawns, -10f, new Bounds(Vector3.zero, new Vector3(32f, 10f, 32f)),
                c.Mat("IslandTop", new Color32(0xFF, 0x8A, 0x6A, 0xFF)), c.Mat("IslandRock", new Color32(0x5B, 0x2E, 0x3A, 0xFF)), 89);
        }

        private static void SceneBuildKitDecorRing(MapBuildContext c, float radius, Material material)
        {
            for (var i = 0; i < 24; i++)
            {
                var direction = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                Decor(c.Decor, PrimitiveType.Cube, $"Rim_{i}", direction * radius + Vector3.up * 0.15f, new Vector3(3.6f, 0.3f, 0.4f), material,
                    Quaternion.LookRotation(Vector3.Cross(Vector3.up, direction)));
            }
        }
    }
}
