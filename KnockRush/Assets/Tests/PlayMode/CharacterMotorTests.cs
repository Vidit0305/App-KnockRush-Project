using System.Collections.Generic;
using KnockRush.Movement;
using NUnit.Framework;
using UnityEngine;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// Deterministic simulation tests for the kinematic character motor. Each test builds a tiny
    /// physics world, steps the motor at 60 Hz and checks the result against MovementSettings.
    /// </summary>
    public sealed class CharacterMotorTests
    {
        private const float Dt = 1f / 60f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private MovementSettings settings;
        private CapsuleCollider shape;

        private sealed class ConveyorStub : MonoBehaviour, IMovementSurface
        {
            public Vector3 Velocity;
            public float FrictionMultiplier => 1f;
            public Vector3 GetSurfaceVelocity(Vector3 worldPoint) => Velocity;
        }

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MovementSettings>();
            settings.SetCollisionMask(1 << 0);

            Box(new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f));

            var shapeObject = Track(new GameObject("MotorShape"));
            shapeObject.layer = 2; // Ignore Raycast: not in the collision mask.
            shape = shapeObject.AddComponent<CapsuleCollider>();
            shape.radius = settings.Radius;
            shape.height = settings.Height;
            shape.center = new Vector3(0f, settings.Height * 0.5f, 0f);

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                Object.DestroyImmediate(go);
            }

            spawned.Clear();
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void FallsAndLandsOnTheFloor()
        {
            var state = MotorState.Spawn(new Vector3(0f, 3f, 0f), 0f);
            var events = Run(ref state, MotorInput.None, 120);

            Assert.IsTrue(state.IsGrounded);
            Assert.AreEqual(MotorMode.Grounded, state.Mode);
            Assert.That(state.Position.y, Is.InRange(-0.001f, 0.05f));
            Assert.IsTrue((events & MotorEvents.Landed) != 0);
        }

        [Test]
        public void FullJumpReachesConfiguredHeight()
        {
            var state = Grounded();
            var apex = JumpAndMeasureApex(ref state, holdJump: true);

            Assert.That(apex, Is.InRange(settings.JumpHeight * 0.88f, settings.JumpHeight * 1.05f));
        }

        [Test]
        public void ReleasingJumpEarlyGivesALowerJump()
        {
            var full = Grounded();
            var fullApex = JumpAndMeasureApex(ref full, holdJump: true);
            var tap = Grounded();
            var tapApex = JumpAndMeasureApex(ref tap, holdJump: false);

            Assert.Less(tapApex, fullApex * 0.8f);
        }

        [Test]
        public void WalkingReachesMaxSpeedAndFacesMoveDirection()
        {
            var state = Grounded();
            Run(ref state, new MotorInput { Move = new Vector2(1f, 0f) }, 60);

            Assert.AreEqual(settings.MaxSpeed, state.HorizontalVelocity.magnitude, 0.05f);
            Assert.AreEqual(90f, state.Yaw, 1f);
            Assert.IsTrue(state.IsGrounded);
        }

        [Test]
        public void WallStopsTheCharacter()
        {
            Box(new Vector3(3.25f, 1f, 0f), new Vector3(0.5f, 2f, 10f)); // face at x = 3
            Physics.SyncTransforms();

            var state = Grounded();
            Run(ref state, new MotorInput { Move = new Vector2(1f, 0f) }, 120);

            var maxX = 3f - settings.Radius;
            Assert.That(state.Position.x, Is.InRange(maxX - 0.1f, maxX + 0.005f));
            Assert.Less(state.HorizontalVelocity.magnitude, 0.5f);
        }

        [Test]
        public void DiveThenSlideThenRecover()
        {
            var state = Grounded();
            var diveInput = new MotorInput { Move = new Vector2(0f, 1f), DivePressed = true };
            var events = Run(ref state, diveInput, 1);
            Assert.AreEqual(MotorMode.Diving, state.Mode);
            Assert.IsTrue((events & MotorEvents.Dived) != 0);
            Assert.GreaterOrEqual(state.HorizontalVelocity.magnitude, settings.DiveHorizontalSpeed - 0.01f);

            var sawSliding = false;
            var sawRecovering = false;
            for (var i = 0; i < 240 && !(sawRecovering && state.Mode == MotorMode.Grounded); i++)
            {
                Run(ref state, new MotorInput { Move = new Vector2(0f, 1f) }, 1);
                sawSliding |= state.Mode == MotorMode.Sliding;
                sawRecovering |= state.Mode == MotorMode.Recovering;
            }

            Assert.IsTrue(sawSliding, "Never entered Sliding.");
            Assert.IsTrue(sawRecovering, "Never entered Recovering.");
            Assert.AreEqual(MotorMode.Grounded, state.Mode);
        }

        [Test]
        public void SteepSlopeIsNotWalkableButGentleSlopeIs()
        {
            var steep = Slope(new Vector3(0f, 6f, 30f), 70f);
            var gentle = Slope(new Vector3(30f, 6f, 30f), 20f);
            Physics.SyncTransforms();

            var steepHit = ProbeAboveSurface(steep);
            var gentleHit = ProbeAboveSurface(gentle);

            Assert.IsTrue(steepHit.HasGround);
            Assert.IsFalse(steepHit.IsWalkable);
            Assert.IsTrue(gentleHit.HasGround);
            Assert.IsTrue(gentleHit.IsWalkable);
        }

        [Test]
        public void StrongImpulseStumblesThenRecovers()
        {
            var state = Grounded();
            CharacterMotor.ApplyImpulse(ref state, new Vector3(8f, 3f, 0f), settings);

            Assert.AreEqual(MotorMode.Stumbled, state.Mode);
            Assert.IsTrue((CharacterMotor.ConsumeEvents(ref state) & MotorEvents.Stumbled) != 0);

            Run(ref state, MotorInput.None, Mathf.CeilToInt((settings.StumbleDuration + 1f) / Dt));
            Assert.AreEqual(MotorMode.Grounded, state.Mode);
            Assert.Greater(state.Position.x, 0.5f, "Impulse should have pushed the character.");
        }

        [Test]
        public void MovingSurfaceCarriesTheCharacter()
        {
            var conveyor = Box(new Vector3(0f, 0.25f, 50f), new Vector3(20f, 0.5f, 20f));
            conveyor.AddComponent<ConveyorStub>().Velocity = new Vector3(2f, 0f, 0f);
            Physics.SyncTransforms();

            var state = MotorState.Spawn(new Vector3(0f, 0.6f, 50f), 0f);
            Run(ref state, MotorInput.None, 60); // settle
            var startX = state.Position.x;
            Run(ref state, MotorInput.None, 60); // one second on the conveyor

            Assert.AreEqual(2f, state.Position.x - startX, 0.15f);
        }

        // ------------------------------------------------------------------

        private MotorState Grounded()
        {
            var state = MotorState.Spawn(new Vector3(0f, 0.05f, 0f), 0f);
            Run(ref state, MotorInput.None, 30);
            Assert.IsTrue(state.IsGrounded, "Setup: character did not settle on the floor.");
            return state;
        }

        private float JumpAndMeasureApex(ref MotorState state, bool holdJump)
        {
            var startY = state.Position.y;
            var apex = startY;
            Run(ref state, new MotorInput { JumpPressed = true, JumpHeld = true }, 1);
            for (var i = 0; i < 120; i++)
            {
                Run(ref state, new MotorInput { JumpHeld = holdJump }, 1);
                apex = Mathf.Max(apex, state.Position.y);
            }

            return apex - startY;
        }

        private MotorEvents Run(ref MotorState state, MotorInput input, int ticks)
        {
            var events = MotorEvents.None;
            for (var i = 0; i < ticks; i++)
            {
                CharacterMotor.Step(ref state, input, settings, Dt, Physics.defaultPhysicsScene, shape);
                events |= CharacterMotor.ConsumeEvents(ref state);
            }

            return events;
        }

        private KinematicCapsule.GroundHit ProbeAboveSurface(GameObject slope)
        {
            // Start clear of the surface along its normal: a sweep that starts inside a collider ignores it.
            var normal = slope.transform.up;
            var surfacePoint = slope.transform.position + normal * 0.5f;
            return KinematicCapsule.ProbeGround(Physics.defaultPhysicsScene, settings, surfacePoint + normal * 0.6f, 3f);
        }

        private GameObject Slope(Vector3 center, float angle)
        {
            var slope = Box(center, new Vector3(10f, 1f, 20f));
            slope.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            return slope;
        }

        private GameObject Box(Vector3 center, Vector3 size)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.transform.position = center;
            box.transform.localScale = size;
            return box;
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }
    }
}
