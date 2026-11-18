using KnockRush.Core.Events;
using UnityEngine;

namespace KnockRush.Core.Platform
{
    /// <summary>
    /// Lives on the persistent AppRoot GameObject and forwards Unity application
    /// callbacks to the <see cref="EventBus"/>. Networking (Phase 4) uses the pause event to
    /// handle mobile backgrounding; the backend (Phase 7) uses it to flush pending saves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationLifecycleRelay : MonoBehaviour
    {
        private void OnApplicationPause(bool pauseStatus)
        {
            EventBus.Publish(new ApplicationPausedEvent(pauseStatus));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            EventBus.Publish(new ApplicationFocusChangedEvent(hasFocus));
        }

        private void OnApplicationQuit()
        {
            EventBus.Publish(new ApplicationQuittingEvent());
        }
    }
}
