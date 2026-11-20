using KnockRush.Inventory;
using KnockRush.Player.Presentation;
using UnityEngine;

namespace KnockRush.UI.Stage
{
    /// <summary>
    /// A background bean in the menu lobby: wears a fixed outfit and either idles with emotes or
    /// jogs back and forth along a short path, so the lobby feels alive behind the player's bean.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuCrowdBean : MonoBehaviour
    {
        public enum Behaviour
        {
            Idle,
            Jog
        }

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly string[] Emotes = { "emote_wave", "emote_celebrate", "emote_bounce", "emote_clap", "emote_spin", "emote_flex", "emote_laugh" };

        [SerializeField] private Animator animator;
        [SerializeField] private CosmeticApplier cosmetics;
        [SerializeField] private Behaviour behaviour;
        [SerializeField] private string body = "default_bean";
        [SerializeField] private string color = "default_orange";
        [SerializeField] private string pattern = "pattern_none";
        [SerializeField] private string hat = "hat_none";
        [SerializeField] private Vector3 pathEnd;
        [SerializeField] private float jogSpeed = 2.2f;
        [SerializeField] private int seed;

        private System.Random random;
        private Vector3 pathStart;
        private bool towardsEnd = true;
        private float pauseUntil;
        private float nextEmote;
        private float emoteEnds;
        private Quaternion restRotation;

        public void Configure(Animator beanAnimator, CosmeticApplier applier, Behaviour mode, Loadout outfit, Vector3 end, int randomSeed)
        {
            animator = beanAnimator;
            cosmetics = applier;
            behaviour = mode;
            body = outfit.Body;
            color = outfit.Color;
            pattern = outfit.Pattern;
            hat = outfit.Hat;
            pathEnd = end;
            seed = randomSeed;
        }

        private void Start()
        {
            random = new System.Random(seed);
            pathStart = transform.localPosition;
            restRotation = transform.localRotation;
            if (cosmetics != null)
            {
                cosmetics.Apply(new Loadout { Body = body, Color = color, Pattern = pattern, Hat = hat, Emote = Emotes[seed % Emotes.Length] });
            }

            nextEmote = Time.time + 1.5f + (float)random.NextDouble() * 5f;
            if (animator != null)
            {
                // Desynchronise identical idles.
                animator.Play(CharacterAnimationDriver.Locomotion, 0, (float)random.NextDouble());
            }
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (behaviour == Behaviour.Idle)
            {
                animator.SetFloat(SpeedId, 0f);
                if (emoteEnds > 0f && Time.time > emoteEnds)
                {
                    emoteEnds = 0f;
                    animator.CrossFadeInFixedTime(CharacterAnimationDriver.Locomotion, 0.25f);
                }
                else if (emoteEnds <= 0f && Time.time > nextEmote)
                {
                    PlayEmote();
                    nextEmote = Time.time + 5f + (float)random.NextDouble() * 6f;
                }

                return;
            }

            Jog();
        }

        private void Jog()
        {
            var target = towardsEnd ? pathEnd : pathStart;
            if (Time.time < pauseUntil)
            {
                animator.SetFloat(SpeedId, 0f, 0.1f, Time.deltaTime);
                // Face the camera while resting.
                transform.localRotation = Quaternion.Slerp(transform.localRotation, restRotation, Time.deltaTime * 6f);
                if (emoteEnds > 0f && Time.time > emoteEnds)
                {
                    emoteEnds = 0f;
                    animator.CrossFadeInFixedTime(CharacterAnimationDriver.Locomotion, 0.25f);
                }

                return;
            }

            var position = transform.localPosition;
            var to = target - position;
            if (to.sqrMagnitude < 0.01f)
            {
                towardsEnd = !towardsEnd;
                pauseUntil = Time.time + 1.2f + (float)random.NextDouble() * 2.5f;
                if (random.NextDouble() < 0.6)
                {
                    PlayEmote();
                }

                return;
            }

            var flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(flat), Time.deltaTime * 8f);
            }

            transform.localPosition = Vector3.MoveTowards(position, target, jogSpeed * Time.deltaTime);
            animator.SetFloat(SpeedId, 0.75f, 0.1f, Time.deltaTime);
        }

        private void PlayEmote()
        {
            var state = EmoteStates.ForItem(Emotes[random.Next(Emotes.Length)]) ?? CharacterAnimationDriver.Celebrate;
            animator.CrossFadeInFixedTime(state, 0.15f);
            emoteEnds = Time.time + 2.6f;
        }
    }
}
