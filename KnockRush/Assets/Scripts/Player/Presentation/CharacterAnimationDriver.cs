using KnockRush.Movement;
using KnockRush.Player.Controls;
using UnityEngine;

namespace KnockRush.Player.Presentation
{
    public enum CharacterEmote
    {
        None = 0,
        Celebrate = 1,
        VictoryDance = 2
    }

    /// <summary>
    /// Chooses the animation state purely from the simulated <see cref="MotorState"/> (plus a few
    /// one-shot events), so remote players animate correctly from replicated state alone and
    /// animation never needs bandwidth. States are cross-faded by name; the controller is built
    /// by KnockRush > Phase 2 > Build Character Assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationDriver : MonoBehaviour
    {
        public const string Locomotion = "Locomotion";
        public const string Jump = "Jump";
        public const string Fall = "Fall";
        public const string Land = "Land";
        public const string Dive = "Dive";
        public const string Hit = "Hit";
        public const string Stumble = "Stumble";
        public const string Grab = "Grab";
        public const string Eliminated = "Eliminated";
        public const string Celebrate = "Celebrate";
        public const string VictoryDance = "VictoryDance";

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        [SerializeField] private PlayerCharacter character;
        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 0.5f)] private float crossFadeDuration = 0.12f;
        [Tooltip("Landing speed (m/s) above which the squash 'Land' animation plays.")]
        [SerializeField] private float hardLandingSpeed = 6f;

        private int currentState;
        private float lockedUntil;
        private CharacterEmote emote;
        private bool eliminated;

        public void PlayEmote(CharacterEmote value)
        {
            emote = value;
        }

        public void SetEliminated(bool value)
        {
            eliminated = value;
        }

        private void Reset()
        {
            character = GetComponentInParent<PlayerCharacter>();
            animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (character != null)
            {
                character.MotorEventsRaised += OnMotorEvents;
            }
        }

        private void OnDisable()
        {
            if (character != null)
            {
                character.MotorEventsRaised -= OnMotorEvents;
            }
        }

        private void Update()
        {
            if (character == null || animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (character.IsLocallyControlled && PlayerInputReader.Local != null)
            {
                if (PlayerInputReader.Local.CelebratePressedThisFrame)
                {
                    emote = CharacterEmote.Celebrate;
                }
                else if (PlayerInputReader.Local.VictoryPressedThisFrame)
                {
                    emote = CharacterEmote.VictoryDance;
                }
            }

            var state = character.State;
            var maxSpeed = Mathf.Max(0.01f, character.Settings.MaxSpeed);
            var speed01 = Mathf.Clamp01(state.HorizontalVelocity.magnitude / maxSpeed);
            animator.SetFloat(SpeedParameter, speed01, 0.08f, Time.deltaTime);

            if (speed01 > 0.15f || state.Mode != MotorMode.Grounded)
            {
                emote = CharacterEmote.None;
            }

            if (Time.time < lockedUntil)
            {
                return;
            }

            CrossFade(Resolve(state, speed01));
        }

        private string Resolve(in MotorState state, float speed01)
        {
            if (eliminated)
            {
                return Eliminated;
            }

            switch (state.Mode)
            {
                case MotorMode.Diving:
                case MotorMode.Sliding:
                    return Dive;
                case MotorMode.Recovering:
                    return Land;
                case MotorMode.Stumbled:
                    return Stumble;
                case MotorMode.Airborne:
                    return state.Velocity.y > 0.5f ? Jump : Fall;
                case MotorMode.Disabled:
                    return emote == CharacterEmote.VictoryDance ? VictoryDance : Celebrate;
                default:
                    if (state.IsGrabbing)
                    {
                        return Grab;
                    }

                    switch (emote)
                    {
                        case CharacterEmote.Celebrate:
                            return Celebrate;
                        case CharacterEmote.VictoryDance:
                            return VictoryDance;
                        default:
                            return Locomotion;
                    }
            }
        }

        private void OnMotorEvents(MotorEvents events)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if ((events & MotorEvents.Stumbled) != 0)
            {
                Play(Hit, 0.25f);
            }
            else if ((events & MotorEvents.Jumped) != 0)
            {
                Play(Jump, 0.15f);
            }
            else if ((events & MotorEvents.Dived) != 0)
            {
                Play(Dive, 0.1f);
            }
            else if ((events & MotorEvents.Landed) != 0 &&
                     character.State.Mode == MotorMode.Grounded &&
                     character.State.LastLandingSpeed >= hardLandingSpeed)
            {
                Play(Land, 0.15f);
            }
            else if ((events & MotorEvents.Respawned) != 0)
            {
                lockedUntil = 0f;
                emote = CharacterEmote.None;
                CrossFade(Fall);
            }
        }

        private void Play(string stateName, float lockDuration)
        {
            emote = CharacterEmote.None;
            var hash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, hash))
            {
                return;
            }

            animator.CrossFadeInFixedTime(hash, crossFadeDuration * 0.5f, 0, 0f);
            currentState = hash;
            lockedUntil = Time.time + lockDuration;
        }

        private void CrossFade(string stateName)
        {
            var hash = Animator.StringToHash(stateName);
            if (hash == currentState || !animator.HasState(0, hash))
            {
                return;
            }

            animator.CrossFadeInFixedTime(hash, crossFadeDuration);
            currentState = hash;
        }
    }
}
