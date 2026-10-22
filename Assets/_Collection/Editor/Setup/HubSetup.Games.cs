using System.Linq;
using AllMightyOne.Hub;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    public static partial class HubSetup
    {
        // fraction of the canvas height below the top: lower where the kit's top row is taller
        static readonly System.Collections.Generic.Dictionary<string, float> HomeButtonDrop = new()
        {
            ["Ninja"] = 0.24f, ["Cubey"] = 0.25f, ["Splat"] = 0.25f,
        };

        /// <summary>
        /// Every game gets a way back to the hub: a round home button on its menu (top-left, sized to the scene's
        /// canvas) and the Android back key while the menu is shown. Block Puzzle's "main menu" button is re-pointed.
        /// </summary>
        [MenuItem("All Mighty One/Hub/Add Home Buttons To Games")]
        public static void AddHomeButtons()
        {
            PrepareText();
            foreach (var path in ScenePaths.Values)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var old = Object.FindObjectsByType<HubReturn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in old)
                {
                    if (r.HomeButton && r.HomeButton.name == "Hub Home Button") Object.DestroyImmediate(r.HomeButton.gameObject);
                    Object.DestroyImmediate(r.gameObject);
                }

                var holder = new GameObject("Hub Return").AddComponent<HubReturn>();
                // Block Adventure's arrow opened the old Block Puzzle menu scene (Classic's sits on a hidden panel)
                var menuButton = scene.name == "BlockBlast Adventure" ? Find("Main Menu Button") : null;
                if (menuButton && menuButton.GetComponent<Button>())
                {
                    var b = menuButton.GetComponent<Button>();
                    for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(b.onClick, i);
                    holder.HomeButton = b;
                    Debug.Log($"[AMO] {scene.name}: home -> existing Main Menu Button");
                }
                else
                {
                    var menu = Find("Canvas/Menu UI") ?? Find("Menu UI");
                    var canvas = menu ? menu.GetComponentInParent<Canvas>(true).rootCanvas : Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.renderMode != RenderMode.WorldSpace);
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    var refSize = scaler ? scaler.referenceResolution : new Vector2(1080, 1920);
                    float size = refSize.x * 0.11f;
                    var btn = Button3D("Hub Home Button", canvas.transform, new Vector2(size, size), Violet, Ui("circle"), size * 0.09f);
                    var rt = (RectTransform)btn.transform;
                    float down = HomeButtonDrop.TryGetValue(scene.name, out float d) ? d : 0.16f;
                    Place(rt, new Vector2(0, 1), new Vector2(refSize.x * 0.1f, -refSize.y * down), new Vector2(size, size));
                    Icon(btn, Ui("ic_home"), 0.56f);
                    holder.HomeButton = btn;
                    holder.MenuPanel = menu;
                    Debug.Log($"[AMO] {scene.name}: home button on '{canvas.name}' (ref {refSize}), menu={(menu ? menu.name : "none")}");
                }
                EditorUtility.SetDirty(holder);
                Save(scene);
            }
        }
    }
}
