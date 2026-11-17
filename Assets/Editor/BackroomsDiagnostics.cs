#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class BackroomsDiagnostics
{
    [MenuItem("Tools/Backrooms/Diagnostico alturas")]
    public static void Run()
    {
        Physics.SyncTransforms();
        var player = Object.FindAnyObjectByType<PlayerController>();
        var enemy = Object.FindAnyObjectByType<EnemyController>();
        var sb = new System.Text.StringBuilder("[Diag]\n");
        if (player)
        {
            var p = player.transform.position;
            sb.AppendLine($"Player pos {p} col={player.GetComponent<Collider>()?.bounds}");
            var hits = Physics.RaycastAll(p + Vector3.up * 10f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
                sb.AppendLine($"  hit {h.collider.name} (root {h.collider.transform.root.name}) y={h.point.y:F2} n={h.normal} trigger={h.collider.isTrigger}");
            if (NavMesh.SamplePosition(p, out var nh, 20f, NavMesh.AllAreas))
                sb.AppendLine($"  NavMesh nearest to player: {nh.position}");
        }
        if (LightController.Instance != null)
            sb.AppendLine($"Lights on: {LightController.Instance.IsLightOn}");
        var cam = Camera.main;
        if (cam && enemy)
        {
            Vector3 vp = cam.WorldToViewportPoint(enemy.transform.position + Vector3.up * 1.3f);
            bool blocked = Physics.Linecast(cam.transform.position, enemy.transform.position + Vector3.up * 1.3f, out var lh) && lh.collider.GetComponentInParent<EnemyController>() == null;
            sb.AppendLine($"Camera {cam.transform.position} fwd {cam.transform.forward}; enemy viewport {vp}; blocked={blocked}");
        }
        if (enemy)
        {
            sb.AppendLine($"Enemy pos {enemy.transform.position}");
            foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"  renderer {r.name} enabled={r.enabled} active={r.gameObject.activeInHierarchy} bounds={r.bounds} mat={r.sharedMaterial?.name} shader={r.sharedMaterial?.shader.name}");
            var a = enemy.GetComponent<NavMeshAgent>();
            sb.AppendLine($"  agent baseOffset={a.baseOffset} height={a.height}");
        }
        foreach (var c in Object.FindObjectsByType<Collider>())
            if (c.transform.root.name == "Untitled" || c.name == "Plane" || c.name == "Ceiling")
                sb.AppendLine($"Collider {c.name} {c.GetType().Name} bounds min={c.bounds.min} max={c.bounds.max}");
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("Assets/../backrooms_diag.txt", sb.ToString());
    }
}
#endif
