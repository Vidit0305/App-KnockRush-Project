using KnockRush.Interaction;
using UnityEngine;

namespace KnockRush.Obstacles
{
    /// <summary>
    /// "Category B" event-driven obstacle: shakes when a character steps on it, then drops away.
    /// Optionally reappears (race maps) or stays gone (survival maps such as Falling Tiles).
    /// Needs a solid collider plus a thin trigger collider on top, both on this GameObject,
    /// and a child "Visual" for the shake. In multiplayer the server owns the trigger (Phase 4).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FallingTile : MonoBehaviour
    {
        public enum TileState
        {
            Idle,
            Shaking,
            Falling,
            Gone
        }

        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float shakeDuration = 0.7f;
        [SerializeField, Min(0.1f)] private float fallDuration = 1.2f;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;

        [Tooltip("Seconds until the tile returns after falling. Negative = never returns.")]
        [SerializeField] private float respawnDelay = -1f;

        private Collider solidCollider;
        private Vector3 baseLocalPosition;
        private Vector3 visualBaseLocal;
        private float stateTime;
        private float fallSpeed;

        public TileState State { get; private set; } = TileState.Idle;

        public void Configure(float shake, float fall, float respawn)
        {
            shakeDuration = shake;
            fallDuration = fall;
            respawnDelay = respawn;
        }

        /// <summary>Starts the shake → fall sequence (also called by the server in multiplayer).</summary>
        public void Trigger()
        {
            if (State != TileState.Idle)
            {
                return;
            }

            SetState(TileState.Shaking);
        }

        private void Awake()
        {
            foreach (var c in GetComponents<Collider>())
            {
                if (!c.isTrigger)
                {
                    solidCollider = c;
                }
            }

            baseLocalPosition = transform.localPosition;
            if (visual != null)
            {
                visualBaseLocal = visual.localPosition;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<IKnockbackTarget>() != null)
            {
                Trigger();
            }
        }

        private void Update()
        {
            stateTime += Time.deltaTime;
            switch (State)
            {
                case TileState.Shaking:
                    if (visual != null)
                    {
                        var t = Time.time * 60f;
                        visual.localPosition = visualBaseLocal + new Vector3(Mathf.Sin(t) * shakeAmplitude, 0f, Mathf.Cos(t * 1.3f) * shakeAmplitude);
                    }

                    if (stateTime >= shakeDuration)
                    {
                        SetState(TileState.Falling);
                    }

                    break;

                case TileState.Falling:
                    fallSpeed += 25f * Time.deltaTime;
                    transform.localPosition += Vector3.down * (fallSpeed * Time.deltaTime);
                    if (stateTime >= fallDuration)
                    {
                        SetState(TileState.Gone);
                    }

                    break;

                case TileState.Gone:
                    if (respawnDelay >= 0f && stateTime >= respawnDelay)
                    {
                        SetState(TileState.Idle);
                    }

                    break;
            }
        }

        private void SetState(TileState state)
        {
            State = state;
            stateTime = 0f;

            switch (state)
            {
                case TileState.Idle:
                    transform.localPosition = baseLocalPosition;
                    fallSpeed = 0f;
                    SetSolid(true);
                    SetVisible(true);
                    break;
                case TileState.Shaking:
                    break;
                case TileState.Falling:
                    if (visual != null)
                    {
                        visual.localPosition = visualBaseLocal;
                    }

                    SetSolid(false);
                    break;
                case TileState.Gone:
                    SetVisible(false);
                    break;
            }
        }

        private void SetSolid(bool solid)
        {
            if (solidCollider != null)
            {
                solidCollider.enabled = solid;
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = visible;
            }
        }
    }
}
