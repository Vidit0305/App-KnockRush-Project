using UnityEngine.UIElements;

namespace KnockRush.UI.Framework
{
    /// <summary>How a screen sits in the navigator.</summary>
    public enum ScreenLayer
    {
        /// <summary>Full screen; hides the screen below it.</summary>
        Page = 0,

        /// <summary>Drawn on top of the current page (dialogs, loading); the page stays visible.</summary>
        Overlay = 1,

        /// <summary>In-match screens: never block touches outside their own buttons.</summary>
        Hud = 2
    }

    /// <summary>
    /// One UI Toolkit screen, built in code from the shared theme. Screens are created once (lazily)
    /// and reused; <see cref="OnShow"/> refreshes their data every time they appear.
    /// </summary>
    public abstract class UIScreen
    {
        public VisualElement Root { get; private set; }

        public AppUI App { get; private set; }

        protected ScreenNavigator Nav => App.Navigator;

        public virtual ScreenLayer Layer => ScreenLayer.Page;

        /// <summary>True when the 3D menu stage (the player's bean) should be visible behind this screen.</summary>
        public virtual bool ShowsStage => false;

        /// <summary>True for the splash and loading screens: the full-screen key art shows behind them.</summary>
        public virtual bool UsesKeyArt => false;

        public bool IsVisible { get; internal set; }

        internal void Create(AppUI app)
        {
            App = app;
            Root = new VisualElement { name = GetType().Name };
            Root.AddToClassList("kr-screen");
            if (Layer == ScreenLayer.Hud)
            {
                Root.pickingMode = PickingMode.Ignore;
            }

            Build(Root);
        }

        /// <summary>Creates the element tree once.</summary>
        protected abstract void Build(VisualElement root);

        /// <summary>Called whenever the screen becomes visible (refresh data here).</summary>
        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        /// <summary>Per-frame update while visible.</summary>
        public virtual void Tick(float deltaTime)
        {
        }

        /// <summary>Android back / Escape. Return false to ignore.</summary>
        public virtual bool OnBack()
        {
            Nav.Pop();
            return true;
        }
    }
}
