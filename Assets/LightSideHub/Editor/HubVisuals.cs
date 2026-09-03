using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide.Hub
{
    /// <summary>How prominently a status line reads.</summary>
    internal enum HubStatus
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// The Hub's own visual vocabulary. It deliberately duplicates nothing from LightSide.Core: the
    /// Hub compiles against no package, because it is what installs them. The design tokens are the
    /// family's, so the two read as one product.
    /// </summary>
    internal static class HubVisuals
    {
        private const string StyleSheetName = "LightSideHub";
        private static StyleSheet styleSheet;

        /// <summary>Attaches the Hub stylesheet and marks the tree for the active editor skin.</summary>
        /// <exception cref="InvalidOperationException">The stylesheet is missing from the package.</exception>
        public static void Attach(VisualElement root)
        {
            styleSheet ??= Resources.Load<StyleSheet>(StyleSheetName)
                           ?? throw new InvalidOperationException(
                               $"The Hub stylesheet '{StyleSheetName}' is missing from the package.");

            root.styleSheets.Add(styleSheet);
            root.AddToClassList("hub");
            root.AddToClassList("hub__root");
            root.EnableInClassList("hub--dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("hub--light", !EditorGUIUtility.isProSkin);
        }

        public static VisualElement Card()
        {
            var card = new VisualElement();
            card.AddToClassList("hub__card");
            return card;
        }

        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.AddToClassList("hub__row");
            foreach (var child in children)
                if (child != null) row.Add(child);
            return row;
        }

        /// <summary>An element that pushes whatever follows it to the trailing edge of a row.</summary>
        public static VisualElement Spacer()
        {
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.AddToClassList("hub__spacer");
            return spacer;
        }

        public static Label Title(string text) => Styled(text, "hub__title");

        public static Label Subtitle(string text) => Styled(text, "hub__subtitle");

        public static Label Text(string text) => Styled(text, "hub__text");

        public static Label Muted(string text) => Styled(text, "hub__muted");

        public static Label Empty(string text) => Styled(text, "hub__empty");

        public static Label Badge(string text, string modifier = null)
        {
            var badge = Styled(text, "hub__badge");
            if (!string.IsNullOrEmpty(modifier)) badge.AddToClassList("hub__badge--" + modifier);
            return badge;
        }

        public static Button Button(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.AddToClassList("hub__button");
            return button;
        }

        public static Button PrimaryButton(string text, Action clicked)
        {
            var button = Button(text, clicked);
            button.AddToClassList("hub__button--primary");
            return button;
        }

        public static Button QuietButton(string text, Action clicked)
        {
            var button = Button(text, clicked);
            button.AddToClassList("hub__button--quiet");
            return button;
        }

        public static TextField Field()
        {
            var field = new TextField();
            field.AddToClassList("hub__field");
            return field;
        }

        public static VisualElement Separator()
        {
            var separator = new VisualElement { pickingMode = PickingMode.Ignore };
            separator.AddToClassList("hub__separator");
            return separator;
        }

        public static Label Status(string text, HubStatus kind)
        {
            var label = Styled(text, "hub__status");
            label.AddToClassList(kind switch
            {
                HubStatus.Warning => "hub__status--warn",
                HubStatus.Error => "hub__status--error",
                _ => "hub__status--info",
            });
            return label;
        }

        private static Label Styled(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
