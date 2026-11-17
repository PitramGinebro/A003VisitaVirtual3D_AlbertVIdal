using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Controla las luces de la escena. Cada cierto tiempo (aleatorio) las luces se apagan
/// durante unos segundos y luego vuelven a encenderse. Mientras están apagadas,
/// el EnemyController persigue al jugador.
/// </summary>
public class LightController : MonoBehaviour
{
    public static LightController Instance;

    /// <summary>Se lanza cada vez que las luces cambian (true = encendidas).</summary>
    public event Action<bool> OnLightsChanged;

    [Header("Referencias")]
    [Tooltip("Material emisivo de las lámparas (opcional).")]
    [SerializeField] private Material lightMaterial;
    [Tooltip("Perfil del Global Volume (opcional, no se usa todavía).")]
    [SerializeField] private VolumeProfile volumeProfile;
    [Tooltip("Luces que se apagan. Si lo dejas vacío, se usan todas las luces de la escena.")]
    [SerializeField] private Light[] sceneLights;

    [Header("Apagones aleatorios")]
    [SerializeField] private bool randomBlackouts = true;
    [Tooltip("Segundos mínimos / máximos con la luz encendida antes de un apagón.")]
    [SerializeField] private Vector2 timeLightsOn = new Vector2(8f, 20f);
    [Tooltip("Segundos mínimos / máximos que dura el apagón.")]
    [SerializeField] private Vector2 timeLightsOff = new Vector2(4f, 8f);
    [Tooltip("Intensidad de luz ambiental durante el apagón (0 = negro total).")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientDuringBlackout = 0.05f;

    [Header("Debug")]
    [Tooltip("Permite apagar/encender las luces con la tecla F.")]
    [SerializeField] private bool allowManualToggle = true;

    public bool IsLightOn { get; private set; } = true;

    private Color initialEmissionColor;
    private float[] initialIntensities;
    private float initialAmbientIntensity;
    private Color initialAmbientColor;
    private Color initialFogColor;
    private float timer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (lightMaterial != null)
            initialEmissionColor = lightMaterial.GetColor("_EmissionColor");

        if (sceneLights == null || sceneLights.Length == 0)
            sceneLights = FindObjectsByType<Light>();

        initialIntensities = new float[sceneLights.Length];
        for (int i = 0; i < sceneLights.Length; i++)
            initialIntensities[i] = sceneLights[i].intensity;

        initialAmbientIntensity = RenderSettings.ambientIntensity;
        initialAmbientColor = RenderSettings.ambientLight;
        initialFogColor = RenderSettings.fogColor;

        ScheduleNext();
    }

    private void Update()
    {
        if (allowManualToggle && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            SetLights(!IsLightOn);
            ScheduleNext();
        }

        if (!randomBlackouts) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SetLights(!IsLightOn);
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        Vector2 range = IsLightOn ? timeLightsOn : timeLightsOff;
        timer = UnityEngine.Random.Range(range.x, range.y);
    }

    /// <summary>Enciende (true) o apaga (false) todas las luces.</summary>
    public void SetLights(bool on)
    {
        if (IsLightOn == on) return;
        IsLightOn = on;

        for (int i = 0; i < sceneLights.Length; i++)
        {
            if (sceneLights[i] == null) continue;
            sceneLights[i].intensity = on ? initialIntensities[i] : 0f;
        }

        // Sirve tanto para ambiente "Skybox" (intensidad) como "Flat/Color" (color)
        RenderSettings.ambientIntensity = on ? initialAmbientIntensity : ambientDuringBlackout;
        RenderSettings.ambientLight = on ? initialAmbientColor : initialAmbientColor * ambientDuringBlackout;
        RenderSettings.fogColor = on ? initialFogColor : initialFogColor * 0.08f;
        if (Camera.main != null && Camera.main.clearFlags == CameraClearFlags.SolidColor)
            Camera.main.backgroundColor = RenderSettings.fogColor;

        if (lightMaterial != null)
        {
            if (on)
            {
                lightMaterial.SetColor("_EmissionColor", initialEmissionColor);
                lightMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                lightMaterial.SetColor("_EmissionColor", Color.black);
                lightMaterial.DisableKeyword("_EMISSION");
            }
        }

        OnLightsChanged?.Invoke(on);
    }

    private void OnDestroy()
    {
        RestoreMaterial();
    }

    private void OnApplicationQuit()
    {
        RestoreMaterial();
    }

    // El material es un asset: si no lo restauramos, se queda apagado en el proyecto.
    private void RestoreMaterial()
    {
        if (lightMaterial != null)
        {
            lightMaterial.SetColor("_EmissionColor", initialEmissionColor);
            lightMaterial.EnableKeyword("_EMISSION");
        }
    }
}
