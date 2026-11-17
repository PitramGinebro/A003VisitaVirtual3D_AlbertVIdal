#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Menú: Tools > Backrooms > Aplicar iluminación Backrooms
/// Crea techo, paneles fluorescentes con luces, ajusta luz ambiental, niebla,
/// cámara y postprocesado para conseguir el look de los Backrooms.
/// Se puede ejecutar varias veces: borra lo generado antes y lo vuelve a crear.
/// </summary>
public static class BackroomsLightingSetup
{
    const string ROOT_NAME = "Backrooms_Lighting";
    const string MAT_FOLDER = "Assets/Materials/Backrooms";

    // Paleta Backrooms: fluorescente amarillento con un pelín de verde
    static readonly Color FluoColor   = new Color(1.00f, 0.95f, 0.70f);
    static readonly Color AmbientCol  = new Color(0.55f, 0.51f, 0.32f);
    static readonly Color FogCol      = new Color(0.62f, 0.57f, 0.34f);
    static readonly Color CeilingCol  = new Color(0.80f, 0.77f, 0.62f);

    const float CEILING_HEIGHT  = 3.0f; // metros desde el suelo
    const float LIGHT_SPACING   = 4.5f; // metros entre paneles
    const float LIGHT_RANGE     = 7f;
    const float LIGHT_INTENSITY = 3f;
    const float FLICKER_CHANCE  = 0.15f; // % de fluorescentes que parpadean

    [MenuItem("Tools/Backrooms/Aplicar iluminación Backrooms")]
    public static void Apply()
    {
        // 1. Limpiar ejecución anterior
        var old = GameObject.Find(ROOT_NAME);
        if (old != null) Object.DestroyImmediate(old);

        // 2. Calcular los límites del laberinto
        Bounds maze = GetMazeBounds();
        float floorY = FindFloorY(maze);
        // Techo bajo, como en los Backrooms (las paredes que sobresalgan quedan tapadas)
        float ceilingY = Mathf.Min(maze.max.y, floorY + CEILING_HEIGHT);
        Debug.Log($"[Backrooms] Laberinto: centro {maze.center}, tamaño {maze.size}, suelo y={floorY:F2}, techo y={ceilingY:F2}");

        // 2b. Colocar al jugador y al enemigo en el suelo, por debajo del techo
        PlaceOnFloor(Object.FindAnyObjectByType<PlayerController>(), floorY, 0.05f);
        PlaceOnFloor(Object.FindAnyObjectByType<EnemyController>(), floorY, 0f);

        // 3. Materiales
        EnsureFolder(MAT_FOLDER);
        Material ceilingMat = GetOrCreateMat("Backrooms_Ceiling", m =>
        {
            m.SetColor("_BaseColor", CeilingCol);
            m.SetFloat("_Smoothness", 0.05f);
        });
        Material panelMat = GetOrCreateMat("Backrooms_FluorescentPanel", m =>
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", FluoColor * 2.2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        });

        var root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "Backrooms lighting");

        // 4. Techo
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "Ceiling";
        ceiling.transform.SetParent(root.transform);
        ceiling.transform.position = new Vector3(maze.center.x, ceilingY + 0.1f, maze.center.z);
        ceiling.transform.localScale = new Vector3(maze.size.x + 2f, 0.2f, maze.size.z + 2f);
        ceiling.GetComponent<MeshRenderer>().sharedMaterial = ceilingMat;

        // 5. Paneles fluorescentes + luces puntuales en cuadrícula
        var lightsParent = new GameObject("FluorescentLights").transform;
        lightsParent.SetParent(root.transform);
        Physics.SyncTransforms();

        int count = 0;
        float midY = Mathf.Lerp(floorY, ceilingY, 0.6f);
        var rng = new System.Random(1234);
        for (float x = maze.min.x + LIGHT_SPACING * 0.5f; x < maze.max.x; x += LIGHT_SPACING)
        {
            for (float z = maze.min.z + LIGHT_SPACING * 0.5f; z < maze.max.z; z += LIGHT_SPACING)
            {
                // No poner luces dentro de las paredes
                if (Physics.CheckSphere(new Vector3(x, midY, z), 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                var fixture = new GameObject($"Fluorescent_{count:000}");
                fixture.transform.SetParent(lightsParent);
                fixture.transform.position = new Vector3(x, ceilingY - 0.02f, z);

                var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Panel";
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                panel.transform.SetParent(fixture.transform, false);
                panel.transform.localScale = new Vector3(1.2f, 0.04f, 0.6f);
                var pr = panel.GetComponent<MeshRenderer>();
                pr.sharedMaterial = panelMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(fixture.transform, false);
                lightGo.transform.localPosition = new Vector3(0, -0.15f, 0);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = FluoColor;
                l.range = LIGHT_RANGE;
                l.intensity = LIGHT_INTENSITY;
                l.shadows = LightShadows.None;

                if (rng.NextDouble() < FLICKER_CHANCE)
                    lightGo.AddComponent<FluorescentFlicker>();

                count++;
            }
        }
        Debug.Log($"[Backrooms] {count} fluorescentes creados.");

        // 6. Directional light: los Backrooms no tienen sol
        foreach (var l in Object.FindObjectsByType<Light>())
        {
            if (l.type == LightType.Directional)
            {
                Undo.RecordObject(l, "Backrooms lighting");
                l.intensity = 0f;
                l.shadows = LightShadows.None;
            }
        }

        // 7. Ambiente y niebla
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientCol;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogCol;
        RenderSettings.fogDensity = 0.045f;
        RenderSettings.reflectionIntensity = 0.3f;

        // 8. Cámaras: fondo del color de la niebla y postprocesado activo
        foreach (var cam in Object.FindObjectsByType<Camera>())
        {
            Undo.RecordObject(cam, "Backrooms lighting");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogCol;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                Undo.RecordObject(data, "Backrooms lighting");
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
            }
        }

        // 9. Postprocesado (Global Volume)
        SetupVolume();

        // 10. LightController: que apague también los paneles en los apagones
        var lc = Object.FindAnyObjectByType<LightController>();
        if (lc != null)
        {
            var so = new SerializedObject(lc);
            so.FindProperty("lightMaterial").objectReferenceValue = panelMat;
            var lights = so.FindProperty("sceneLights");
            if (lights != null) lights.arraySize = 0; // auto: todas las luces
            so.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[Backrooms] Iluminación aplicada y escena guardada.");
    }

    static void PlaceOnFloor(Component c, float floorY, float extra)
    {
        if (c == null) return;
        Transform root = c.transform.root;
        float bottom = c.transform.position.y;
        var col = c.GetComponent<Collider>();
        if (col != null) bottom = col.bounds.min.y;
        float delta = (floorY + extra) - bottom;
        if (Mathf.Abs(delta) < 0.01f) return;
        Undo.RecordObject(root, "Backrooms lighting");
        root.position += Vector3.up * delta;
        Debug.Log($"[Backrooms] {root.name} movido {delta:F2} m para quedar sobre el suelo.");
    }

    static float FindFloorY(Bounds maze)
    {
        Physics.SyncTransforms();
        // Rayo hacia abajo desde el jugador, ignorando su propio collider
        var player = Object.FindAnyObjectByType<PlayerController>();
        Vector3 origin = player != null ? player.transform.position : maze.center;
        origin.y = maze.max.y + 5f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerController>() || h.collider.GetComponentInParent<EnemyController>()) continue;
            if (h.collider.transform.root.name == ROOT_NAME) continue;
            // Nos quedamos con la superficie más baja que sea "suelo" (normal hacia arriba)
            if (h.normal.y > 0.7f && (best == float.MinValue || h.point.y < best)) best = h.point.y;
        }
        return best == float.MinValue ? maze.min.y : best;
    }

    static Bounds GetMazeBounds()
    {
        // Preferimos el modelo del laberinto ("Untitled"); si no, todo menos jugador/enemigo
        var maze = GameObject.Find("Untitled");
        Renderer[] rends = maze != null
            ? maze.GetComponentsInChildren<Renderer>()
            : Object.FindObjectsByType<Renderer>();

        bool has = false;
        Bounds b = new Bounds();
        foreach (var r in rends)
        {
            if (r.GetComponentInParent<PlayerController>() || r.GetComponentInParent<EnemyController>()) continue;
            if (r.transform.root.name == ROOT_NAME) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (!has) b = new Bounds(Vector3.zero, new Vector3(40, 3, 40));
        return b;
    }

    static void SetupVolume()
    {
        var volume = Object.FindAnyObjectByType<Volume>();
        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        var profile = volume.sharedProfile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, MAT_FOLDER + "/Backrooms_Volume.asset");
            volume.sharedProfile = profile;
        }

        var tone = GetOrAdd<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);

        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(FluoColor);

        var ca = GetOrAdd<ColorAdjustments>(profile);
        ca.postExposure.Override(0.5f);
        ca.contrast.Override(-12f);
        ca.saturation.Override(-8f);
        ca.colorFilter.Override(new Color(1f, 0.96f, 0.82f));

        var wb = GetOrAdd<WhiteBalance>(profile);
        wb.temperature.Override(18f);
        wb.tint.Override(-8f); // ligeramente verdoso, como los fluorescentes viejos

        var vig = GetOrAdd<Vignette>(profile);
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.5f);
        vig.color.Override(Color.black);

        var grain = GetOrAdd<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.35f);
        grain.response.Override(0.6f);

        var chroma = GetOrAdd<ChromaticAberration>(profile);
        chroma.intensity.Override(0.12f);

        EditorUtility.SetDirty(profile);
    }

    static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T comp))
        {
            comp = profile.Add<T>(true);
            if (AssetDatabase.Contains(profile))
                AssetDatabase.AddObjectToAsset(comp, profile);
        }
        comp.active = true;
        EditorUtility.SetDirty(comp);
        return comp;
    }

    static Material GetOrCreateMat(string name, System.Action<Material> setup)
    {
        string path = $"{MAT_FOLDER}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
#endif
