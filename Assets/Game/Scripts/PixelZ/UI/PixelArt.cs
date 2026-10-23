using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lienzo de píxeles para dibujar por código (ruleta, tablero, mesa y cartas del casino) con estética pixel art:
// textura pequeña con filtro Point que se muestra ampliada, fuente 3x5, palos de la baraja y sombreado con tramado.
public class PixelCanvas
{
    public readonly int W, H;
    public readonly Texture2D Texture;
    public readonly Color32[] Pixels;

    private static readonly float[] Bayer =
    {
        0f, 8f, 2f, 10f,
        12f, 4f, 14f, 6f,
        3f, 11f, 1f, 9f,
        15f, 7f, 13f, 5f,
    };

    public PixelCanvas(int w, int h)
    {
        W = w;
        H = h;
        Texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Pixels = new Color32[w * h];
    }

    public void Clear(Color32 c)
    {
        for (int i = 0; i < Pixels.Length; i++) Pixels[i] = c;
    }

    public void Apply()
    {
        Texture.SetPixels32(Pixels);
        Texture.Apply(false);
    }

    // Coordenadas con y hacia abajo (como en un editor de pixel art).
    public void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        Pixels[(H - 1 - y) * W + x] = c;
    }

    public void Blend(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = (H - 1 - y) * W + x;
        Color dst = Pixels[i];
        Color o = Color.Lerp(dst, new Color(c.r, c.g, c.b, 1f), c.a);
        o.a = Mathf.Max(dst.a, c.a);
        Pixels[i] = o;
    }

    public void Rect(int x, int y, int w, int h, Color32 c)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Set(i, j, c);
    }

    // Caja con bisel: borde claro arriba/izquierda y oscuro abajo/derecha, para que parezca en relieve.
    public void Bevel(int x, int y, int w, int h, Color fill, float light = 1.3f, float dark = 0.65f, bool dither = false)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
            {
                float k = 1f;
                if (j == y || i == x) k = light;
                else if (j == y + h - 1 || i == x + w - 1) k = dark;
                else if (dither) k = 0.95f + 0.1f * Noise(i, j);
                Set(i, j, Shade(fill, k, i, j));
            }
    }

    // Multiplica el brillo y lo cuantiza con tramado Bayer (gradientes con "escalones" de pixel art).
    public static Color32 Shade(Color c, float k, int x, int y, float steps = 10f)
    {
        float t = Bayer[(x & 3) + (y & 3) * 4] / 16f - 0.5f;
        float r = Mathf.Floor(Mathf.Clamp01(c.r * k) * steps + 0.5f + t * 0.9f) / steps;
        float g = Mathf.Floor(Mathf.Clamp01(c.g * k) * steps + 0.5f + t * 0.9f) / steps;
        float b = Mathf.Floor(Mathf.Clamp01(c.b * k) * steps + 0.5f + t * 0.9f) / steps;
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), c.a);
    }

    public static float Noise(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h & 1023) / 1023f;
    }

    // ---------- Texto ----------

    private static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
    {
        { '0', new[] { "###", "#.#", "#.#", "#.#", "###" } },
        { '1', new[] { ".#.", "##.", ".#.", ".#.", "###" } },
        { '2', new[] { "###", "..#", "###", "#..", "###" } },
        { '3', new[] { "###", "..#", ".##", "..#", "###" } },
        { '4', new[] { "#.#", "#.#", "###", "..#", "..#" } },
        { '5', new[] { "###", "#..", "###", "..#", "###" } },
        { '6', new[] { "###", "#..", "###", "#.#", "###" } },
        { '7', new[] { "###", "..#", "..#", ".#.", ".#." } },
        { '8', new[] { "###", "#.#", "###", "#.#", "###" } },
        { '9', new[] { "###", "#.#", "###", "..#", "###" } },
        { 'A', new[] { ".#.", "#.#", "###", "#.#", "#.#" } },
        { 'J', new[] { "..#", "..#", "..#", "#.#", ".#." } },
        { 'Q', new[] { ".#.", "#.#", "#.#", "#.#", ".##" } },
        { 'K', new[] { "#.#", "#.#", "##.", "#.#", "#.#" } },
        { 'P', new[] { "##.", "#.#", "##.", "#..", "#.." } },
        { 'R', new[] { "##.", "#.#", "##.", "#.#", "#.#" } },
        { 'I', new[] { "###", ".#.", ".#.", ".#.", "###" } },
        { 'M', new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#" } },
        { '-', new[] { "...", "...", "###", "...", "..." } },
        { ':', new[] { ".", "#", ".", "#", "." } },
        { ' ', new[] { "..", "..", "..", "..", ".." } },
    };

    public static int TextWidth(string s, int scale = 1)
    {
        int w = 0;
        foreach (char ch in s)
            if (Font.TryGetValue(ch, out string[] g)) w += (g[0].Length + 1) * scale;
        return Mathf.Max(0, w - scale);
    }

    public void Text(string s, int x, int y, Color32 c, int scale = 1, Color32? shadow = null)
    {
        if (shadow.HasValue) Text(s, x + scale, y + scale, shadow.Value, scale);
        foreach (char ch in s)
        {
            if (!Font.TryGetValue(ch, out string[] g)) continue;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < g[row].Length; col++)
                    if (g[row][col] == '#') Rect(x + col * scale, y + row * scale, scale, scale, c);
            x += (g[0].Length + 1) * scale;
        }
    }

    public void TextCentered(string s, int cx, int cy, Color32 c, int scale = 1, Color32? shadow = null)
    {
        Text(s, cx - TextWidth(s, scale) / 2, cy - 5 * scale / 2, c, scale, shadow);
    }

    // ---------- Palos (0 picas, 1 corazones, 2 diamantes, 3 tréboles) ----------

    private static readonly string[][] Suits5 =
    {
        new[] { "..#..", ".###.", "#####", "..#..", ".###." },
        new[] { ".#.#.", "#####", "#####", ".###.", "..#.." },
        new[] { "..#..", ".###.", "#####", ".###.", "..#.." },
        new[] { "..#..", "#.#.#", "#####", "..#..", ".###." },
    };

    private static readonly string[][] Suits7 =
    {
        new[] { "...#...", "..###..", ".#####.", "#######", "##.#.##", "...#...", "..###.." },
        new[] { ".##.##.", "#######", "#######", "#######", ".#####.", "..###..", "...#..." },
        new[] { "...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..." },
        new[] { "..###..", "..###..", "##.#.##", "#######", "##.#.##", "...#...", "..###.." },
    };

    public void Suit(int suit, int x, int y, Color32 c, bool big, int scale = 1)
    {
        string[] g = big ? Suits7[suit] : Suits5[suit];
        for (int row = 0; row < g.Length; row++)
            for (int col = 0; col < g[row].Length; col++)
                if (g[row][col] == '#') Rect(x + col * scale, y + row * scale, scale, scale, c);
    }

    public void Disc(float cx, float cy, float r, Color c)
    {
        for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
            for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r * r) Blend(x, y, c);
            }
    }

    // RawImage que muestra el lienzo ampliado `scale` veces (cada píxel = un bloque).
    public RawImage Show(Transform parent, string name, int scale)
    {
        RectTransform rt = UIFactory.Rect(name, parent);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = Texture;
        img.raycastTarget = false;
        rt.sizeDelta = new Vector2(W * scale, H * scale);
        return img;
    }
}
