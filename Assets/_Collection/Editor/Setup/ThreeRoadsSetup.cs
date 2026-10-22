using System.Collections.Generic;
using System.Linq;
using AllMightyOne.ThreeRoads;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Wires the 3 Roads scene: speed ramp, generated track pieces by difficulty, gems per run with rewarded x3,
    /// rewarded revive, title and hints.
    /// </summary>
    public static class ThreeRoadsSetup
    {
        public const string ScenePath = "Assets/Most In One/ThreeRoads/Scene/3Road.unity";

        [MenuItem("All Mighty One/3 Roads/Generate Pieces And Setup Scene")]
        public static void BuildAll()
        {
            ThreeRoadsTrackGenerator.Generate(9090);
            Run();
        }

        [MenuItem("All Mighty One/3 Roads/Setup Scene")]
        public static void Run()
        {
            var scene = Open(ScenePath);
            var gm = FindComponent<UniversalGameManager>();
            var roads = FindComponent<ThreeRoadsController>();
            var db = roads.DatabaseHolder;

            gm.InGameMenuName = "InGame UI"; // was "InGame": no such menu
            gm.MaxRevivesPerRun = 1;
            gm.LevelDataName = ""; // was Big Brain's: analytics would file 3 Roads runs under Big Brain (GameId falls back to the scene name)
            EditorUtility.SetDirty(gm);

            // speed 6 -> 12 over the first 1500 m; the hand-made pieces come after an easy start, harder generated ones later
            // (easy ones retire at 900 m)
            roads.SpeedGainPerMeter = 0.004f;
            roads.MaxForwardSpeed = 12f;
            roads.Parts = BuildParts();
            EditorUtility.SetDirty(roads);

            // menu: title and how to play (the start button is a full-screen tap area)
            var menu = Find("Canvas/Menu UI").transform;
            var menuText = Find("Menu UI/Best Score Text");
            LegacyLabel(menuText, menu, "Title", "3 ROADS", new Vector2(0, 700), 170, new Color(1f, 0.82f, 0.25f));
            LegacyLabel(menuText, menu, "How To Play", "TAP TO SWITCH SIDES\nGATES ADD RUNNERS", new Vector2(0, 420), 70, Color.white);

            // end screen: gems of the run, rewarded x3 and revive above the full-screen "tap to continue"
            var lose = Find("EndGame UI/Lose UI").transform;
            var gemsText = TmpLabel(Find("Lose UI/Title"), lose, "Run Gems Text", "+0 GEMS", new Vector2(0, 250), 90, new Color(0.45f, 0.9f, 1f));
            var font = Find("Continue Button Lose/Text (Legacy)").GetComponent<Text>();

            var host = gm.gameObject;
            var meta = host.GetComponent<ThreeRoadsMeta>() ?? host.AddComponent<ThreeRoadsMeta>();
            meta.Controller = roads;
            meta.Database = db;
            meta.GemsText = gemsText;
            AddListener(gm.OnLoseEvent, meta, nameof(ThreeRoadsMeta.OnRunEnded));
            AddListener(roads.OnGameStart, meta, nameof(ThreeRoadsMeta.OnRunStarted));

            var revive = CreateTextButton(lose, "Revive Button", "REVIVE (AD)", new Vector2(0, 20), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(revive.onClick, roads, nameof(ThreeRoadsController.RequestRevive));
            revive.gameObject.AddComponent<ReviveButtonVisibility>();

            var triple = CreateTextButton(lose, "Triple Reward Button", "x3 GEMS (AD)", new Vector2(0, -200), new Vector2(760, 180), font, AdButtonColor, AdTextColor);
            AddListener(triple.onClick, meta, nameof(ThreeRoadsMeta.RequestTripleReward));
            triple.gameObject.SetActive(false);
            meta.TripleRewardButton = triple.gameObject;
            EditorUtility.SetDirty(host);

            Save(scene);
            AddScenesToBuild(new[] { ScenePath });
            Debug.Log($"[AMO] 3 Roads setup done: {roads.Parts.Count} track pieces");
        }

        static List<ThreeRoadsController.TrackPart> BuildParts()
        {
            string folder = ThreeRoadsTrackGenerator.PartsFolder;
            ThreeRoadsController.TrackPart Part(string name, float length, float weight, float minZ, float maxZ = 0) => new()
            {
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.prefab"), Length = length, Weight = weight, MinZ = minZ, MaxZ = maxZ
            };
            var parts = new List<ThreeRoadsController.TrackPart>
            {
                Part("Sub_1", 60, 5, 120), Part("Sub_2", 60, 5, 120), Part("Sub_3", 60, 5, 120), Part("Sub_Bouns", 30, 2, 0),
            };
            foreach (var tier in ThreeRoadsTrackGenerator.Tiers)
                for (int k = 1; k <= tier.Count; k++)
                    parts.Add(Part($"Sub_Gen_{tier.Name}_{k}", 60, 5, tier.MinZ, tier.MaxZ));
            var missing = parts.Where(p => !p.Prefab).ToList();
            if (missing.Count > 0) throw new System.Exception("[AMO] 3 Roads track pieces missing: run Generate Track Pieces first");
            return parts;
        }

        static TMP_Text TmpLabel(GameObject source, Transform parent, string name, string text, Vector2 pos, float size, Color color)
        {
            DestroyChild(parent, name);
            var go = Object.Instantiate(source, parent);
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400, size * 1.4f);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableAutoSizing = false;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
