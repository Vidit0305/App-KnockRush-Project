using UnityEngine;

namespace KnockRush.Movement
{
    /// <summary>
    /// Collision queries for an upright kinematic capsule whose origin is at its feet.
    /// All queries go through an explicit <see cref="PhysicsScene"/> so the same code works
    /// in the default scene, in Fusion's per-runner physics scenes and on the dedicated server.
    /// </summary>
    public static class KinematicCapsule
    {
        public const int MaxSlideIterations = 4;
        private const int MaxOverlaps = 16;
        private const float MinMoveDistance = 0.0001f;

        private static readonly Collider[] OverlapBuffer = new Collider[MaxOverlaps];

        public struct GroundHit
        {
            public bool HasGround;
            public bool IsWalkable;
            public Vector3 Normal;
            public Vector3 Point;
            public float Distance;
            public Collider Collider;
        }

        public static void GetCapsulePoints(Vector3 feet, MovementSettings settings, out Vector3 bottom, out Vector3 top)
        {
            bottom = feet + Vector3.up * settings.Radius;
            top = feet + Vector3.up * (settings.Height - settings.Radius);
        }

        /// <summary>
        /// Moves the capsule by <paramref name="displacement"/>, sliding along anything it hits.
        /// Velocity components pointing into hit surfaces are removed.
        /// </summary>
        /// <returns>True if any surface was hit.</returns>
        public static bool MoveAndSlide(
            PhysicsScene physics,
            MovementSettings settings,
            bool grounded,
            ref Vector3 position,
            ref Vector3 velocity,
            Vector3 displacement,
            out bool hitWall)
        {
            hitWall = false;
            var hitAnything = false;
            var remaining = displacement;
            var minGroundY = settings.MinGroundNormalY;

            for (var i = 0; i < MaxSlideIterations; i++)
            {
                var distance = remaining.magnitude;
                if (distance < MinMoveDistance)
                {
                    break;
                }

                var direction = remaining / distance;
                GetCapsulePoints(position, settings, out var bottom, out var top);

                if (!physics.CapsuleCast(bottom, top, settings.Radius, direction, out var hit,
                        distance + settings.SkinWidth, settings.CollisionMask, QueryTriggerInteraction.Ignore))
                {
                    position += remaining;
                    break;
                }

                hitAnything = true;
                var travel = Mathf.Max(0f, hit.distance - settings.SkinWidth);
                position += direction * travel;
                remaining -= direction * travel;

                var normal = hit.normal;
                var walkable = normal.y >= minGroundY;
                if (!walkable)
                {
                    hitWall |= normal.y > -0.5f;

                    // While grounded, treat steep surfaces as vertical walls so we never climb them.
                    if (grounded && normal.y > 0f)
                    {
                        normal.y = 0f;
                        if (normal.sqrMagnitude < 1e-6f)
                        {
                            normal = -direction;
                        }

                        normal.Normalize();
                    }
                }

                remaining = Vector3.ProjectOnPlane(remaining, normal);
                if (Vector3.Dot(velocity, normal) < 0f)
                {
                    velocity = Vector3.ProjectOnPlane(velocity, normal);
                }
            }

            return hitAnything;
        }

        /// <summary>Pushes the capsule out of any geometry it overlaps.</summary>
        public static bool Depenetrate(PhysicsScene physics, MovementSettings settings, Collider shape,
            ref Vector3 position, ref Vector3 velocity)
        {
            if (shape == null)
            {
                return false;
            }

            GetCapsulePoints(position, settings, out var bottom, out var top);
            var count = physics.OverlapCapsule(bottom, top, settings.Radius, OverlapBuffer,
                settings.CollisionMask, QueryTriggerInteraction.Ignore);

            var moved = false;
            for (var i = 0; i < count; i++)
            {
                var other = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (other == null || other == shape)
                {
                    continue;
                }

                var otherTransform = other.transform;
                if (Physics.ComputePenetration(shape, position, Quaternion.identity,
                        other, otherTransform.position, otherTransform.rotation,
                        out var direction, out var distance))
                {
                    position += direction * (distance + 0.001f);
                    if (Vector3.Dot(velocity, direction) < 0f)
                    {
                        velocity = Vector3.ProjectOnPlane(velocity, direction);
                    }

                    moved = true;
                }
            }

            return moved;
        }

        /// <summary>Sphere-casts down from the bottom of the capsule to find ground.</summary>
        public static GroundHit ProbeGround(PhysicsScene physics, MovementSettings settings, Vector3 position, float maxDistance)
        {
            var result = new GroundHit { Normal = Vector3.up };
            var castRadius = settings.Radius * 0.95f;
            var origin = position + Vector3.up * (settings.Radius + settings.SkinWidth);
            var startOffset = settings.Radius - castRadius + settings.SkinWidth;

            if (!physics.SphereCast(origin, castRadius, Vector3.down, out var hit,
                    maxDistance + startOffset, settings.CollisionMask, QueryTriggerInteraction.Ignore))
            {
                return result;
            }

            result.HasGround = true;
            result.Normal = hit.normal;
            result.Point = hit.point;
            result.Collider = hit.collider;
            result.Distance = Mathf.Max(0f, hit.distance - startOffset);

            // The cast normal can be skewed on edges; confirm with a short ray when steep.
            var normal = hit.normal;
            if (normal.y < settings.MinGroundNormalY &&
                physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.down, out var rayHit, 0.1f,
                    settings.CollisionMask, QueryTriggerInteraction.Ignore))
            {
                normal = rayHit.normal;
            }

            result.IsWalkable = normal.y >= settings.MinGroundNormalY;
            result.Normal = normal;
            return result;
        }
    }
}
