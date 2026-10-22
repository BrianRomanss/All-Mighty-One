using System.Linq;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Wires Block Puzzle: Classic and Adventure get a rewarded "Continue" on game over (one revive per run);
    /// Adventure's first levels get an easier target score.
    /// </summary>
    public static class BlockPuzzleSetup
    {
        public const string MenuScene = "Assets/Most In One/Grid/Scene/BlockBlast MainMenu.unity";
        public const string ClassicScene = "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity";
        public const string AdventureScene = "Assets/Most In One/Grid/Scene/BlockBlast Adventure.unity";

        [MenuItem("All Mighty One/Block Puzzle/Setup Classic Scene")]
        public static void Run()
        {
            var scene = Open(ClassicScene);

            var gm = FindComponent<UniversalGameManager>();
            gm.MaxRevivesPerRun = 1;
            EditorUtility.SetDirty(gm);

            var game = FindComponent<BlockBlastGame>();
            var restart = Find("Lose UI/Restart Button");
            var cont = CloneButton(restart, "Continue Button", "CONTINUE (AD)", new Vector2(0, -575));
            cont.GetComponent<UnityEngine.UI.Image>().color = AdButtonColor;
            AddListener(cont.onClick, game, nameof(BlockBlastGame.RequestRevive));
            if (!cont.GetComponent<ReviveButtonVisibility>()) cont.gameObject.AddComponent<ReviveButtonVisibility>();

            Save(scene);
            AddScenesToBuild(new[] { MenuScene, ClassicScene, AdventureScene });
            Debug.Log("[AMO] Block Puzzle classic setup done");
        }

        // Target scores of the first adventure levels (the kit uses 3500 from level 1; 4500 from level 10 stays)
        static readonly (int level, int target)[] EasyStart = { (1, 2000), (2, 2500), (3, 3000) };

        [MenuItem("All Mighty One/Block Puzzle/Setup Adventure Scene")]
        public static void RunAdventure()
        {
            var scene = Open(AdventureScene);

            var gm = FindComponent<UniversalGameManager>();
            gm.MaxRevivesPerRun = 1;
            gm.LoopFromLevel = 10; // after level 24, replay 10-24 in a fixed shuffled order
            EditorUtility.SetDirty(gm);
            foreach (var (level, target) in EasyStart)
            {
                var profile = (GridLevelProfile)gm.LevelsScriptables.First(l => l.name == $"Level {level}");
                profile.CustomValue = target;
                EditorUtility.SetDirty(profile);
            }

            var game = FindComponent<BlockBlastGame>();
            var restart = Find("Lose UI/Restart Button");
            var cont = CloneButton(restart, "Continue Button", "CONTINUE (AD)", new Vector2(0, -575));
            cont.GetComponent<UnityEngine.UI.Image>().color = AdButtonColor;
            AddListener(cont.onClick, game, nameof(BlockBlastGame.RequestRevive));
            if (!cont.GetComponent<ReviveButtonVisibility>()) cont.gameObject.AddComponent<ReviveButtonVisibility>();

            Save(scene);
            AssetDatabase.SaveAssets();
            AddScenesToBuild(new[] { MenuScene, ClassicScene, AdventureScene });
            Debug.Log($"[AMO] Block Puzzle adventure setup done: {gm.LevelsScriptables.Length} levels");
        }
    }
}
