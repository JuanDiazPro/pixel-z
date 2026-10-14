using UnityEngine;
using UnityEngine.Rendering.Universal;

// Parpadeo suave de una Light2D, para velas, candelabros y antorchas.
[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    public float amount = 0.18f;
    public float speed = 3f;

    private Light2D light2D;
    private float baseIntensity;
    private float baseRadius;
    private float seed;

    private void Awake()
    {
        light2D = GetComponent<Light2D>();
        baseIntensity = light2D.intensity;
        baseRadius = light2D.pointLightOuterRadius;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        float n2 = Mathf.PerlinNoise(seed + 7f, Time.time * speed * 2.7f) - 0.5f;
        float k = 1f + (n * 1.4f + n2 * 0.6f) * amount * 2f;
        light2D.intensity = baseIntensity * k;
        light2D.pointLightOuterRadius = baseRadius * (1f + (k - 1f) * 0.35f);
    }
}
