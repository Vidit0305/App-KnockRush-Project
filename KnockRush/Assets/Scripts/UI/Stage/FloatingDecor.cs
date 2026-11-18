using UnityEngine;

namespace KnockRush.UI.Stage
{
    /// <summary>Menu backdrop shape: bobs and tumbles slowly (cheap, no physics).</summary>
    [DisallowMultipleComponent]
    public sealed class FloatingDecor : MonoBehaviour
    {
        private Vector3 origin;
        private Vector3 spin;
        private float phase;

        private void Start()
        {
            origin = transform.localPosition;
            var seed = Mathf.Abs(origin.GetHashCode());
            phase = seed % 628 / 100f;
            spin = new Vector3(seed % 23 - 11, seed % 31 - 15, seed % 17 - 8);
        }

        private void Update()
        {
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * 0.8f + phase) * 0.35f);
            transform.Rotate(spin * Time.deltaTime, Space.Self);
        }
    }
}
