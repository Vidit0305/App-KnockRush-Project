namespace KnockRush.Core.SceneManagement
{
    /// <summary>
    /// Names of scenes that are part of Build Settings. Map scenes are added in Phase 3/6
    /// (and later moved to Addressables), so they are not listed here.
    /// </summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string MainMenu = "MainMenu";

        /// <summary>Development movement test scene (Assets/Scenes/Dev/Playground.unity).</summary>
        public const string Playground = "Playground";

        /// <summary>First playable map (Assets/Scenes/Maps/HammerRush.unity).</summary>
        public const string HammerRush = "HammerRush";
    }
}
