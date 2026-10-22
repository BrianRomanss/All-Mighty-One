using System.IO;
using System.Linq;
using AllMightyOne.Runner;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Wires the Big Brain scene: prefab levels (30), persistent Brain upgrade, rewarded bigger start,
    /// rewarded revive and rewarded x3 gems.
    /// </summary>
    public static class BigBrainSetup
    {
        public const string ScenePath = "Assets/Most In One/Runner/Scenes/BigBrain.unity";

        public static void BuildAll()
        {
            BigBrainLevelTools.ConvertScenesToPrefabs();
            BigBrainLevelTools.GenerateMenu();
            Run();
        }

        // Batch entry point: regenerate levels and re-wire (level scenes are already converted)
        public static void RegenerateAndWire()
        {
            BigBrainLevelTools.GenerateMenu();
            Run();
        }

        [MenuItem("All Mighty One/Big Brain/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);

            var gm = FindComponent<UniversalGameManager>();
            gm.LevelStoreType = LevelStoringType.Prefabs; // levels were scenes; they are prefabs now
            gm.LevelsPrefs = AssetDatabase.FindAssets("t:Prefab", new[] { BigBrainLevelTools.Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (n: int.TryParse(Path.GetFileNameWithoutExtension(p).Replace("BigBrain_Level_", ""), out int n) ? n : -1, p))
                .Where(x => x.n > 0).OrderBy(x => x.n)
                .Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.p)).ToArray();
            gm.InGameMenuName = "InGame UI";
            gm.LoopFromLevel = 4;
            gm.MaxRevivesPerRun = 1;
            EditorUtility.SetDirty(gm);

            var runner = FindComponent<CharacterControl_BigBrain>();
            runner.MaxHeadScale = 12f; // set explicitly: an earlier serialized value would override the code default
            runner.ReviveScale = 2.5f;
            EditorUtility.SetDirty(runner);
            var end = FindComponent<EndGameUIHandler>();
            var menu = Find("Menu UI").transform;
            var font = Find("StartGame Button/Text (Legacy)").GetComponent<Text>();

            var brainCard = EpicRunnerSetup.CopyCard(menu, "Upgrade_Health_Button", "Upgrade_Brain_Button", "Brain IQ", new Vector2(0, -450));
            var host = gm.gameObject;
            var shop = host.GetComponent<RunnerUpgradeShop>() ?? host.AddComponent<RunnerUpgradeShop>();
            shop.Database = gm.DatabaseHolder;
            shop.SaveKeyPrefix = "AMO_BigBrain_";
            shop.Cards = new()
            {
                // head scale 2.5 -> 6.0 over 15 levels, shown as IQ (x40): 100 -> 240
                new RunnerUpgradeShop.Card { Id = "brain", Button = brainCard, MaxLevel = 15, BaseCost = 50, CostGrowth = 1.3f, BaseValue = 2.5f, ValuePerLevel = 0.25f, DisplayMultiplier = 40 },
            };

            // the kit's "Start Bigger" paid 75 gems for one run; it becomes a rewarded head start
            var oldBigger = Find("Menu UI/StartBiggerReward");
            if (oldBigger) oldBigger.SetActive(false);
            var bigger = CreateTextButton(menu, "Bigger Start Button", "BIG HEAD START (AD)", new Vector2(0, -130), new Vector2(640, 150), font, AdButtonColor, AdTextColor);

            var meta = host.GetComponent<BigBrainMeta>() ?? host.AddComponent<BigBrainMeta>();
            meta.Runner = runner;
            meta.Shop = shop;
            meta.BiggerStartButton = bigger.gameObject;
            var rewarded = bigger.gameObject.AddComponent<RewardedButton>();
            rewarded.Placement = "bigbrain_bigger_start";
            rewarded.OncePerScene = true;
            rewarded.OnRewardGranted = new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddVoidPersistentListener(rewarded.OnRewardGranted, meta.BiggerStart);
            EditorUtility.SetDirty(host);

            var revive = CreateTextButton(Find("Lose UI").transform, "Revive Button", "REVIVE (AD)", new Vector2(0, 250), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, runner, nameof(CharacterControl_BigBrain.RequestRevive));
            revive.gameObject.AddComponent<ReviveButtonVisibility>();

            var triple = CreateTextButton(Find("Win UI").transform, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, 80), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, end, nameof(EndGameUIHandler.RequestTripleReward));
            triple.gameObject.SetActive(false);
            end.TripleRewardButton = triple.gameObject;
            EditorUtility.SetDirty(end);

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] Big Brain setup done: {gm.LevelsPrefs.Length} levels");
        }
    }
}
