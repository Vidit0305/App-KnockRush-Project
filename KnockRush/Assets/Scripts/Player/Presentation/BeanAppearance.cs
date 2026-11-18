using UnityEngine;

namespace KnockRush.Player.Presentation
{
    /// <summary>
    /// Applies the player's body colour to the bean's toon material. Uses a per-character
    /// material instance (same shader variant), which keeps the SRP Batcher effective.
    /// Phase 8 extends this with patterns, costumes and accessories.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeanAppearance : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Placeholder palette until the customisation catalogue (Phase 8).</summary>
        public static readonly Color[] DefaultPalette =
        {
            new Color32(0xFF, 0x6A, 0x1A, 0xFF), // Rush Orange
            new Color32(0x5B, 0x2E, 0xFF, 0xFF), // Knock Purple
            new Color32(0x22, 0xC5, 0x8B, 0xFF), // Mint
            new Color32(0xFF, 0x4F, 0x9A, 0xFF), // Bubblegum
            new Color32(0x2D, 0x9C, 0xFF, 0xFF), // Sky
            new Color32(0xFF, 0xD1, 0x2B, 0xFF), // Sunny
            new Color32(0xE8, 0x3A, 0x3A, 0xFF), // Cherry
            new Color32(0x8B, 0xE0, 0x3C, 0xFF)  // Lime
        };

        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private Color bodyColor = new Color32(0xFF, 0x6A, 0x1A, 0xFF);

        private Material instance;

        public Color BodyColor
        {
            get => bodyColor;
            set
            {
                bodyColor = value;
                Apply();
            }
        }

        public void SetPaletteIndex(int index)
        {
            BodyColor = DefaultPalette[Mathf.Abs(index) % DefaultPalette.Length];
        }

        private void Awake()
        {
            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            Apply();
        }

        private void Apply()
        {
            if (bodyRenderer == null || !Application.isPlaying)
            {
                return;
            }

            if (instance == null)
            {
                instance = bodyRenderer.material; // creates the per-character instance
            }

            instance.SetColor(BaseColorId, bodyColor);
        }

        private void OnDestroy()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
    }
}
