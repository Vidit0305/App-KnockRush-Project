using System.Collections.Generic;
using System.Linq;
using KnockRush.Core.Events;
using KnockRush.Interaction;
using KnockRush.Obstacles;
using UnityEngine;

namespace KnockRush.Maps
{
    /// <summary>
    /// Single-device race loop used to play and test maps before networking (Phase 3):
    /// countdown with inputs locked → race timer → finish line → placement / qualification.
    /// Phase 5 replaces this with the server-authoritative round controller; the events stay the same.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfflineRaceSession : MonoBehaviour
    {
        public enum Phase
        {
            Countdown,
            Running,
            Finished
        }

        [SerializeField, Min(0)] private int countdownSeconds = 3;

        [Tooltip("How many finishers qualify (offline: usually all).")]
        [SerializeField, Min(1)] private int qualificationSlots = 20;

        private readonly List<ICourseParticipant> participants = new List<ICourseParticipant>();
        private float phaseStartTime;
        private int lastCountdownValue = -1;
        private int finishedCount;

        public static OfflineRaceSession Current { get; private set; }

        public Phase CurrentPhase { get; private set; } = Phase.Countdown;

        /// <summary>Seconds since GO (0 during countdown).</summary>
        public float ElapsedTime => CurrentPhase == Phase.Countdown ? 0f : (CurrentPhase == Phase.Running ? Time.time - phaseStartTime : finalTime);

        public float CountdownRemaining => CurrentPhase == Phase.Countdown ? Mathf.Max(0f, countdownSeconds - (Time.time - phaseStartTime)) : 0f;

        public float TimeLimit => MapController.Current != null && MapController.Current.Definition != null
            ? MapController.Current.Definition.TimeLimit
            : 180f;

        private float finalTime;

        private void Awake()
        {
            Current = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<FinishLineCrossedEvent>(OnFinishLineCrossed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<FinishLineCrossedEvent>(OnFinishLineCrossed);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Start()
        {
            participants.AddRange(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ICourseParticipant>());
            foreach (var participant in participants)
            {
                participant.InputLocked = true;
            }

            phaseStartTime = Time.time;
            ObstacleSimulation.ResetClock(Time.timeAsDouble + countdownSeconds);
            FallingTile.Armed = false;
        }

        private void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.Countdown:
                {
                    var secondsLeft = Mathf.CeilToInt(CountdownRemaining);
                    if (secondsLeft != lastCountdownValue)
                    {
                        lastCountdownValue = secondsLeft;
                        EventBus.Publish(new RaceCountdownEvent(secondsLeft));
                    }

                    if (secondsLeft <= 0)
                    {
                        StartRace();
                    }

                    break;
                }

                case Phase.Running:
                    if (ElapsedTime >= TimeLimit)
                    {
                        EndRace();
                        EventBus.Publish(new RaceTimeUpEvent());
                    }

                    break;
            }
        }

        private void StartRace()
        {
            CurrentPhase = Phase.Running;
            phaseStartTime = Time.time;
            FallingTile.Armed = true;
            foreach (var participant in participants)
            {
                participant.InputLocked = false;
            }
        }

        private void EndRace()
        {
            finalTime = Time.time - phaseStartTime;
            CurrentPhase = Phase.Finished;
        }

        private void OnFinishLineCrossed(FinishLineCrossedEvent crossed)
        {
            if (CurrentPhase != Phase.Running || crossed.Participant.HasFinished)
            {
                return;
            }

            finishedCount++;
            var time = ElapsedTime;
            crossed.Participant.MarkFinished();
            var qualified = finishedCount <= qualificationSlots;
            EventBus.Publish(new RaceFinishedEvent(crossed.Participant, time, finishedCount, qualified));

            if (participants.All(p => p.HasFinished))
            {
                EndRace();
            }
        }
    }
}
