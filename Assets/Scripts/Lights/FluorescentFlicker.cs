using UnityEngine;

/// <summary>
/// Parpadeo de fluorescente viejo: casi siempre estable, con tirones cortos de vez en cuando.
/// Respeta los apagones del LightController (no hace nada con la luz apagada).
/// </summary>
[RequireComponent(typeof(Light))]
public class FluorescentFlicker : MonoBehaviour
{
    [SerializeField] private Vector2 timeBetweenFlickers = new Vector2(2f, 9f);
    [SerializeField] private Vector2 flickerDuration = new Vector2(0.08f, 0.6f);
    [Range(0f, 1f)]
    [SerializeField] private float minIntensityFactor = 0.1f;

    private Light lightComp;
    private float baseIntensity;
    private float nextFlicker;
    private float flickerEnd;

    private void Awake()
    {
        lightComp = GetComponent<Light>();
        baseIntensity = lightComp.intensity;
        nextFlicker = Time.time + Random.Range(timeBetweenFlickers.x, timeBetweenFlickers.y);
    }

    private void Update()
    {
        if (LightController.Instance != null && !LightController.Instance.IsLightOn) return;

        if (Time.time >= nextFlicker && Time.time > flickerEnd)
        {
            flickerEnd = Time.time + Random.Range(flickerDuration.x, flickerDuration.y);
            nextFlicker = flickerEnd + Random.Range(timeBetweenFlickers.x, timeBetweenFlickers.y);
        }

        if (Time.time < flickerEnd)
            lightComp.intensity = baseIntensity * (Random.value < 0.5f ? minIntensityFactor : Random.Range(0.6f, 1.1f));
        else
            lightComp.intensity = baseIntensity;
    }
}
