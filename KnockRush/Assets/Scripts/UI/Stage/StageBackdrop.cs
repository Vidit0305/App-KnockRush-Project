using UnityEngine;

namespace KnockRush.UI.Stage
{
    /// <summary>
    /// A painted backdrop quad parented to the stage camera: it always fills the screen
    /// ("cover" fit, cropping the image rather than letter-boxing) on any phone aspect ratio,
    /// so the 3D bean stands in front of the artwork.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class StageBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float distance = 40f;
        [SerializeField] private float imageAspect = 16f / 9f;

        private float appliedAspect;
        private float appliedFov;

        public void Configure(Camera camera, float atDistance, float aspect)
        {
            targetCamera = camera;
            distance = atDistance;
            imageAspect = aspect;
            Fit();
        }

        private void LateUpdate()
        {
            if (targetCamera != null && (!Mathf.Approximately(targetCamera.aspect, appliedAspect) || !Mathf.Approximately(targetCamera.fieldOfView, appliedFov)))
            {
                Fit();
            }
        }

        private void Fit()
        {
            if (targetCamera == null)
            {
                return;
            }

            appliedAspect = targetCamera.aspect;
            appliedFov = targetCamera.fieldOfView;
            var viewHeight = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var viewWidth = viewHeight * targetCamera.aspect;
            var height = Mathf.Max(viewHeight, viewWidth / imageAspect) * 1.01f;
            transform.localPosition = new Vector3(0f, 0f, distance);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(height * imageAspect, height, 1f);
        }
    }
}
