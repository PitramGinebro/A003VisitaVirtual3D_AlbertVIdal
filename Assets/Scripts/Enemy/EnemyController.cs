using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// El enemigo solo aparece y persigue al jugador cuando las luces están apagadas.
/// Cuando vuelve la luz, se esconde y se queda quieto.
/// Si toca al jugador, este muere y la escena se reinicia.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Tooltip("Velocidad de persecución.")]
    [SerializeField] private float chaseSpeed = 4f;
    [Tooltip("Si es true, vuelve a su posición inicial cada vez que se enciende la luz.")]
    [SerializeField] private bool resetPositionOnLight = false;
    [Tooltip("Distancia (en horizontal) a la que el enemigo toca al jugador.")]
    [SerializeField] private float catchDistance = 1.2f;

    [Tooltip("Hace que el enemigo se vea aunque todo esté a oscuras (material sin iluminación).")]
    [SerializeField] private bool visibleInDark = true;

    [Header("Muerte")]
    [Tooltip("Segundos que se muestra la pantalla de muerte antes de reiniciar.")]
    [SerializeField] private float restartDelay = 2f;
    [SerializeField] private string deathMessage = "HAS MUERTO";

    private NavMeshAgent body;
    private Renderer[] visuals;
    private AudioSource audioSource;
    private Vector3 startPosition;
    private bool isChasing;

    private bool playerDead;
    private float deathFade;
    private Texture2D blackTexture;

    private void Start()
    {
        body = GetComponent<NavMeshAgent>();
        body.speed = chaseSpeed;
        visuals = GetComponentsInChildren<Renderer>();
        LiftVisualsAbovePivot();
        if (visibleInDark) MakeVisualsUnlit();
        audioSource = GetComponent<AudioSource>();
        startPosition = transform.position;

        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.white);
        blackTexture.Apply();

        if (LightController.Instance != null)
        {
            LightController.Instance.OnLightsChanged += HandleLightsChanged;
            HandleLightsChanged(LightController.Instance.IsLightOn);
        }
        else
        {
            Debug.LogWarning("EnemyController: no hay LightController en la escena.");
            HandleLightsChanged(true);
        }
    }

    // El NavMeshAgent pega el pivote al suelo: subimos los visuales (hijos)
    // para que su parte de abajo quede a ras de suelo y no medio enterrados.
    private void LiftVisualsAbovePivot()
    {
        float minY = float.MaxValue;
        foreach (Renderer r in visuals) minY = Mathf.Min(minY, r.bounds.min.y);
        if (minY == float.MaxValue) return;

        float lift = transform.position.y - minY;
        if (lift <= 0.01f) return;
        foreach (Renderer r in visuals)
            if (r.transform != transform)
                r.transform.position += Vector3.up * lift;
    }

    // Copia la textura y el color a un material Unlit: así no depende de las luces
    private void MakeVisualsUnlit()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) return;

        foreach (Renderer r in visuals)
        {
            Material[] mats = r.materials; // instancias, no toca los assets
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                Material m = new Material(unlit);
                if (src.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                else if (src.HasProperty("_MainTex")) m.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", c * 0.8f);
                if (src.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // visible por las dos caras
                mats[i] = m;
            }
            r.materials = mats;
        }
    }

    private void OnDestroy()
    {
        if (LightController.Instance != null)
            LightController.Instance.OnLightsChanged -= HandleLightsChanged;
    }

    private void HandleLightsChanged(bool lightsOn)
    {
        if (playerDead) return;

        isChasing = !lightsOn;

        foreach (Renderer r in visuals)
            r.enabled = isChasing;

        if (body.isOnNavMesh)
        {
            body.isStopped = !isChasing;
            if (!isChasing)
            {
                body.ResetPath();
                if (resetPositionOnLight) body.Warp(startPosition);
            }
        }

        if (audioSource != null)
        {
            if (isChasing && !audioSource.isPlaying) audioSource.Play();
            else if (!isChasing) audioSource.Stop();
        }
    }

    private void Update()
    {
        if (playerDead || !isChasing || PlayerController.Instance == null || !body.isOnNavMesh) return;

        Vector3 target = PlayerController.Instance.transform.position;
        body.SetDestination(target);

        // Mira siempre hacia el jugador (solo en horizontal)
        Vector3 look = new Vector3(target.x, transform.position.y, target.z);
        if ((look - transform.position).sqrMagnitude > 0.01f)
            transform.LookAt(look);

        // Distancia en horizontal, para que la diferencia de altura no impida el contacto
        Vector3 flatDiff = target - transform.position;
        flatDiff.y = 0f;
        if (flatDiff.magnitude <= catchDistance)
            KillPlayer();
    }

    // Por si el enemigo (o su hijo) tiene un collider marcado como Trigger
    private void OnTriggerEnter(Collider other)
    {
        if (isChasing && PlayerController.Instance != null &&
            other.GetComponentInParent<PlayerController>() == PlayerController.Instance)
            KillPlayer();
    }

    private void KillPlayer()
    {
        if (playerDead) return;
        playerDead = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        Debug.Log("¡El enemigo te ha atrapado! Reiniciando...");

        if (body.isOnNavMesh) body.isStopped = true;

        // Congelamos el juego (jugador incluido) mientras se muestra la pantalla de muerte
        Time.timeScale = 0f;
        AudioListener.pause = true;

        float t = 0f;
        while (t < restartDelay)
        {
            t += Time.unscaledDeltaTime;
            deathFade = Mathf.Clamp01(t / (restartDelay * 0.5f));
            yield return null;
        }

        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        if (!playerDead) return;

        // Fundido a rojo oscuro
        GUI.color = new Color(0.35f, 0f, 0f, deathFade * 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTexture);

        GUI.color = new Color(1f, 1f, 1f, deathFade);
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.1f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), deathMessage, style);
    }
}
