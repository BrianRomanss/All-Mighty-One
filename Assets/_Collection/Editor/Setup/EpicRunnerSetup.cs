using System.IO;
using System.Linq;
using AllMightyOne.Runner;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Wires the Squad Runner (EpicRunner) scene: 30 levels, persistent Squad/Power upgrades (cards copied from the Pistol
    /// scene for a matching look), rewarded revive on lose and rewarded x3 gems on win.
    /// </summary>
    public static class EpicRunnerSetup
    {
        public const string ScenePath = "Assets/Most In One/Runner/Scenes/EpicRunner.unity";

        public static void BuildAll()
        {
            EpicRunnerLevelTools.GenerateMenu();
            Run();
        }

        [MenuItem("All Mighty One/Squad Runner/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);

            var gm = FindComponent<UniversalGameManager>();
            gm.LevelsPrefs = AssetDatabase.FindAssets("t:Prefab", new[] { EpicRunnerLevelTools.Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (n: int.TryParse(Path.GetFileNameWithoutExtension(p).Replace("EpicRunner_Level", "").Trim(), out int n) ? n : -1, p))
                .Where(x => x.n > 0).OrderBy(x => x.n)
                .Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.p)).ToArray();
            gm.InGameMenuName = "InGame UI";
            gm.LoopFromLevel = 4;
            gm.MaxRevivesPerRun = 1;
            EditorUtility.SetDirty(gm);

            var runner = FindComponent<CharacterControl_ShootRunner>();
            var end = FindComponent<EndGameUIHandler>();
            var menu = Find("Menu UI").transform;
            var font = Find("Lose UI/Continue Button Lose/Text (Legacy)").GetComponent<Text>();

            // upgrade cards copied from the Pistol scene
            var squadCard = CopyCard(menu, "Upgrade_Health_Button", "Upgrade_Squad_Button", "Squad", new Vector2(-170, -420));
            var powerCard = CopyCard(menu, "Upgrade_FireRate_Button", "Upgrade_Power_Button", "Power", new Vector2(170, -420));

            var host = gm.gameObject;
            var shop = host.GetComponent<RunnerUpgradeShop>() ?? host.AddComponent<RunnerUpgradeShop>();
            shop.Database = gm.DatabaseHolder;
            shop.SaveKeyPrefix = "AMO_Squad_";
            shop.Cards = new()
            {
                new RunnerUpgradeShop.Card { Id = "squad", Button = squadCard, MaxLevel = 10, BaseCost = 40, CostGrowth = 1.3f, BaseValue = 1, ValuePerLevel = 1 },
                new RunnerUpgradeShop.Card { Id = "tier", Button = powerCard, MaxLevel = runner.ChildPrefabs.Length, BaseCost = 300, CostGrowth = 3f, BaseValue = 1, ValuePerLevel = 1 },
            };
            var meta = host.GetComponent<SquadRunnerMeta>() ?? host.AddComponent<SquadRunnerMeta>();
            meta.Runner = runner;
            meta.Shop = shop;
            EditorUtility.SetDirty(host);

            var revive = CreateTextButton(Find("Lose UI").transform, "Revive Button", "REVIVE (AD)", new Vector2(0, 250), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, runner, nameof(CharacterControl_ShootRunner.RequestRevive));
            revive.gameObject.AddComponent<ReviveButtonVisibility>();

            var triple = CreateTextButton(Find("Win UI").transform, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, 80), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, end, nameof(EndGameUIHandler.RequestTripleReward));
            triple.gameObject.SetActive(false);
            end.TripleRewardButton = triple.gameObject;
            EditorUtility.SetDirty(end);

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] Squad Runner setup done: {gm.LevelsPrefs.Length} levels");
        }

        /// <summary>
        /// The suburb scenery is ~1650 static renderers that all cast shadows. Scenery away from the road stops casting shadows
        /// (it cannot shadow the play area), and the scenery is marked for static batching when the memory cost is acceptable.
        /// </summary>
        [MenuItem("All Mighty One/Squad Runner/Optimize Environment")]
        public static void OptimizeEnvironment()
        {
            var scene = Open(ScenePath);
            var env = Find("Objects/Environment");
            var renderers = env.GetComponentsInChildren<MeshRenderer>(true);
            int shadowsOff = 0;
            long vertices = 0;
            foreach (var r in renderers)
            {
                var b = r.bounds;
                bool nearRoad = b.min.x < RoadHalfWidth && b.max.x > -RoadHalfWidth;
                if (!nearRoad && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    shadowsOff++;
                }
                if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) vertices += mf.sharedMesh.vertexCount;
            }

            // static batching copies every mesh into combined buffers: ~48 bytes per vertex for position/normal/tangent/uv
            float batchMB = vertices * 48f / (1024 * 1024);
            bool batch = batchMB <= MaxStaticBatchMB;
            if (batch)
                foreach (var t in env.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(t.gameObject) | StaticEditorFlags.BatchingStatic);

            Save(scene);
            Debug.Log($"[AMO] Environment: {renderers.Length} renderers, shadows turned off on {shadowsOff}, " +
                      $"static batching {(batch ? "enabled" : "skipped")} (~{batchMB:0} MB for {vertices} vertices)");
        }

        const float RoadHalfWidth = 8f;
        const float MaxStaticBatchMB = 40f;

        /// <summary> Copies an upgrade card from the Pistol scene (matching art) into a menu, retitled and unwired. </summary>
        public static Button CopyCard(Transform menu, string sourceName, string newName, string title, Vector2 pos)
        {
            DestroyChild(menu, newName);
            var pistol = EditorSceneManager.OpenScene(PistolSetup.ScenePath, OpenSceneMode.Additive);
            var source = pistol.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == sourceName);
            var copy = Object.Instantiate(source.gameObject, menu);
            EditorSceneManager.CloseScene(pistol, true);

            copy.name = newName;
            ((RectTransform)copy.transform).anchoredPosition = pos;
            var titleText = copy.transform.Find("Title text")?.GetComponent<TMPro.TMP_Text>();
            if (titleText) titleText.text = title;
            var button = copy.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            return button;
        }
    }
}
