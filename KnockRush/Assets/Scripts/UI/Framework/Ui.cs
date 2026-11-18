using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Framework
{
    /// <summary>Small factory helpers so screens read like layout code.</summary>
    public static class Ui
    {
        public static VisualElement Row(params string[] classes) => Element("kr-row", classes);

        public static VisualElement Col(params string[] classes) => Element("kr-col", classes);

        public static VisualElement Spacer() => Element("kr-spacer");

        public static VisualElement Panel(params string[] classes) => Element("kr-panel", classes);

        public static VisualElement Element(string firstClass, params string[] classes)
        {
            var element = new VisualElement();
            element.AddToClassList(firstClass);
            foreach (var c in classes)
            {
                element.AddToClassList(c);
            }

            return element;
        }

        public static Label Label(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var c in classes)
            {
                label.AddToClassList(c);
            }

            return label;
        }

        public static Label Title(string text) => Label(text, "kr-title", "kr-display");

        public static Label Heading(string text) => Label(text, "kr-heading", "kr-display");

        public static Button Button(string text, Action onClick, params string[] classes)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("kr-btn");
            button.AddToClassList("kr-display");
            foreach (var c in classes)
            {
                button.AddToClassList(c);
            }

            return button;
        }

        /// <summary>Screen header: back button, title, flexible space, optional right-hand content.</summary>
        public static VisualElement Header(string title, Action onBack, VisualElement right = null)
        {
            var header = Row("kr-topbar");
            if (onBack != null)
            {
                header.Add(Button("BACK", onBack, "kr-btn--ghost", "kr-btn--small"));
            }

            var label = Heading(title);
            label.style.marginLeft = 16;
            header.Add(label);
            header.Add(Spacer());
            if (right != null)
            {
                header.Add(right);
            }

            return header;
        }

        public static VisualElement CurrencyPill(string iconClass, out Label value)
        {
            var pill = Row("kr-pill");
            pill.Add(Element(iconClass));
            value = Label("0", "kr-display");
            pill.Add(value);
            return pill;
        }

        public static VisualElement ProgressBar(out VisualElement fill, Color? color = null)
        {
            var bar = Element("kr-bar");
            fill = Element("kr-bar__fill");
            if (color.HasValue)
            {
                fill.style.backgroundColor = color.Value;
            }

            bar.Add(fill);
            return bar;
        }

        public static void SetProgress(VisualElement fill, float value01)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(value01) * 100f);
        }

        /// <summary>A round colour swatch (cosmetic preview) inside a card.</summary>
        public static VisualElement Swatch(Color color, Color? accent = null)
        {
            var swatch = Element("kr-swatch");
            swatch.style.backgroundColor = color;
            if (accent.HasValue)
            {
                var dot = new VisualElement();
                dot.style.position = Position.Absolute;
                dot.style.width = 34;
                dot.style.height = 34;
                dot.style.right = -2;
                dot.style.bottom = -2;
                dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 17;
                dot.style.backgroundColor = accent.Value;
                swatch.Add(dot);
            }

            return swatch;
        }

        public static ScrollView Scroll(ScrollViewMode mode = ScrollViewMode.Vertical)
        {
            var scroll = new ScrollView(mode);
            scroll.AddToClassList("kr-scroll");
            scroll.horizontalScrollerVisibility = mode == ScrollViewMode.Horizontal ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = mode == ScrollViewMode.Vertical ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            return scroll;
        }

        public static TextField Input(string placeholder, int maxLength)
        {
            var field = new TextField { maxLength = maxLength };
            field.AddToClassList("kr-input");
            field.textEdition.placeholder = placeholder;
            field.textEdition.hidePlaceholderOnFocus = true;
            return field;
        }

        public static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{Mathf.FloorToInt(seconds - minutes * 60f):00}";
        }

        public static string FormatNumber(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Friendly text for a backend error (strips the status prefix).</summary>
        public static string ErrorText(Exception exception)
        {
            var message = exception?.GetBaseException().Message ?? "Something went wrong.";
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            return colon > 0 && colon < 24 ? message.Substring(colon + 2) : message;
        }
    }
}
