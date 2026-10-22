using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    /// <summary> Diagnostic: prints a scene hierarchy branch with components and rect info. Args: -probeScene path -probeRoot name </summary>
    public static class SceneProbe
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            string scene = Arg(args, "-probeScene");
            string[] roots = (Arg(args, "-probeRoot") ?? "").Split('|');
            int depth = int.TryParse(Arg(args, "-probeDepth"), out int d) ? d : 3;
            EditorSceneManager.OpenScene(scene);
            var sb = new StringBuilder();
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && (roots.Contains(g.name) || roots.Contains("*") && !g.transform.parent)))
                Dump(go.transform, 0, depth, sb);
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        /// <summary> Prints the serialized JSON of every component of the given types. Args: -probeScene path -probeTypes A|B </summary>
        public static void Components()
        {
            var args = System.Environment.GetCommandLineArgs();
            EditorSceneManager.OpenScene(Arg(args, "-probeScene"));
            var types = (Arg(args, "-probeTypes") ?? "").Split('|');
            var sb = new StringBuilder();
            foreach (var c in Resources.FindObjectsOfTypeAll<Component>().Where(c => c && c.gameObject.scene.IsValid() && types.Contains(c.GetType().Name)))
            {
                sb.AppendLine($"== {c.GetType().Name} on {c.gameObject.name}");
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                bool enter = true;
                while (it.Next(enter))
                {
                    enter = it.propertyType == SerializedPropertyType.Generic && it.depth < 6 && !it.isArray || (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arraySize < 12);
                    string val = it.propertyType switch
                    {
                        SerializedPropertyType.ObjectReference => it.objectReferenceValue ? it.objectReferenceValue.name : "null",
                        SerializedPropertyType.String => $"\"{it.stringValue}\"",
                        SerializedPropertyType.Integer => it.intValue.ToString(),
                        SerializedPropertyType.Boolean => it.boolValue.ToString(),
                        SerializedPropertyType.Float => it.floatValue.ToString("0.###"),
                        SerializedPropertyType.Enum => it.enumValueIndex >= 0 && it.enumValueIndex < it.enumDisplayNames.Length ? it.enumDisplayNames[it.enumValueIndex] : it.intValue.ToString(),
                        _ => it.isArray ? $"[{it.arraySize}]" : ""
                    };
                    if (it.name == "m_Script" || it.name.StartsWith("m_Object") || it.name == "m_EditorHideFlags") continue;
                    sb.AppendLine($"{new string(' ', it.depth * 2)}{it.name} = {val}");
                }
            }
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        /// <summary> Lists direct children of prefabs (name, source prefab, position, gate texts). Args: -probePrefabs path|path </summary>
        public static void PrefabLayout()
        {
            var args = System.Environment.GetCommandLineArgs();
            var sb = new StringBuilder();
            foreach (var path in (Arg(args, "-probePrefabs") ?? "").Split('|'))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset) { sb.AppendLine($"missing {path}"); continue; }
                var child = Arg(args, "-probeChild");
                var root = string.IsNullOrEmpty(child) ? asset : asset.transform.Find(child)?.gameObject;
                if (!root) { sb.AppendLine($"no child {child} in {path}"); continue; }
                sb.AppendLine($"== {path}{(string.IsNullOrEmpty(child) ? "" : "/" + child)} ({root.transform.childCount} children)");
                foreach (Transform c in root.transform)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                    string srcPath = src ? System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(src)) : "-";
                    string gate = "";
                    foreach (var comp in c.GetComponentsInChildren<Component>(true))
                    {
                        if (!comp || comp.GetType().Name != "MOST_Gate") continue;
                        var so = new SerializedObject(comp);
                        gate += $" [{so.FindProperty("Type")?.enumNames[so.FindProperty("Type").enumValueIndex]} {so.FindProperty("GateText")?.stringValue} -> {so.FindProperty("OutputText")?.stringValue}]";
                    }
                    var cols = c.GetComponentsInChildren<Collider>(true);
                    string bounds = "";
                    if (cols.Length > 0)
                    {
                        var b = cols[0].bounds;
                        foreach (var col in cols) b.Encapsulate(col.bounds);
                        bounds = $" bounds x[{b.min.x:0.##},{b.max.x:0.##}] z[{b.min.z:0.##},{b.max.z:0.##}] layer={c.gameObject.layer}";
                    }
                    sb.AppendLine($"{c.name} | {srcPath} | {c.position.x:0.##},{c.position.y:0.##},{c.position.z:0.##}{gate}{bounds}");
                }
            }
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        /// <summary> Deep dump of prefabs: hierarchy with tag/layer/colliders and selected fields of MOST components. Args: -probePrefabs a|b </summary>
        public static void PrefabDeep()
        {
            var args = System.Environment.GetCommandLineArgs();
            string[] fields = { "Type", "GateOperation", "GateValue", "GateText", "OutputText", "EnableFixedOutput", "OutputOperation", "OutputValue",
                "EnableUpgrade", "IncreaseAmount", "DestroyColliderOnHit", "TriggerTags", "MaxHealth", "DefaultDamageAmount", "DamageTags", "DefeatType",
                "UseOnTrigger", "UseOnCollision", "Damage", "DamageAmount", "Amount", "CurrencyName", "AmountRescale" };
            var sb = new StringBuilder();
            foreach (var path in (Arg(args, "-probePrefabs") ?? "").Split('|'))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!root) { sb.AppendLine($"missing {path}"); continue; }
                sb.AppendLine($"== {path}");
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int depth = 0; for (var p = t; p != root.transform; p = p.parent) depth++;
                    var cols = string.Join(",", t.GetComponents<Collider>().Select(c => $"{c.GetType().Name}{(c.isTrigger ? "(T)" : "")}"));
                    var comps = string.Join(",", t.GetComponents<Component>().Where(c => c is MonoBehaviour).Select(c => c.GetType().Name));
                    sb.AppendLine($"{new string(' ', depth * 2)}{t.name} tag={t.tag} layer={LayerMask.LayerToName(t.gameObject.layer)} active={t.gameObject.activeSelf} [{cols}] {{{comps}}} lp={t.localPosition} ls={t.localScale}");
                    foreach (var mb in t.GetComponents<MonoBehaviour>())
                    {
                        if (!mb || !(mb.GetType().Name.StartsWith("MOST_") || mb.GetType().Name.Contains("Damage"))) continue;
                        var so = new SerializedObject(mb);
                        foreach (var f in fields)
                        {
                            var prop = so.FindProperty(f);
                            if (prop == null) continue;
                            string v = prop.propertyType switch
                            {
                                SerializedPropertyType.Enum => prop.enumDisplayNames[Mathf.Clamp(prop.enumValueIndex, 0, prop.enumDisplayNames.Length - 1)],
                                SerializedPropertyType.Float => prop.floatValue.ToString("0.###"),
                                SerializedPropertyType.Integer => prop.intValue.ToString(),
                                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                                SerializedPropertyType.String => $"\"{prop.stringValue}\"",
                                _ => prop.isArray ? $"[{prop.arraySize}]" : prop.propertyType.ToString()
                            };
                            sb.AppendLine($"{new string(' ', depth * 2 + 4)}{mb.GetType().Name}.{f} = {v}");
                        }
                    }
                }
            }
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        /// <summary> Lists root objects of a scene and their direct children with source prefab and position. Args: -probeScene path </summary>
        public static void SceneLayout()
        {
            var args = System.Environment.GetCommandLineArgs();
            var scene = EditorSceneManager.OpenScene(Arg(args, "-probeScene"));
            var sb = new StringBuilder();
            sb.AppendLine($"lighting: skybox={RenderSettings.skybox?.name} ambient={RenderSettings.ambientMode}");
            foreach (var root in scene.GetRootGameObjects())
            {
                var comps = string.Join(",", root.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name));
                sb.AppendLine($"ROOT {root.name} {{{comps}}} pos={root.transform.position} children={root.transform.childCount}");
                foreach (Transform c in root.transform)
                {
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                    string gate = "";
                    foreach (var comp in c.GetComponentsInChildren<Component>(true))
                    {
                        if (!comp) continue;
                        var tn = comp.GetType().Name;
                        if (tn == "MOST_Gate") { var so = new SerializedObject(comp); gate += $" [{so.FindProperty("GateText")?.stringValue}]"; }
                        if (tn == "EnemyZoneControl") { var so = new SerializedObject(comp); gate += $" [zone {so.FindProperty("NumberOfChildren")?.FindPropertyRelative("min")?.intValue}-{so.FindProperty("NumberOfChildren")?.FindPropertyRelative("max")?.intValue}]"; }
                    }
                    sb.AppendLine($"   {c.name} | {(src ? src.name : "-")} | {c.position.x:0.##},{c.position.y:0.##},{c.position.z:0.##}{gate}");
                }
            }
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        /// <summary> Rendering cost summary of a scene. Args: -probeScene path </summary>
        public static void RenderStats()
        {
            var args = System.Environment.GetCommandLineArgs();
            var scene = EditorSceneManager.OpenScene(Arg(args, "-probeScene"));
            var renderers = Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene == scene && r.gameObject.activeInHierarchy && r.enabled).ToList();
            long tris = 0; int casters = 0, staticBatch = 0, skinned = 0, particles = 0;
            var materials = new System.Collections.Generic.HashSet<Material>();
            var perRoot = new System.Collections.Generic.Dictionary<string, (int count, long tris)>();
            foreach (var r in renderers)
            {
                long rt = 0;
                if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) rt = mf.sharedMesh.triangles.LongLength / 3;
                else if (r is SkinnedMeshRenderer smr && smr.sharedMesh) { rt = smr.sharedMesh.triangles.LongLength / 3; skinned++; }
                else if (r is ParticleSystemRenderer) particles++;
                tris += rt;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) casters++;
                if (GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject, StaticEditorFlags.BatchingStatic)) staticBatch++;
                foreach (var m in r.sharedMaterials) if (m) materials.Add(m);
                var root = r.transform.root.name;
                perRoot.TryGetValue(root, out var v);
                perRoot[root] = (v.count + 1, v.tris + rt);
            }
            var lights = Resources.FindObjectsOfTypeAll<Light>().Where(l => l.gameObject.scene == scene && l.isActiveAndEnabled).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"renderers={renderers.Count} tris={tris} materials={materials.Count} shadowCasters={casters} staticBatching={staticBatch} skinned={skinned} particles={particles}");
            sb.AppendLine($"lights={lights.Count} ({string.Join(", ", lights.Select(l => $"{l.name}:{l.type}/{l.shadows}/{l.lightmapBakeType}"))})");
            sb.AppendLine("heaviest roots:");
            foreach (var kv in perRoot.OrderByDescending(k => k.Value.tris).Take(12))
                sb.AppendLine($"  {kv.Key}: renderers={kv.Value.count} tris={kv.Value.tris}");
            var cam = Camera.main ? Camera.main : Resources.FindObjectsOfTypeAll<Camera>().FirstOrDefault(c => c.gameObject.scene == scene);
            if (cam) sb.AppendLine($"camera {cam.name}: near={cam.nearClipPlane} far={cam.farClipPlane} pos={cam.transform.position} fov={cam.fieldOfView}");
            sb.AppendLine($"fog={RenderSettings.fog} mode={RenderSettings.fogMode} start={RenderSettings.fogStartDistance} end={RenderSettings.fogEndDistance} density={RenderSettings.fogDensity}");
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipeline) sb.AppendLine($"URP {pipeline.name}: shadowDistance={pipeline.shadowDistance} cascades={pipeline.shadowCascadeCount} srpBatcher={pipeline.useSRPBatcher} msaa={pipeline.msaaSampleCount} renderScale={pipeline.renderScale} hdr={pipeline.supportsHDR}");
            var objects = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Objects");
            if (objects)
            {
                var scripted = objects.GetComponentsInChildren<MonoBehaviour>(true).Select(m => m.GetType().Name).GroupBy(n => n).Select(g => $"{g.Key}x{g.Count()}");
                sb.AppendLine($"Objects scripts: {string.Join(", ", scripted)}");
                sb.AppendLine($"Objects rigidbodies={objects.GetComponentsInChildren<Rigidbody>(true).Length} animators={objects.GetComponentsInChildren<Animator>(true).Length} colliders={objects.GetComponentsInChildren<Collider>(true).Length}");
                var bounds = objects.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                sb.AppendLine($"Objects bounds={bounds.min}..{bounds.max}");
                sb.AppendLine("Objects children: " + string.Join(", ", objects.transform.Cast<Transform>().Select(t => $"{t.name}({t.GetComponentsInChildren<Renderer>().Length})")));
            }
            Debug.Log("[AMO] PROBE\n" + sb);
        }

        static void Dump(Transform t, int indent, int maxDepth, StringBuilder sb)
        {
            var comps = string.Join(",", t.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType().Name));
            string rect = t is RectTransform rt ? $" pos={rt.anchoredPosition} size={rt.sizeDelta} anchor={rt.anchorMin}-{rt.anchorMax}" : "";
            var text = t.GetComponent<UnityEngine.UI.Text>()?.text ?? t.GetComponent<TMPro.TMP_Text>()?.text;
            string where = t is RectTransform ? "" : $" p={t.position} tag={t.tag} layer={t.gameObject.layer}";
            sb.AppendLine($"{new string(' ', indent * 2)}{t.name} [{(t.gameObject.activeSelf ? "on" : "off")}] {{{comps}}}{rect}{where}{(text != null ? $" text=\"{text}\"" : "")}");
            if (indent >= maxDepth) return;
            foreach (Transform c in t) Dump(c, indent + 1, maxDepth, sb);
        }

        static string Arg(string[] args, string name)
        {
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
