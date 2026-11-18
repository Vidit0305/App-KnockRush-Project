using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KnockRush.UI.Framework
{
    /// <summary>
    /// Stack-based screen navigation. Pages replace each other (with a slide/fade transition);
    /// overlays and HUD screens stack on top of the current page.
    /// </summary>
    public sealed class ScreenNavigator
    {
        private readonly AppUI app;
        private readonly VisualElement layer;
        private readonly Dictionary<Type, UIScreen> screens = new Dictionary<Type, UIScreen>();
        private readonly List<UIScreen> stack = new List<UIScreen>();

        public ScreenNavigator(AppUI app, VisualElement layer)
        {
            this.app = app;
            this.layer = layer;
        }

        public event Action<UIScreen> Changed;

        public UIScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;

        public IReadOnlyList<UIScreen> Stack => stack;

        /// <summary>The screen of type <typeparamref name="T"/> (created on first use).</summary>
        public T Get<T>() where T : UIScreen, new()
        {
            if (!screens.TryGetValue(typeof(T), out var screen))
            {
                screen = new T();
                screen.Create(app);
                screen.Root.AddToClassList("kr-screen--hidden");
                screen.Root.style.display = DisplayStyle.None;
                layer.Add(screen.Root);
                screens.Add(typeof(T), screen);
            }

            return (T)screen;
        }

        public bool IsShowing<T>() where T : UIScreen => stack.Exists(s => s is T);

        /// <summary>Clears the stack and shows <typeparamref name="T"/> as the only screen.</summary>
        public T SetRoot<T>() where T : UIScreen, new()
        {
            var screen = Get<T>();
            if (stack.Count == 1 && stack[0] == screen)
            {
                return screen;
            }

            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] != screen)
                {
                    Hide(stack[i]);
                }
            }

            stack.Clear();
            stack.Add(screen);
            Show(screen);
            Changed?.Invoke(screen);
            return screen;
        }

        /// <summary>Hides every screen (scenes without menu UI).</summary>
        public void Clear()
        {
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                Hide(stack[i]);
            }

            stack.Clear();
            Changed?.Invoke(null);
        }

        public T Push<T>() where T : UIScreen, new()
        {
            var screen = Get<T>();
            if (Top == screen)
            {
                screen.OnShow();
                return screen;
            }

            stack.Remove(screen);
            if (screen.Layer == ScreenLayer.Page)
            {
                // Pages cover everything below them.
                foreach (var below in stack)
                {
                    if (below.IsVisible)
                    {
                        Hide(below);
                    }
                }
            }

            stack.Add(screen);
            Show(screen);
            Changed?.Invoke(screen);
            return screen;
        }

        /// <summary>Removes the top screen and reveals the one(s) below.</summary>
        public void Pop()
        {
            if (stack.Count <= 1)
            {
                return;
            }

            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            Hide(top);
            RevealTop();
            Changed?.Invoke(Top);
        }

        /// <summary>Removes a specific screen wherever it is in the stack (e.g. a finished loading overlay).</summary>
        public void Remove<T>() where T : UIScreen
        {
            var index = stack.FindIndex(s => s is T);
            if (index < 0)
            {
                return;
            }

            var screen = stack[index];
            stack.RemoveAt(index);
            Hide(screen);
            RevealTop();
            Changed?.Invoke(Top);
        }

        public void Tick(float deltaTime)
        {
            for (var i = 0; i < stack.Count; i++)
            {
                if (stack[i].IsVisible)
                {
                    stack[i].Tick(deltaTime);
                }
            }
        }

        public bool Back()
        {
            return Top != null && Top.OnBack();
        }

        private void RevealTop()
        {
            // Show the topmost page and everything stacked above it.
            var firstVisible = stack.FindLastIndex(s => s.Layer == ScreenLayer.Page);
            for (var i = Math.Max(0, firstVisible); i < stack.Count; i++)
            {
                if (!stack[i].IsVisible)
                {
                    Show(stack[i]);
                }
            }
        }

        private static void Show(UIScreen screen)
        {
            screen.Root.style.display = DisplayStyle.Flex;
            screen.Root.BringToFront();
            screen.IsVisible = true;
            screen.OnShow();

            // Next frame: remove the hidden class so the USS transition animates in.
            screen.Root.schedule.Execute(() => screen.Root.RemoveFromClassList("kr-screen--hidden"));
        }

        private static void Hide(UIScreen screen)
        {
            if (!screen.IsVisible)
            {
                return;
            }

            screen.IsVisible = false;
            screen.OnHide();
            screen.Root.AddToClassList("kr-screen--hidden");
            screen.Root.style.display = DisplayStyle.None;
        }
    }
}
