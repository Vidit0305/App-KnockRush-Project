using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Framework
{
    /// <summary>
    /// Asset references the runtime UI needs (built by the editor UI builder into
    /// ScriptableObjects/UI/Resources/UiResources.asset, loaded from Resources at startup).
    /// </summary>
    public sealed class UiResources : ScriptableObject
    {
        public const string ResourcesPath = "UiResources";

        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private StyleSheet theme;
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;

        public PanelSettings PanelSettings => panelSettings;
        public StyleSheet Theme => theme;
        public Font DisplayFont => displayFont;
        public Font BodyFont => bodyFont;

        public static UiResources Load() => Resources.Load<UiResources>(ResourcesPath);

        public void Configure(PanelSettings panel, StyleSheet styleSheet, Font display, Font body)
        {
            panelSettings = panel;
            theme = styleSheet;
            displayFont = display;
            bodyFont = body;
        }
    }
}
