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

    /// <summary>Maps cosmetic emote ids (backend catalogue) to animator state names.</summary>
    public static class EmoteStates
    {
        public static string ForItem(string emoteItemId)
        {
            switch (emoteItemId)
            {
                case "emote_celebrate": return CharacterAnimationDriver.Celebrate;
                case "emote_victory": return CharacterAnimationDriver.VictoryDance;
                case "emote_wave": return "Wave";
                case "emote_bounce": return "Bounce";
                case "emote_spin": return "Spin";
                case "emote_clap": return "Clap";
                case "emote_bow": return "Bow";
                case "emote_flex": return "Flex";
                case "emote_laugh": return "Laugh";
                case "emote_robotdance": return "RobotDance";
                default: return null;
            }
        }
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

        [SerializeField] private Animator animator;
        [SerializeField, Range(0f, 0.5f)] private float crossFadeDuration = 0.12f;
        [Tooltip("Landing speed (m/s) above which the squash 'Land' animation plays.")]
        [SerializeField] private float hardLandingSpeed = 6f;

        private ICharacterView character;
        private int currentState;
        private float lockedUntil;
        private string emoteState;

        /// <summary>Emote played by the "Celebrate" input (the player's equipped emote).</summary>
        public string EquippedEmoteState { get; set; } = Celebrate;
        private bool eliminated;

        public void PlayEmote(CharacterEmote value)
        {
            emoteState = value == CharacterEmote.Celebrate ? Celebrate : value == CharacterEmote.VictoryDance ? VictoryDance : null;
        }

        /// <summary>Plays any emote state by name (see <see cref="EmoteStates"/>).</summary>
        public void PlayEmote(string stateName)
        {
            emoteState = stateName;
        }

        public void SetEliminated(bool value)
        {
            eliminated = value;
        }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            character = GetComponentInParent<ICharacterView>();
        }

        private void OnEnable()
        {
            if (character != null)
            {
                character.MotorEventsRaised += OnMotorEvents;
                character.Finished += OnFinished;
            }
        }

        private void OnDisable()
        {
            if (character != null)
            {
                character.MotorEventsRaised -= OnMotorEvents;
                character.Finished -= OnFinished;
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
                    emoteState = EquippedEmoteState;
                }
                else if (PlayerInputReader.Local.VictoryPressedThisFrame)
                {
                    emoteState = VictoryDance;
                }
            }

            var state = character.State;
            var maxSpeed = Mathf.Max(0.01f, character.Settings.MaxSpeed);
            var speed01 = Mathf.Clamp01(state.HorizontalVelocity.magnitude / maxSpeed);
            animator.SetFloat(SpeedParameter, speed01, 0.08f, Time.deltaTime);

            if (!character.HasFinished && (speed01 > 0.15f || state.Mode != MotorMode.Grounded))
            {
                emoteState = null;
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
                    return emoteState ?? Celebrate;
                default:
                    if (state.IsGrabbing)
                    {
                        return Grab;
                    }

                    return emoteState ?? Locomotion;
            }
        }

        private void OnFinished()
        {
            emoteState = EquippedEmoteState;
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
                emoteState = null;
                CrossFade(Fall);
            }
        }

        private void Play(string stateName, float lockDuration)
        {
            emoteState = null;
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
