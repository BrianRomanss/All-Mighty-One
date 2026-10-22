using AllMightyOne.Dodge;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Wires the Bull Dodge scene: spawn-rate ramp, tiered close-dodge score, best score, gems per run with rewarded x3,
    /// rewarded revive, bull cleanup, title and end screen.
    /// </summary>
    public static class DodgeSetup
    {
        public const string ScenePath = "Assets/Most In One/Dodge/Scenes/Dodge.unity";

        [MenuItem("All Mighty One/Bull Dodge/Setup Scene")]
        public static void Run()
        {
            var (kitFont, kitStyle) = KitButtonFont();
            var scene = Open(ScenePath);
            var fontHolder = new GameObject("font holder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var font = fontHolder.GetComponent<Text>();
            font.font = kitFont;
            font.fontStyle = kitStyle;

            var gm = FindComponent<UniversalGameManager>();
            var character = FindComponent<CharacterControl_Dodge>();
            var spawner = FindComponent<MOST_Spawn>();
            var db = character.ScoreHolder;
            gm.MaxRevivesPerRun = 1;
            EditorUtility.SetDirty(gm);

            // difficulty ramp: 3 bulls/s at the start, one more per cycle every 8 s, up to 7 bulls/s after ~64 s
            spawner.SpawnRatePerCycle = 6;
            spawner.EnableRateAccelerator = true;
            spawner.TargetSpawnRate = 14;
            spawner.AddedAmount = 1;
            spawner.UpdateTime = 8f;
            spawner.EnableDebug = false;
            EditorUtility.SetDirty(spawner);

            // the menu's "Best" showed the current score (0 on the menu); it is the all-time best
            var mainActions = gm.GetComponent<MOST_Action>();
            if (mainActions.GetAction("Display Best Score Data") is MOSTAction_DisplayData best) best.ShowHighestValue = true;
            EditorUtility.SetDirty(mainActions);

            // menu: title and start hint (the start button is a full-screen invisible tap area)
            var menu = Find("Canvas/Menu UI").transform;
            var labelSource = Find("Menu UI/Best Score");
            Label(labelSource, menu, "Title", "BULL DODGE", new Vector2(0, 600), 150, new Color(1f, 0.62f, 0.18f));
            DestroyChild(menu, "Tap Hint"); // the kit's menu already says "Tap To Start"

            // end screen
            var endUi = Find("Canvas/End Game UI").transform;
            Label(labelSource, endUi, "Game Over Title", "GAME OVER", new Vector2(0, 900), 130, new Color(1f, 0.35f, 0.25f));
            var scoreText = Label(labelSource, endUi, "Run Score Text", "SCORE 0", new Vector2(0, 680), 100, Color.white);
            var bestText = Label(labelSource, endUi, "Run Best Text", "BEST 0", new Vector2(0, 560), 70, new Color(1f, 0.85f, 0.3f));
            var gemsText = Label(labelSource, endUi, "Run Gems Text", "+0 GEMS", new Vector2(0, 440), 80, new Color(0.45f, 0.9f, 1f));
            Label(labelSource, endUi, "Restart Hint", "TAP TO RETRY", new Vector2(0, -700), 80, Color.white);

            var host = gm.gameObject;
            var meta = host.GetComponent<DodgeMeta>() ?? host.AddComponent<DodgeMeta>();
            meta.Character = character;
            meta.Spawner = spawner;
            meta.Database = db;
            meta.ScoreText = scoreText;
            meta.BestText = bestText;
            meta.GemsText = gemsText;
            AddListener(gm.OnLoseEvent, meta, nameof(DodgeMeta.OnRunEnded));
            EditorUtility.SetDirty(host);

            var revive = CreateTextButton(endUi, "Revive Button", "REVIVE (AD)", new Vector2(0, 180), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, character, nameof(CharacterControl_Dodge.RequestRevive));
            revive.gameObject.AddComponent<ReviveButtonVisibility>();

            var triple = CreateTextButton(endUi, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, -60), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, meta, nameof(DodgeMeta.RequestTripleReward));
            triple.gameObject.SetActive(false);
            meta.TripleRewardButton = triple.gameObject;

            // revive undoes what OnDefeat / OnLose switched off
            character.OnRevive ??= new UnityEvent();
            for (int i = character.OnRevive.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(character.OnRevive, i);
            var joystick = FindComponent<MOST_Controller>();
            UnityEventTools.AddBoolPersistentListener(character.OnRevive, joystick.EnableState, true);
            var effects = Find("Character_Dodge/Appearance_Effects");
            UnityEventTools.AddBoolPersistentListener(character.OnRevive, effects.SetActive, true);
            EditorUtility.SetDirty(character);

            Object.DestroyImmediate(fontHolder);
            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log("[AMO] Bull Dodge setup done");
        }

        static TMP_Text Label(GameObject source, Transform parent, string name, string text, Vector2 pos, float size, Color color)
        {
            DestroyChild(parent, name);
            var go = Object.Instantiate(source, parent);
            go.name = name;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400, size * 1.4f);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableAutoSizing = false;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            foreach (var anim in go.GetComponents<Animation>()) Object.DestroyImmediate(anim);
            return tmp;
        }

        // Dodge has no legacy-Text UI; the rewarded buttons use the runner games' button font
        internal static (Font, FontStyle) KitButtonFont()
        {
            EditorSceneManager.OpenScene(BigBrainSetup.ScenePath, OpenSceneMode.Single);
            var text = Find("StartGame Button/Text (Legacy)").GetComponent<Text>();
            return (text.font, text.fontStyle);
        }
    }
}
