using System.IO;
using System.Linq;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary> Wires the Pistol scene: 30 levels, rewarded revive on lose, rewarded x3 gems on win. </summary>
    public static class PistolSetup
    {
        public const string ScenePath = "Assets/Most In One/Runner/Scenes/Pistol.unity";

        [MenuItem("All Mighty One/Pistol/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);

            var gm = FindComponent<UniversalGameManager>();
            gm.LevelsPrefs = AssetDatabase.FindAssets("t:Prefab", new[] { PistolLevelTools.Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (n: int.TryParse(Path.GetFileNameWithoutExtension(p).Replace("Pistol_Level", "").Trim(), out int n) ? n : -1, p))
                .Where(x => x.n > 0).OrderBy(x => x.n)
                .Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.p)).ToArray();
            gm.InGameMenuName = "InGame UI";
            gm.LoopFromLevel = 4;
            gm.MaxRevivesPerRun = 1;
            EditorUtility.SetDirty(gm);

            var player = FindComponent<CharacterControl_Gun>();
            var end = FindComponent<EndGameUIHandler>();
            var font = Find("Lose UI/Continue Button Lose/Text (Legacy)").GetComponent<Text>();

            var lose = Find("Lose UI").transform;
            var revive = CreateTextButton(lose, "Revive Button", "REVIVE (AD)", new Vector2(0, 250), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, player, nameof(CharacterControl_Gun.RequestRevive));
            revive.gameObject.AddComponent<ReviveButtonVisibility>();

            var win = Find("Win UI").transform;
            var triple = CreateTextButton(win, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, 80), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, end, nameof(EndGameUIHandler.RequestTripleReward));
            triple.gameObject.SetActive(false); // shown by EndGameUIHandler after the gem count
            end.TripleRewardButton = triple.gameObject;
            EditorUtility.SetDirty(end);

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] Pistol setup done: {gm.LevelsPrefs.Length} levels");
        }

        // Batch entry point: generate levels, then wire the scene
        public static void BuildAll()
        {
            PistolLevelTools.GenerateMenu();
            Run();
        }
    }
}
