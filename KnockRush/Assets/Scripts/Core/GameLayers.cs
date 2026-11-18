namespace KnockRush.Core
{
    /// <summary>
    /// Physics layers used by gameplay. Indices must match ProjectSettings/TagManager
    /// (applied by KnockRush > Setup).
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Player = 6;
        public const int Obstacle = 7;
        public const int Trigger = 8;

        public const string PlayerName = "Player";
        public const string ObstacleName = "Obstacle";
        public const string TriggerName = "Trigger";

        /// <summary>What characters and the camera collide with: level geometry and obstacles.</summary>
        public const int EnvironmentMask = (1 << Default) | (1 << Obstacle);

        public const int PlayerMask = 1 << Player;
    }
}
