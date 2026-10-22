using AllMightyOne.Bomb;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Turns the Explosion hazard demo into Bomb Survival: game manager and menu / HUD / game-over screens, hazards that
    /// start with the run and speed up, grid movement that respects walls, rewarded revive and x3 gems.
    /// Also repairs the kit's event wiring that still pointed at renamed classes (cannon and line explosions never fired).
    /// </summary>
    public static class BombSetup
    {
        public const string ScenePath = "Assets/Most In One/Spawn/Scenes/Explosion.unity";
        const string DatabasePath = "Assets/Most In One/Common/MOST/Stored Data/M_Database.asset";
        const int BlockLayer = 6; // walls, pillars, bricks and bombs

        [MenuItem("All Mighty One/Bomb Survival/Setup Scene")]
        public static void Run()
        {
            var (kitFont, kitStyle) = DodgeSetup.KitButtonFont();
            var scene = Open(ScenePath);
            var host = Find("Main");
            var canvas = Find("Main/Canvas").transform;

            // ---- repaired kit wiring
            var cannon = FindComponent<MOST_ProjectileGenerator>();
            var cannonSpawn = Find("SpawnSystem_Cannon").GetComponent<MOST_Spawn>();
            Clear(cannonSpawn.OnSpawnCalledEvent); // was "ProjectileGenerator.SpawnProjectile" (renamed class)
            UnityEventTools.AddPersistentListener(cannonSpawn.OnSpawnCalledEvent,
                (UnityAction<Vector3>)System.Delegate.CreateDelegate(typeof(UnityAction<Vector3>), cannon, typeof(MOST_ProjectileGenerator).GetMethod("SpawnProjectile", new[] { typeof(Vector3) })));
            var lines = FindComponent<ExplosionLine>();
            var lineSpawn = Find("SpawnSystem_Lines").GetComponent<MOST_Spawn>();
            Clear(lineSpawn.OnCycleEnd); // was "LineControl.ActivateNewExplosion" (renamed class)
            AddListener(lineSpawn.OnCycleEnd, lines, nameof(ExplosionLine.ActivateNewExplosion));
            Clear(lines.OnExplosion); // "CameraShake.Shake" on a missing object

            // ---- hazards start with the run and speed up
            var bombs = Find("SpawnSystem_Bombs").GetComponent<MOST_Spawn>();
            Ramp(bombs, 2, 8, 12f);       // 2 bombs / 4 s, +1 every 12 s
            Ramp(cannonSpawn, 1, 3, 20f); // cannon volleys
            Ramp(lineSpawn, 3, 7, 24f);   // line blasts per cycle
            foreach (var s in new[] { bombs, cannonSpawn, lineSpawn })
            {
                s.EnableOnStart = false;
                s.IgnoredPositionsLayers = 1 << BlockLayer; // no bombs inside pillars
                EditorUtility.SetDirty(s);
            }

            // ---- player: grid movement stops at walls, pillars, bricks and bombs (ObstacleLayers was empty)
            var player = Find("Character_Spawn");
            var movement = player.GetComponent<MOST_GridMovement>();
            movement.ObstacleLayers = 1 << BlockLayer;
            movement.EnableOnStart = false;
            EditorUtility.SetDirty(movement);
            var damage = player.GetComponent<MOST_Damage>();

            // ---- UI
            var font = new GameObject("font holder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            font.font = kitFont;
            font.fontStyle = kitStyle;

            var joystick = Find("Canvas/JoyStick");
            var menu = Panel(canvas, "Menu UI", false);
            var backdrop = menu.gameObject.AddComponent<Image>(); // the arena is busy: dim it so the menu reads well
            backdrop.color = new Color(0.05f, 0.02f, 0.12f, 0.45f);
            var start = CreateTextButton(menu, "StartGame Button", "", Vector2.zero, new Vector2(3000, 4000), font, new Color(0, 0, 0, 0), Color.clear);
            Label(font, menu, "Title", "BOMB SURVIVAL", new Vector2(0, 650), 112, new Color(1f, 0.55f, 0.2f));
            var menuBest = Label(font, menu, "Best Text", "BEST 0s", new Vector2(0, 470), 90, Color.white);
            Label(font, menu, "How To Play", "DODGE THE BLASTS\nSURVIVE AS LONG AS YOU CAN", new Vector2(0, -350), 65, Color.white);
            Label(font, menu, "Tap Hint", "TAP TO START", new Vector2(0, -650), 85, new Color(1f, 0.85f, 0.3f));

            var inGame = Panel(canvas, "InGame UI", false);
            var timeText = Label(font, inGame, "Time Text", "0s", new Vector2(0, 950), 130, Color.white);

            var lose = Panel(canvas, "Lose UI", true);
            Label(font, lose, "Headline", "BOOM!", new Vector2(0, 750), 150, new Color(1f, 0.35f, 0.25f));
            var runTime = Label(font, lose, "Run Time Text", "0s", new Vector2(0, 580), 120, Color.white);
            var runBest = Label(font, lose, "Run Best Text", "BEST 0s", new Vector2(0, 450), 80, new Color(1f, 0.85f, 0.3f));
            var runGems = Label(font, lose, "Run Gems Text", "+0 GEMS", new Vector2(0, 330), 90, new Color(0.45f, 0.9f, 1f));
            var revive = CreateTextButton(lose, "Revive Button", "REVIVE (AD)", new Vector2(0, 100), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            revive.gameObject.AddComponent<ReviveButtonVisibility>();
            var triple = CreateTextButton(lose, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, -130), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            triple.gameObject.SetActive(false);
            var retry = CreateTextButton(lose, "Retry Button", "RETRY", new Vector2(0, -360), new Vector2(760, 180), font, new Color(0.85f, 0.3f, 0.3f), Color.white);
            joystick.transform.SetAsLastSibling(); // above the panels while playing
            lose.SetAsLastSibling();

            // ---- game manager (the scene had none)
            var gm = host.GetComponent<UniversalGameManager>() ?? host.AddComponent<UniversalGameManager>();
            gm.LevelStoreType = LevelStoringType.None;
            gm.LevelDataName = "";
            gm.DatabaseHolder = AssetDatabase.LoadAssetAtPath<MOST_Database>(DatabasePath);
            gm.MainMenu = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            var so = new SerializedObject(gm);
            so.FindProperty("_mainMenuPath").stringValue = ScenePath;
            so.ApplyModifiedPropertiesWithoutUndo();
            gm.UseMenuJump = true;
            gm.Menus = new[]
            {
                new ObjectsJump { Name = "Menu UI", TargetObject = menu.gameObject, DeactivateAllOnEnable = true },
                new ObjectsJump { Name = "InGame UI", TargetObject = inGame.gameObject, Listener = start, DeactivateAllOnEnable = true },
                new ObjectsJump { Name = "OnLose UI", TargetObject = lose.gameObject, DeactivateAllOnEnable = true },
            };
            gm.InGameMenuName = "InGame UI";
            gm.OnLoseMenuName = "OnLose UI";
            gm.DelayBeforeLoseJump = 1.5f;
            gm.MaxRevivesPerRun = 1;
            menu.gameObject.SetActive(true);

            var game = host.GetComponent<BombSurvival>() ?? host.AddComponent<BombSurvival>();
            game.Manager = gm;
            game.Database = gm.DatabaseHolder;
            game.Player = damage;
            game.Movement = movement;
            game.Hazards = new[] { bombs, cannonSpawn, lineSpawn };
            game.PlayControls = joystick;
            game.TimeText = timeText;
            game.MenuBestText = menuBest;
            game.RunTimeText = runTime;
            game.RunBestText = runBest;
            game.RunGemsText = runGems;
            game.TripleRewardButton = triple.gameObject;

            AddListener(start.onClick, gm, nameof(UniversalGameManager.OnStart));
            AddListener(gm.OnStartPlay, game, nameof(BombSurvival.Begin));
            AddListener(damage.OnDefeat, game, nameof(BombSurvival.OnPlayerDefeated));
            AddListener(revive.onClick, game, nameof(BombSurvival.RequestRevive));
            AddListener(triple.onClick, game, nameof(BombSurvival.RequestTripleReward));
            AddListener(retry.onClick, game, nameof(BombSurvival.Reload));
            EditorUtility.SetDirty(damage);
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(game);

            Object.DestroyImmediate(font.gameObject);
            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log("[AMO] Bomb Survival setup done");
        }

        static void Clear(UnityEventBase evt)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(evt, i);
        }

        static void Ramp(MOST_Spawn s, int start, int target, float every)
        {
            s.SpawnRatePerCycle = start;
            s.EnableRateAccelerator = true;
            s.TargetSpawnRate = target;
            s.AddedAmount = 1;
            s.UpdateTime = every;
            s.EnableDebug = false;
        }

        static Transform Panel(Transform canvas, string name, bool dim)
        {
            DestroyChild(canvas, name);
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (dim)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.6f);
            }
            go.SetActive(false);
            return rt;
        }

        static Text Label(Text font, Transform parent, string name, string text, Vector2 pos, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1500, size * 2.6f);
            var t = go.GetComponent<Text>();
            t.font = font.font;
            t.fontStyle = font.fontStyle;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.6f);
            outline.effectDistance = new Vector2(4, -4);
            return t;
        }
    }
}
