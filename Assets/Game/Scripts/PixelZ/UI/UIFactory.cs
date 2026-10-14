using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Helpers para construir la interfaz por código (menú, HUD, pausa, tienda, game over).
public static class UIFactory
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    public static readonly Color Cream = new Color(1f, 0.95f, 0.82f);
    public static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.09f, 0.9f);
    public static readonly Color ButtonColor = new Color(0.24f, 0.15f, 0.1f, 0.96f);
    public static readonly Color ButtonHover = new Color(0.5f, 0.3f, 0.12f, 1f);
    public static readonly Color BorderColor = new Color(0.95f, 0.72f, 0.3f, 1f);

    private static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static Canvas CreateCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Coloca un RectTransform con un ancla puntual.
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static Image Image(Transform parent, string name, Sprite sprite, Color color)
    {
        RectTransform rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // Imagen a pantalla completa que cubre todo el área manteniendo proporción (como "cover" en CSS).
    public static Image FullscreenImage(Transform parent, Sprite sprite, Color color)
    {
        Image img = Image(parent, "Background", sprite, color);
        Stretch(img.rectTransform);
        if (sprite != null)
        {
            var fitter = img.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
        return img;
    }

    public static Image Panel(Transform parent, string name, Color color, bool border = true)
    {
        Image img = Image(parent, name, null, color);
        img.raycastTarget = true;
        if (border)
        {
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(6f, -6f);
        }
        return img;
    }

    public static Text Text(Transform parent, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
    {
        RectTransform rt = Rect("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = true;
        if (outline)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.05f, 0.03f, 0.02f, 1f);
            o.effectDistance = new Vector2(2f, -2f);
            var s = rt.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.6f);
            s.effectDistance = new Vector2(3f, -4f);
        }
        return t;
    }

    public static Button Button(Transform parent, string label, int fontSize, Action onClick, Vector2 size)
    {
        Image img = Panel(parent, "Button - " + label, ButtonColor);
        img.rectTransform.sizeDelta = size;
        var button = img.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());

        Text t = Text(img.transform, label, fontSize, Cream);
        Stretch(t.rectTransform);

        var fx = img.gameObject.AddComponent<UIButtonFx>();
        fx.Init(img, t);
        return button;
    }

    public static void SetLabel(Button b, string label)
    {
        b.GetComponentInChildren<Text>().text = label;
    }
}

// Efecto hover/click para los botones (cambia color y escala, funciona con el juego en pausa).
public class UIButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Color normal = UIFactory.ButtonColor;
    public Color hover = UIFactory.ButtonHover;
    public bool selected;

    private Image image;
    private Text label;
    private bool over, down;

    public void Init(Image img, Text text)
    {
        image = img;
        label = text;
    }

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
    }

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) { over = false; down = false; }
    public void OnPointerDown(PointerEventData e) => down = true;
    public void OnPointerUp(PointerEventData e) => down = false;

    private void OnDisable()
    {
        over = down = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        bool hot = over || selected;
        image.color = hot ? hover : normal;
        if (label != null) label.color = hot ? UIFactory.Gold : UIFactory.Cream;
        float target = down ? 0.96f : over ? 1.06f : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
    }
}
