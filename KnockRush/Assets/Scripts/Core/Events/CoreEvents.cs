namespace KnockRush.Core.Events
{
    /// <summary>Published once when AppRoot has initialised every core service.</summary>
    public readonly struct AppInitializedEvent
    {
    }

    public readonly struct SceneLoadStartedEvent
    {
        public readonly string SceneName;

        public SceneLoadStartedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public readonly struct SceneLoadProgressEvent
    {
        public readonly string SceneName;

        /// <summary>Normalised 0..1 progress.</summary>
        public readonly float Progress;

        public SceneLoadProgressEvent(string sceneName, float progress)
        {
            SceneName = sceneName;
            Progress = progress;
        }
    }

    public readonly struct SceneLoadCompletedEvent
    {
        public readonly string SceneName;

        public SceneLoadCompletedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    /// <summary>Mobile app sent to background (true) or resumed (false).</summary>
    public readonly struct ApplicationPausedEvent
    {
        public readonly bool IsPaused;

        public ApplicationPausedEvent(bool isPaused)
        {
            IsPaused = isPaused;
        }
    }

    public readonly struct ApplicationFocusChangedEvent
    {
        public readonly bool HasFocus;

        public ApplicationFocusChangedEvent(bool hasFocus)
        {
            HasFocus = hasFocus;
        }
    }

    public readonly struct ApplicationQuittingEvent
    {
    }
}
