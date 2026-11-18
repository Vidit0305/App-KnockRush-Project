using System.Collections;
using System.Collections.Generic;
using KnockRush.Interaction;
using KnockRush.Obstacles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KnockRush.Tests.PlayMode
{
    public sealed class ObstacleTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private sealed class StubTarget : MonoBehaviour, IKnockbackTarget
        {
            public readonly List<Vector3> Impulses = new List<Vector3>();
            public Vector3 Position => transform.position;
            public void ApplyImpulse(Vector3 impulse) => Impulses.Add(impulse);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                Object.DestroyImmediate(go);
            }

            spawned.Clear();
        }

        [Test]
        public void RotatingObstacle_PoseAndPointVelocity()
        {
            var rotor = Track(new GameObject("Rotor")).AddComponent<RotatingObstacle>();
            rotor.Configure(Vector3.up, 90f);

            rotor.ApplyTime(1d);
            Assert.That(Quaternion.Angle(rotor.transform.localRotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.1f));

            // ω = π/2 rad/s at 2 m radius → 3.14 m/s tangential.
            var velocity = rotor.GetPointVelocity(rotor.transform.TransformPoint(new Vector3(2f, 0f, 0f)));
            Assert.AreEqual(Mathf.PI, velocity.magnitude, 0.05f);
        }

        [Test]
        public void PendulumObstacle_FollowsSineSwing()
        {
            Assert.AreEqual(0f, PendulumObstacle.AngleAt(0d, 70f, 2.8f), 0.001f);
            Assert.AreEqual(70f, PendulumObstacle.AngleAt(0.7d, 70f, 2.8f), 0.01f);
            Assert.AreEqual(-70f, PendulumObstacle.AngleAt(2.1d, 70f, 2.8f), 0.01f);
        }

        [Test]
        public void OscillatingObstacle_ReachesBothEnds()
        {
            Assert.AreEqual(0f, OscillatingObstacle.Progress(0d, 4f, 0.2f), 0.001f);
            Assert.AreEqual(1f, OscillatingObstacle.Progress(2d, 4f, 0.2f), 0.001f);
            Assert.That(OscillatingObstacle.Progress(1d, 4f, 0.2f), Is.InRange(0.05f, 0.95f));

            var mover = Track(new GameObject("Mover")).AddComponent<OscillatingObstacle>();
            mover.Configure(new Vector3(8f, 0f, 0f), 4f, 0.2f);
            mover.ApplyTime(2d);
            Assert.AreEqual(8f, mover.transform.localPosition.x, 0.001f);
        }

        [Test]
        public void CyclingPlatform_IsSolidThenGone()
        {
            Assert.IsTrue(CyclingPlatform.IsSolidAt(0.5d, 5f, 0.6f));
            Assert.IsFalse(CyclingPlatform.IsSolidAt(4d, 5f, 0.6f));
            Assert.IsTrue(CyclingPlatform.IsSolidAt(5.5d, 5f, 0.6f));
        }

        [Test]
        public void ObstacleSimulation_RegistersEnabledObstacles()
        {
            var before = ObstacleSimulation.Count;
            var rotor = Track(new GameObject("Rotor")).AddComponent<RotatingObstacle>();
            Assert.AreEqual(before + 1, ObstacleSimulation.Count);
            rotor.enabled = false;
            Assert.AreEqual(before, ObstacleSimulation.Count);
        }

        [UnityTest]
        public IEnumerator ImpulseZone_KnocksOverlappingTarget()
        {
            var zone = Track(new GameObject("Zone"));
            zone.transform.position = new Vector3(500f, 0f, 0f);
            zone.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);
            zone.AddComponent<ImpulseZone>().Configure(ImpulseMode.Directional, 10f, 0f, Vector3.up);

            var target = Track(new GameObject("Target"));
            target.transform.position = new Vector3(500f, -0.5f, 0f);
            target.AddComponent<CapsuleCollider>();
            var body = target.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var stub = target.AddComponent<StubTarget>();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.GreaterOrEqual(stub.Impulses.Count, 1, "Zone never hit the target.");
            Assert.AreEqual(10f, stub.Impulses[0].y, 0.01f);
            Assert.AreEqual(1, stub.Impulses.Count, "Cooldown should prevent repeated hits.");
        }

        [UnityTest]
        public IEnumerator FallingTile_ShakesThenFallsAndStaysGone()
        {
            var tileObject = Track(new GameObject("Tile"));
            tileObject.transform.position = new Vector3(-500f, 0f, 0f);
            var solid = tileObject.AddComponent<BoxCollider>();
            var tile = tileObject.AddComponent<FallingTile>();
            tile.Configure(0.1f, 0.2f, -1f);

            tile.Trigger();
            Assert.AreEqual(FallingTile.TileState.Shaking, tile.State);

            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(FallingTile.TileState.Gone, tile.State);
            Assert.IsFalse(solid.enabled);
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }
    }
}
