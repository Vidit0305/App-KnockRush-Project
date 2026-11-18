using KnockRush.Interaction;

namespace KnockRush.Maps
{
    public readonly struct CheckpointReachedEvent
    {
        public readonly ICourseParticipant Participant;
        public readonly int Order;

        public CheckpointReachedEvent(ICourseParticipant participant, int order)
        {
            Participant = participant;
            Order = order;
        }
    }

    public readonly struct ParticipantFellEvent
    {
        public readonly ICourseParticipant Participant;

        public ParticipantFellEvent(ICourseParticipant participant)
        {
            Participant = participant;
        }
    }

    public readonly struct FinishLineCrossedEvent
    {
        public readonly ICourseParticipant Participant;

        public FinishLineCrossedEvent(ICourseParticipant participant)
        {
            Participant = participant;
        }
    }

    /// <summary>Countdown tick (3, 2, 1) and 0 = GO.</summary>
    public readonly struct RaceCountdownEvent
    {
        public readonly int SecondsLeft;

        public RaceCountdownEvent(int secondsLeft)
        {
            SecondsLeft = secondsLeft;
        }
    }

    public readonly struct RaceFinishedEvent
    {
        public readonly ICourseParticipant Participant;
        public readonly float Time;
        public readonly int Placement;
        public readonly bool Qualified;

        public RaceFinishedEvent(ICourseParticipant participant, float time, int placement, bool qualified)
        {
            Participant = participant;
            Time = time;
            Placement = placement;
            Qualified = qualified;
        }
    }

    public readonly struct RaceTimeUpEvent
    {
    }
}
