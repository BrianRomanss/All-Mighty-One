using System.Linq;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary> Wires the Splat scene: 50 generated levels, Splat rules for hints, hint/skip (rewarded) buttons, no revive. </summary>
    public static class SplatSetup
    {
        public const string ScenePath = "Assets/Most In One/Grid/Scene/Splat.unity";

        [MenuItem("All Mighty One/Splat/Generate Levels And Setup Scene")]
        public static void BuildAll()
        {
            SplatLevelTools.GenerateMenu();
            if (SplatLevelTools.Validate()) Run();
        }

        [MenuItem("All Mighty One/Splat/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);

            var gm = FindComponent<UniversalGameManager>();
            gm.LevelsPrefs = CubeyLevelTools.LevelPrefabs(SplatLevelTools.SplatLevelsFolder, "SplatLevel")
                .Select(l => AssetDatabase.LoadAssetAtPath<GameObject>(l.path)).ToArray();
            gm.MaxRevivesPerRun = 0; // a Splat level cannot be lost; restarting is free, hint/skip are the rewarded placements
            gm.LoopFromLevel = 10;
            EditorUtility.SetDirty(gm);

            var player = FindComponent<CharacterControl_Cubey>();
            player.PaintedTilesStayOpen = true;
            player.AdPlacementPrefix = "splat";
            player.LoseWhenStuck = false; // painted tiles never block, so there is always a move
            EditorUtility.SetDirty(player);

            var inGame = Find("Canvas/InGame UI");
            var font = Find("Lose UI/Restart Button/Text (Legacy)")?.GetComponent<Text>() ?? Find("Progress/Current Level").GetComponent<Text>();
            var hint = CreateTextButton(inGame.transform, "Hint Button", "HINT (AD)", new Vector2(-260, -780), new Vector2(440, 150), font, AdButtonColor, AdTextColor);
            AddListener(hint.onClick, player, nameof(CharacterControl_Cubey.RequestHint));
            var skip = CreateTextButton(inGame.transform, "Skip Button", "SKIP (AD)", new Vector2(260, -780), new Vector2(440, 150), font, AdButtonColor, AdTextColor);
            AddListener(skip.onClick, player, nameof(CharacterControl_Cubey.RequestSkip));

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] Splat setup done: {gm.LevelsPrefs.Length} levels");
        }
    }
}
