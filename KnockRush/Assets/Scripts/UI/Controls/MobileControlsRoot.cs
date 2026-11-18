using UnityEngine;

namespace KnockRush.UI.Controls
{
    /// <summary>
    /// Shows the on-screen joystick and buttons on phones/tablets. In the Editor they are shown
    /// too (mouse can drive them) unless <see cref="hideInEditor"/> is set; on desktop builds
    /// they are hidden because keyboard/gamepad are used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MobileControlsRoot : MonoBehaviour
    {
        [SerializeField] private GameObject controlsContainer;
        [SerializeField] private bool hideInEditor;

        private void Awake()
        {
            var show = Application.isMobilePlatform || (Application.isEditor && !hideInEditor);
            var target = controlsContainer != null ? controlsContainer : gameObject;
            target.SetActive(show);
        }
    }
}
