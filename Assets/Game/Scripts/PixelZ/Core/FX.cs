using UnityEngine;

// Efectos visuales sencillos hechos con sprites: anillos, chispas, explosiones y temblor de cámara.
public static class FX
{
    public const string TopSortingLayer = "Layer 3";

    // Los efectos no deben oscurecerse con la iluminación 2D del castillo.
    public static void Unlit(SpriteRenderer sr)
    {
        Material m = PixelZDatabase.I != null ? PixelZDatabase.I.unlitMaterial : null;
        if (m != null) sr.sharedMaterial = m;
    }

    public static void Ring(Vector2 pos, Color color, float fromSize, float toSize, float duration, string sortingLayer = TopSortingLayer)
    {
        var go = new GameObject("FX Ring");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        FX.Unlit(sr);
        sr.sprite = PixelZDatabase.I.ring;
        sr.color = color;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 50;
        go.AddComponent<FXScaleFade>().Init(fromSize, toSize, duration);
    }

    public static void Glow(Vector2 pos, Color color, float fromSize, float toSize, float duration, string sortingLayer = TopSortingLayer)
    {
        var go = new GameObject("FX Glow");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        FX.Unlit(sr);
        sr.sprite = PixelZDatabase.I.glow;
        sr.color = color;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 49;
        go.AddComponent<FXScaleFade>().Init(fromSize, toSize, duration);
    }

    // Partículas cuadradas que salen disparadas y se desvanecen.
    public static void Burst(Vector2 pos, Color color, int count, float speed = 3f, float size = 0.09f, string sortingLayer = TopSortingLayer)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("FX Spark");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            FX.Unlit(sr);
            sr.sprite = PixelZDatabase.I.white;
            sr.color = color;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = 51;
            Vector2 v = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
            go.AddComponent<FXParticle>().Init(v, size * Random.Range(0.7f, 1.3f), Random.Range(0.3f, 0.6f));
        }
    }

    public static void Explosion(Vector2 pos, float size, string sortingLayer = TopSortingLayer)
    {
        var frames = PixelZDatabase.I.explosion;
        if (frames == null || frames.Length == 0) return;
        var go = new GameObject("FX Explosion");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var sr = go.AddComponent<SpriteRenderer>();
        FX.Unlit(sr);
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 52;
        var anim = go.AddComponent<SpriteAnimator>();
        anim.target = sr;
        anim.Play(frames, 18f, false);
        Object.Destroy(go, frames.Length / 18f);
    }

    public static void Shake(float strength, float duration)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake.Shake(strength, duration);
    }
}

public class FXScaleFade : MonoBehaviour
{
    private float from, to, duration, t;
    private SpriteRenderer sr;
    private Color baseColor;

    public void Init(float fromSize, float toSize, float dur)
    {
        from = fromSize;
        to = toSize;
        duration = dur;
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        transform.localScale = Vector3.one * from;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
        Color c = baseColor;
        c.a *= 1f - k;
        sr.color = c;
        if (k >= 1f) Destroy(gameObject);
    }
}

public class FXParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float life, maxLife;
    private SpriteRenderer sr;
    private Color baseColor;

    public void Init(Vector2 v, float size, float lifetime)
    {
        velocity = v;
        life = maxLife = lifetime;
        sr = GetComponent<SpriteRenderer>();
        baseColor = sr.color;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= 1f - 4f * Time.deltaTime;
        Color c = baseColor;
        c.a *= Mathf.Clamp01(life / maxLife);
        sr.color = c;
        if (life <= 0f) Destroy(gameObject);
    }
}

// Temblor de cámara compatible con CameraFollow: sólo aplica la diferencia de offset de cada frame.
public class CameraShake : MonoBehaviour
{
    private float strength, time, duration;
    private Vector3 lastOffset;

    public void Shake(float s, float d)
    {
        if (s < strength * (time / Mathf.Max(duration, 0.001f))) return;
        strength = s;
        duration = time = d;
    }

    private void LateUpdate()
    {
        Vector3 offset = Vector3.zero;
        if (time > 0f)
        {
            time -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / duration);
            offset = (Vector3)(Random.insideUnitCircle * strength * k);
        }
        transform.position += offset - lastOffset;
        lastOffset = offset;
    }
}
