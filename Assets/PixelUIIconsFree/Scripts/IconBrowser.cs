using System.Linq;
using UnityEngine;

namespace HeyHeyThere.PixelUIIconsFree
{
    /// <summary>
    /// The demo scene: every icon in a grid, a tab per folder and a search by name; a click shows the
    /// icon large and at 1x to 4x. Drawn with IMGUI, so it needs no input package.
    /// </summary>
    public class IconBrowser : MonoBehaviour
    {
        [Tooltip("Each icon's folder.")]
        public string[] groups;
        public Sprite[] icons;

        const int Scale = 3, Cell = 16 * Scale + 10, Side = 240;
        string[] folders, tabs;
        int tab, picked;
        string search = "";
        Vector2 scroll;
        GUIStyle slot, lit, field;

        void Start()
        {
            folders = groups.Distinct().ToArray();
            tabs = new[] { $"All {icons.Length}" }
                .Concat(folders.Select(f => $"{char.ToUpper(f[0])}{f.Substring(1)} {groups.Count(g => g == f)}")).ToArray();
        }

        void OnGUI()
        {
            DemoStyle.Init();
            if (slot == null)
            {
                slot = Box(new Color(0.16f, 0.15f, 0.24f), new Color(0.23f, 0.21f, 0.34f));
                lit = Box(new Color(0.45f, 0.4f, 0.85f), new Color(0.45f, 0.4f, 0.85f));
                field = new GUIStyle(GUI.skin.textField) { fontSize = 14, padding = new RectOffset(8, 8, 5, 5) };
            }
            float width = Screen.width - 32;
            GUILayout.BeginArea(new Rect(16, 12, width, Screen.height - 24));
            DemoStyle.Flow(width, tabs, i =>
            {
                if (GUILayout.Toggle(i == tab, tabs[i], DemoStyle.Button))
                    tab = i;
            });
            var shown = Enumerable.Range(0, icons.Length).Where(Shown).ToArray();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", DemoStyle.Hint, GUILayout.ExpandWidth(false));
            search = GUILayout.TextField(search, field, GUILayout.Width(200));
            GUILayout.Label($"  {shown.Length} icons. Click one to see it at 1x to 4x.", DemoStyle.Hint);
            GUILayout.EndHorizontal();
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            int columns = Mathf.Max(1, (int)(width - Side - 40) / Cell);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Width(columns * Cell + 20));
            var area = GUILayoutUtility.GetRect(columns * Cell, (shown.Length + columns - 1) / columns * Cell);
            for (int k = 0; k < shown.Length; k++)
            {
                var cell = new Rect(area.x + k % columns * Cell, area.y + k / columns * Cell, Cell - 4, Cell - 4);
                if (GUI.Button(cell, GUIContent.none, shown[k] == picked ? lit : slot))
                    picked = shown[k];
                Draw(new Rect(cell.x + 3, cell.y + 3, 16 * Scale, 16 * Scale), icons[shown[k]]);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginVertical(GUILayout.Width(Side));
            var icon = icons[picked];
            GUILayout.Label(icon.name, DemoStyle.Hint);
            GUILayout.Label($"Icons/{groups[picked]}/{icon.name}.png", DemoStyle.Hint);
            GUILayout.Space(8);
            var big = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(false));
            GUI.Box(big, GUIContent.none, slot);
            Draw(big, icon);
            GUILayout.Space(12);
            var sizes = GUILayoutUtility.GetRect(Side, 64, GUILayout.ExpandWidth(false));
            float x = sizes.x;
            for (int s = 1; s <= 4; s++)
            {
                Draw(new Rect(x, sizes.yMax - 16 * s, 16 * s, 16 * s), icon);
                x += 16 * s + 12;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        bool Shown(int i) =>
            (tab == 0 || groups[i] == folders[tab - 1])
            && icons[i].name.Replace('_', ' ').IndexOf(search.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>The sprite over the rect, its pixels square as the importer's point filtering keeps them.</summary>
        static void Draw(Rect r, Sprite s)
        {
            var t = s.texture;
            var uv = s.textureRect;
            GUI.DrawTextureWithTexCoords(r, t, new Rect(uv.x / t.width, uv.y / t.height, uv.width / t.width, uv.height / t.height));
        }

        static GUIStyle Box(Color normal, Color hover)
        {
            var style = new GUIStyle();
            style.normal.background = Fill(normal);
            style.hover.background = style.active.background = Fill(hover);
            return style;
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
