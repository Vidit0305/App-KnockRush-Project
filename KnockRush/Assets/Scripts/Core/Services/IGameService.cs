using System.Threading;
using System.Threading.Tasks;

namespace KnockRush.Core.Services
{
    /// <summary>
    /// A long-lived service created and initialised by <see cref="AppRoot"/> at startup.
    /// Services are plain C# objects (not MonoBehaviours) so they are testable and
    /// independent of scene lifetime. Implement <see cref="System.IDisposable"/> if the
    /// service needs cleanup on shutdown.
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Called once, in registration order, during application startup.
        /// Return <see cref="Task.CompletedTask"/> for synchronous services.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
