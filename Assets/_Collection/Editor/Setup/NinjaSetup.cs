using System.Linq;
using AllMightyOne.Ninja;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Turns the Ninja sandbox into Ninja Arena: zombie waves per level (NinjaArena), menu / HUD / win / lose screens,
    /// rewarded revive and x3 gems. Removes the training dummies and the game-manager wiring copied from Big Brain.
    /// </summary>
    public static class NinjaSetup
    {
        public const string ScenePath = "Assets/Most In One/Controls/Scenes/Ninja.unity";

        [MenuItem("All Mighty One/Ninja Arena/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);
            var gm = FindComponent<UniversalGameManager>();
            var host = gm.gameObject;

            // ---- game manager: the kit copied Big Brain's (start event -> Big Brain runner, Big Brain level scenes)
            gm.LevelStoreType = LevelStoringType.None; // NinjaArena keeps the level number
            gm.LevelDataName = ""; // was Big Brain's: analytics would file Ninja runs under Big Brain (GameId falls back to the scene name)
            gm.LevelsScene = new SceneAsset[0];
            var so = new SerializedObject(gm);
            so.FindProperty("_levelsScenePaths").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            gm.UseMenuJump = true;
            gm.InGameMenuName = "InGame UI";
            gm.MaxRevivesPerRun = 1;
            ClearListeners(gm.OnStartPlay);
            ClearListeners(gm.OnWinEvent);
            var endHandler = host.GetComponent<EndGameUIHandler>(); // runner-only gem counter
            if (endHandler) Object.DestroyImmediate(endHandler);

            // ---- arena: no training dummies; one zombie kept as the spawn template
            foreach (var name in new[] { "Dummy", "Dummy (1)", "Dummy (2)", "Zombie (1)", "Zombie (2)", "Zombie (3)", "Zombie (4)" })
            {
                var go = Find("Objects/" + name);
                if (go) Object.DestroyImmediate(go);
            }
            var template = Find("Objects/Zombie");
            template.SetActive(false);

            var player = FindComponent<NinjaMode>();
            var arena = host.GetComponent<NinjaArena>() ?? host.AddComponent<NinjaArena>();
            arena.Manager = gm;
            arena.Database = gm.DatabaseHolder;
            arena.Player = player.transform;
            arena.Movement = player.GetComponent<MOST_FreeMovement>();
            arena.Modes = player;
            arena.ZombieTemplate = template;

            // ---- controls: joysticks and weapon buttons in one container, shown only while playing
            var canvas = Find("Main/Canvas").transform;
            var controls = Find("Canvas/Play Controls")?.transform;
            if (!controls)
            {
                controls = new GameObject("Play Controls", typeof(RectTransform)).transform;
                var rt = (RectTransform)controls;
                rt.SetParent(canvas, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.SetSiblingIndex(Find("Canvas/JoyStick Move").transform.GetSiblingIndex());
                foreach (var name in new[] { "JoyStick Move", "JoyStick Aim", "JoyStick Special", "SwapToSuriken Bt", "SwapToKatana Bt" })
                    Find("Canvas/" + name).transform.SetParent(controls, false);
            }
            arena.PlayControls = controls.gameObject;

            var labelSource = Find("ScoreRecord/Current Level Text");
            var font = labelSource.GetComponent<Text>();

            // ---- menu
            var menu = Find("Canvas/Menu UI");
            menu.SetActive(true);
            Find("Canvas/InGame UI").SetActive(false);
            Find("Menu UI/StartBiggerReward").SetActive(false);
            LegacyLabel(labelSource, menu.transform, "Title", "NINJA ARENA", new Vector2(0, 650), 170, new Color(1f, 0.35f, 0.3f));
            arena.MenuLevelText = LegacyLabel(labelSource, menu.transform, "Level Text", "LEVEL 1", new Vector2(0, 450), 100, Color.white);
            DestroyChild(menu.transform, "Tap Hint"); // the kit's start button already says "Tap To Start"
            var start = Find("Menu UI/StartGame Button").GetComponent<Button>();
            AddListener(start.onClick, gm, nameof(UniversalGameManager.OnStart));
            AddListener(gm.OnStartPlay, arena, nameof(NinjaArena.Begin));

            // ---- HUD
            var inGame = Find("Canvas/InGame UI").transform;
            arena.LevelText = LegacyLabel(labelSource, inGame, "Level Text", "LEVEL 1", new Vector2(0, 1020), 90, Color.white);
            arena.WaveText = LegacyLabel(labelSource, inGame, "Wave Text", "WAVE 1/1", new Vector2(0, 920), 70, new Color(1f, 0.85f, 0.3f));
            arena.HealthFill = HealthBar(inGame, new Vector2(0, 830));

            // ---- win screen
            Find("Canvas/EndGame UI").SetActive(true); // the kit had the container off: Win / Lose UI could never show
            var win = Find("EndGame UI/Win UI").transform;
            LegacyLabel(labelSource, win, "Headline", "LEVEL CLEARED!", new Vector2(0, 700), 130, new Color(0.5f, 1f, 0.5f));
            arena.WinGemsText = LegacyLabel(labelSource, win, "Gems Text", "+0 GEMS", new Vector2(0, 520), 100, new Color(0.45f, 0.9f, 1f));
            var triple = CreateTextButton(win, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, 150), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, arena, nameof(NinjaArena.RequestTripleReward));
            triple.gameObject.SetActive(false);
            arena.TripleRewardButton = triple.gameObject;
            var next = CreateTextButton(win, "Next Level Button", "NEXT LEVEL", new Vector2(0, -150), new Vector2(760, 180), font, new Color(0.3f, 0.75f, 0.35f), Color.white);
            AddListener(next.onClick, arena, nameof(NinjaArena.Reload));

            // ---- lose screen
            var lose = Find("EndGame UI/Lose UI").transform;
            LegacyLabel(labelSource, lose, "Headline", "DEFEATED", new Vector2(0, 700), 130, new Color(1f, 0.35f, 0.3f));
            arena.LoseGemsText = LegacyLabel(labelSource, lose, "Gems Text", "+0 GEMS", new Vector2(0, 520), 100, new Color(0.45f, 0.9f, 1f));
            var revive = CreateTextButton(lose, "Revive Button", "REVIVE (AD)", new Vector2(0, 150), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, arena, nameof(NinjaArena.RequestRevive));
            if (!revive.GetComponent<ReviveButtonVisibility>()) revive.gameObject.AddComponent<ReviveButtonVisibility>();
            var retry = CreateTextButton(lose, "Retry Button", "RETRY", new Vector2(0, -150), new Vector2(760, 180), font, new Color(0.85f, 0.3f, 0.3f), Color.white);
            AddListener(retry.onClick, arena, nameof(NinjaArena.Reload));

            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(host);
            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log("[AMO] Ninja Arena setup done");
        }

        static void ClearListeners(UnityEngine.Events.UnityEventBase evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(evt, i);
        }

        // a rounded bar with a horizontal fill image
        static Image HealthBar(Transform parent, Vector2 pos)
        {
            DestroyChild(parent, "Health Bar");
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var bg = new GameObject("Health Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)bg.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(720, 56);
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = sprite; bgImg.type = Image.Type.Sliced; bgImg.color = new Color(0, 0, 0, 0.55f);
            bgImg.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var frt = (RectTransform)fill.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(8, 8); frt.offsetMax = new Vector2(-8, -8);
            var img = fill.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.color = new Color(0.35f, 0.9f, 0.35f);
            img.raycastTarget = false;
            return img;
        }
    }
}
