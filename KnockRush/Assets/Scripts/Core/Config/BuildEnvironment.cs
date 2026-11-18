namespace KnockRush.Core.Config
{
    /// <summary>
    /// Which backend / networking environment this build talks to.
    /// Selected per build via <see cref="GameConfig"/>; later phases map this to
    /// Photon App IDs, Firebase projects and server fleets.
    /// </summary>
    public enum BuildEnvironment
    {
        Development = 0,
        Staging = 1,
        Production = 2
    }
}
