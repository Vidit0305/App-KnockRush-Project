using Fusion;

namespace KnockRush.GameModes
{
    public enum MatchPhase : byte
    {
        Lobby = 0,
        Loading = 1,
        Countdown = 2,
        Playing = 3,
        RoundResults = 4,
        MatchResults = 5
    }

    public enum RecordStatus : byte
    {
        Empty = 0,
        Active = 1,
        Qualified = 2,
        Eliminated = 3
    }

    /// <summary>Replicated per-participant match record (humans and bots). Survives map changes.</summary>
    public struct PlayerRecord : INetworkStruct
    {
        public PlayerRef Player;
        public int UserHash;
        public RecordStatus Status;
        public byte ColorIndex;
        public short Placement;
        public short RoundPlacement;
        public byte RoundsCleared;
        public NetworkBool IsBot;
        public NetworkBool Disconnected;
        public int LeftTick;

        public bool InUse => Status != RecordStatus.Empty;
    }
}
