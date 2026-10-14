using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeyHeyThere.PixelUIIconsFree
{
    /// <summary>The demo scenes' IMGUI look: dark buttons with a violet pick, muted labels.</summary>
    static class DemoStyle
    {
        public static GUIStyle Button, Label, Hint;

        /// <summary>Call from OnGUI, where GUI.skin exists.</summary>
        public static void Init()
        {
            if (Button != null)
                return;
            Button = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(12, 12, 5, 5), margin = new RectOffset(0, 4, 0, 6) };
            Button.normal.background = Fill(new Color(0.16f, 0.15f, 0.24f));
            Button.hover.background = Fill(new Color(0.23f, 0.21f, 0.34f));
            Button.active.background = Button.onNormal.background = Button.onHover.background = Button.onActive.background = Fill(new Color(0.45f, 0.4f, 0.85f));
            Button.normal.textColor = Button.hover.textColor = new Color(0.8f, 0.82f, 0.92f);
            Button.active.textColor = Button.onNormal.textColor = Button.onHover.textColor = Button.onActive.textColor = Color.white;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            Label.normal.textColor = new Color(0.62f, 0.64f, 0.76f);
            Hint = new GUIStyle(Label) { alignment = TextAnchor.MiddleLeft };
        }

        /// <summary>A toolbar of names; true when the pick changed.</summary>
        public static bool Choice(ref int value, string[] names)
        {
            int v = GUILayout.Toolbar(value, names, Button, GUI.ToolbarButtonSize.FitToContents, GUILayout.ExpandWidth(false));
            if (v == value)
                return false;
            value = v;
            return true;
        }

        /// <summary>Buttons left to right, wrapping at the width given; draw(i) draws the i-th.</summary>
        public static void Flow(float width, IList<string> names, Action<int> draw)
        {
            float used = 0f;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Count; i++)
            {
                float w = Button.CalcSize(new GUIContent(names[i])).x + Button.margin.horizontal;
                if (used > 0f && used + w > width)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    used = 0f;
                }
                used += w;
                draw(i);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        static Texture2D Fill(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
