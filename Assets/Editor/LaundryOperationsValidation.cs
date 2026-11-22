using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class LaundryOperationsValidation
{
    public static string Run()
    {
        var problems = new List<string>(); var wf = Object.FindAnyObjectByType<LaundryWorkflow>();
        if (!wf) return "Workflow missing";
        var so = new SerializedObject(wf); var p = so.GetIterator();
        while (p.NextVisible(true)) if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && !p.propertyPath.EndsWith(".drum") && !p.propertyPath.EndsWith(".contents") && !p.propertyPath.EndsWith(".display")) problems.Add("Missing reference: " + p.propertyPath);
        Physics.SyncTransforms(); var player = Object.FindAnyObjectByType<Player>();
        var routes = new[] {
            new[]{new Vector3(6.9f,.28f,27),new Vector3(6.9f,.28f,14.5f),new Vector3(8.75f,.28f,12.7f)},
            new[]{new Vector3(6.9f,.28f,27),new Vector3(5.15f,.28f,28.4f)},
            new[]{new Vector3(6.9f,.28f,27),new Vector3(7.5f,.28f,28.4f)},
            new[]{new Vector3(6.9f,.28f,27),new Vector3(9.85f,.28f,28.4f)},
            new[]{new Vector3(6.9f,.28f,15),new Vector3(12.1f,.28f,15),new Vector3(12.4f,.28f,12)},
            new[]{new Vector3(12.1f,.28f,15),new Vector3(12.1f,.28f,13.6f)},
            new[]{new Vector3(6.9f,.28f,14.5f),new Vector3(6.65f,.28f,14.4f)},
            new[]{new Vector3(6.45f,.28f,12.7f),new Vector3(6.45f,.28f,3),new Vector3(-11.5f,.29f,3),new Vector3(-11.5f,.29f,8.6f),new Vector3(-9.8f,.29f,8.6f)},
            new[]{new Vector3(6.9f,.28f,17.72f),new Vector3(4.8f,.28f,17.72f)},
            new[]{new Vector3(6.9f,.28f,22.57f),new Vector3(4.8f,.28f,22.57f)},
            new[]{new Vector3(6.9f,.28f,26.45f),new Vector3(4.8f,.28f,26.45f)}
        };
        int samples = 0;
        for (int r = 0; r < routes.Length; r++) for (int segment = 1; segment < routes[r].Length; segment++)
        {
            Vector3 a = routes[r][segment - 1], b = routes[r][segment]; int count = Mathf.CeilToInt(Vector3.Distance(a, b) / .25f);
            for (int n = 0; n <= count; n++)
            {
                var pos = Vector3.Lerp(a, b, (float)n / Mathf.Max(1, count)); samples++;
                foreach (var c in Physics.OverlapCapsule(pos + Vector3.up * .99f, pos + Vector3.up * 1.57f, .9124f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (c.transform.IsChildOf(player.transform) || c.bounds.max.y < pos.y + .1f) continue;
                    string issue = "Route " + r + " intersects " + c.name + " near " + pos;
                    if (!problems.Any(x => x.StartsWith("Route " + r + " intersects " + c.name))) problems.Add(issue);
                }
            }
        }
        var manager = Object.FindAnyObjectByType<GameManager>(); var routeData = Object.FindAnyObjectByType<Routes>();
        var paths = new[]{routeData.Route1,routeData.Route2,routeData.Route3,routeData.Route4}; int navPaths = 0;
        foreach (var route in paths)
        {
            var end = route.LastOrDefault(t => t); if (!end) continue;
            UnityEngine.AI.NavMeshHit start; if (!UnityEngine.AI.NavMesh.SamplePosition(end.position,out start,3, -1)) { problems.Add("NPC route has no NavMesh near end"); continue; }
            foreach (var point in manager.linePoint)
            {
                var path = new UnityEngine.AI.NavMeshPath();
                if (UnityEngine.AI.NavMesh.CalculatePath(start.position,point.position,-1,path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) navPaths++;
                else problems.Add("NPC approach path incomplete");
            }
        }
        var computer = Object.FindAnyObjectByType<ShopComputer>(); var cs = new SerializedObject(computer);
        var window = (RectTransform)cs.FindProperty("window").objectReferenceValue; int buttons = 0;
        foreach (var b in window.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            buttons++; if (b.onClick.GetPersistentEventCount() == 0) problems.Add("Unwired button: " + b.name);
            for (int i=0;i<b.onClick.GetPersistentEventCount();i++) if (!b.onClick.GetPersistentTarget(i)) problems.Add("Missing button target: " + b.name);
        }
        string report = "Serialized references checked; " + samples + " capsule samples across " + routes.Length + " routes; " + navPaths + "/12 NPC paths; " + buttons + " wired computer buttons.\n" + (problems.Count == 0 ? "PASS" : string.Join("\n",problems));
        Directory.CreateDirectory("QAOutput/LaundryOperations"); File.WriteAllText("QAOutput/LaundryOperations/scene-validation.txt",report); return report;
    }
}
