using System.Linq;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary> Wires the Cubey scene: 100 levels, hint/skip (rewarded) buttons, stuck message, no revive. </summary>
    public static class CubeySetup
    {
        public const string ScenePath = "Assets/Most In One/Grid/Scene/Cubey.unity";

        [MenuItem("All Mighty One/Cubey/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);

            var gm = FindComponent<UniversalGameManager>();
            gm.LevelsPrefs = CubeyLevelTools.LevelPrefabs(CubeyLevelTools.CubeyLevelsFolder, "CubeyLevel")
                .Select(l => AssetDatabase.LoadAssetAtPath<GameObject>(l.path)).ToArray();
            gm.MaxRevivesPerRun = 0; // restarting is free in a puzzle; hint/skip are the rewarded placements
            gm.LoopFromLevel = 10;
            EditorUtility.SetDirty(gm);

            var player = FindComponent<CharacterControl_Cubey>();
            player.LoseReasonText = Find("Lose UI/LevelText").GetComponent<Text>();
            EditorUtility.SetDirty(player);

            var inGame = Find("Canvas/InGame UI") ?? Find("InGame UI");
            var font = Find("Lose UI/Restart Button/Text (Legacy)").GetComponent<Text>();
            var hint = CreateTextButton(inGame.transform, "Hint Button", "HINT (AD)", new Vector2(-260, -780), new Vector2(440, 150), font, AdButtonColor, AdTextColor);
            AddListener(hint.onClick, player, nameof(CharacterControl_Cubey.RequestHint));
            var skip = CreateTextButton(inGame.transform, "Skip Button", "SKIP (AD)", new Vector2(260, -780), new Vector2(440, 150), font, AdButtonColor, AdTextColor);
            AddListener(skip.onClick, player, nameof(CharacterControl_Cubey.RequestSkip));

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] Cubey setup done: {gm.LevelsPrefs.Length} levels");
        }
    }
}
