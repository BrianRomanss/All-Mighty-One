using System.Collections.Generic;
using System.IO;
using System.Linq;
using AllMightyOne.Hub;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Builds the collection hub (Hub.unity, first scene in the build): game catalog, card grid with category tabs,
    /// continue card, gem wallet, unlock / free gems / settings / remove-ads popups. Also puts a home button
    /// (and back-key handling) into every game. Art comes from Assets/_Collection/Art/Hub (UI sprites from
    /// Tools/hub_art.py; optional Icons/&lt;id&gt;.png card art and Logo.png).
    /// </summary>
    public static partial class HubSetup
    {
        public const string ScenePath = "Assets/_Collection/Scenes/Hub.unity";
        public const string CatalogPath = "Assets/_Collection/Data/GameCatalog.asset";
        const string Art = "Assets/_Collection/Art/Hub";
        const string UiArt = Art + "/UI";
        const string KitUi = "Assets/Most In One/Common/Textures/UI";
        const string FontPath = "Assets/Most In One/Common/Third-Party/Fonts/TMP/Nunito SDF World Thicker.asset";
        const string DatabasePath = "Assets/Most In One/Common/MOST/Stored Data/M_Database.asset";
        const string AudioPath = "Assets/Most In One/Common/MOST/Stored Data/AudioManager.asset";

        static readonly Color Ink = new(0.23f, 0.16f, 0.48f);       // dark text on white panels
        static readonly Color Green = new(0.26f, 0.8f, 0.33f);
        static readonly Color Orange = new(1f, 0.6f, 0.15f);
        static readonly Color Red = new(1f, 0.3f, 0.36f);
        static readonly Color Violet = new(0.42f, 0.3f, 0.95f);

        // ------------------------------------------------------------------ catalog

        public static readonly (string id, string name, string tagline, string scene, GameCategory cat, string hex, int cost, ProgressKind kind, string key)[] Games =
        {
            ("squad", "Squad Runner", "Grow your squad, smash the boss", "EpicRunner", GameCategory.Runner, "#3D8BFF", 0, ProgressKind.DatabaseLevel, "CurrentLevel_EpicRunner"),
            ("crowd", "Crowd Count", "Pick the best gates, build a crowd", "Count", GameCategory.Runner, "#2EC27E", 0, ProgressKind.DatabaseLevel, "CurrentLevel_Count"),
            ("brain", "Big Brain", "Grow the biggest brain", "BigBrain", GameCategory.Runner, "#FF5FA2", 0, ProgressKind.DatabaseLevel, "CurrentLevel_BigBrain"),
            ("pistol", "Pistol Run", "Shoot your way to the finish", "Pistol", GameCategory.Runner, "#FF8A3D", 100, ProgressKind.DatabaseLevel, "CurrentLevel_Pistol"),
            ("dodge", "Bull Dodge", "Dodge the bulls as close as you dare", "Dodge", GameCategory.Arcade, "#E8553D", 0, ProgressKind.DatabaseBest, "Dodge Template Score"),
            ("roads", "3 Roads", "Run three roads at once", "3Road", GameCategory.Arcade, "#8A5CFF", 150, ProgressKind.DatabaseBest, "3Road Score"),
            ("bomb", "Bomb Survival", "Survive the blasts", "Explosion", GameCategory.Arcade, "#FF6B2C", 200, ProgressKind.PrefsBestSeconds, "AMO_Bomb_Best"),
            ("ninja", "Ninja Arena", "Slice through zombie waves", "Ninja", GameCategory.Arcade, "#17B3A3", 250, ProgressKind.PrefsLevel, "AMO_Ninja_Level"),
            ("blocks", "Block Puzzle", "Fill lines, chase the high score", "BlockBlast Classic", GameCategory.Puzzle, "#22B8E6", 0, ProgressKind.DatabaseBest, "BlockBlast Score"),
            ("adventure", "Block Adventure", "Reach the target in every level", "BlockBlast Adventure", GameCategory.Puzzle, "#5B6CFF", 200, ProgressKind.DatabaseLevel, "CurrentLevel_BlockBlast"),
            ("cubey", "Cubey", "Slide through every tile", "Cubey", GameCategory.Puzzle, "#FFB52E", 0, ProgressKind.DatabaseLevel, "CurrentLevel_Cubey"),
            ("splat", "Splat", "Paint the whole maze", "Splat", GameCategory.Puzzle, "#7CC943", 150, ProgressKind.DatabaseLevel, "CurrentLevel_Splat"),
        };

        public static readonly Dictionary<string, string> ScenePaths = new()
        {
            ["EpicRunner"] = "Assets/Most In One/Runner/Scenes/EpicRunner.unity",
            ["Count"] = "Assets/Most In One/Runner/Scenes/Count.unity",
            ["BigBrain"] = "Assets/Most In One/Runner/Scenes/BigBrain.unity",
            ["Pistol"] = "Assets/Most In One/Runner/Scenes/Pistol.unity",
            ["Dodge"] = "Assets/Most In One/Dodge/Scenes/Dodge.unity",
            ["3Road"] = "Assets/Most In One/ThreeRoads/Scene/3Road.unity",
            ["Explosion"] = "Assets/Most In One/Spawn/Scenes/Explosion.unity",
            ["Ninja"] = "Assets/Most In One/Controls/Scenes/Ninja.unity",
            ["BlockBlast Classic"] = "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity",
            ["BlockBlast Adventure"] = "Assets/Most In One/Grid/Scene/BlockBlast Adventure.unity",
            ["Cubey"] = "Assets/Most In One/Grid/Scene/Cubey.unity",
            ["Splat"] = "Assets/Most In One/Grid/Scene/Splat.unity",
        };

        static GameCatalog BuildCatalog()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<GameCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Database = AssetDatabase.LoadAssetAtPath<MOST_Database>(DatabasePath);
            catalog.Games = Games.Select(g => new GameEntry
            {
                Id = g.id, DisplayName = g.name, Tagline = g.tagline, SceneName = g.scene, Category = g.cat,
                Theme = Hex(g.hex), UnlockCost = g.cost, Progress = g.kind, ProgressKey = g.key, Icon = GameIcon(g.id),
            }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary> Card art: Icons/&lt;id&gt;.png (reference art) or Thumbs/&lt;id&gt;.png (captured from the game). </summary>
        static Sprite GameIcon(string id)
        {
            foreach (var folder in new[] { "Icons", "Thumbs" })
            {
                string path = $"{Art}/{folder}/{id}.png";
                if (!File.Exists(path)) continue;
                ImportSprite(path, Vector4.zero);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        // ------------------------------------------------------------------ sprites and text styles

        static readonly Dictionary<string, Vector4> Borders = new()
        {
            ["rr48"] = new Vector4(56, 56, 56, 56), ["rr24"] = new Vector4(28, 28, 28, 28), ["pill"] = new Vector4(63, 63, 63, 63),
            ["shadow"] = new Vector4(96, 96, 96, 96),
        };

        static void ImportSprite(string path, Vector4 border, bool repeat = false)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteBorder != border || ti.mipmapEnabled
                || (repeat && ti.wrapMode != TextureWrapMode.Repeat);
            if (!changed) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.spriteBorder = border;
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        static Sprite Ui(string name)
        {
            string path = $"{UiArt}/{name}.png";
            ImportSprite(path, Borders.TryGetValue(name, out var b) ? b : Vector4.zero, repeat: name == "bg_pattern");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite Kit(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{KitUi}/{name}.png");

        static TMP_FontAsset _font;
        static Material _outline, _soft;

        static void PrepareText()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            string dir = Art + "/Fonts";
            Directory.CreateDirectory(dir);
            _outline = TextMaterial($"{dir}/Hub Outline.mat", outline: 0.22f, underlay: 0.55f);
            _soft = TextMaterial($"{dir}/Hub Soft.mat", outline: 0f, underlay: 0f);
        }

        static Material TextMaterial(string path, float outline, float underlay)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(_font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = _font.material.shader;
            mat.SetTexture("_MainTex", _font.material.GetTexture("_MainTex"));
            mat.SetFloat("_OutlineWidth", outline);
            mat.SetColor("_OutlineColor", new Color(0.13f, 0.07f, 0.32f, 1f));
            mat.SetFloat("_FaceDilate", outline > 0 ? 0.12f : 0.05f);
            if (underlay > 0)
            {
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", new Color(0.08f, 0.03f, 0.25f, underlay));
                mat.SetFloat("_UnderlayOffsetY", -0.9f);
                mat.SetFloat("_UnderlaySoftness", 0.15f);
                mat.SetFloat("_UnderlayDilate", 0.2f);
            }
            else mat.DisableKeyword("UNDERLAY_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ UI building blocks

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        static TMP_Text Txt(string name, Transform parent, string text, float size, Color color, bool outline = true,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font;
            t.fontSharedMaterial = outline ? _outline : _soft;
            t.text = text;
            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.5f;
            t.fontSizeMax = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary> A chunky button: darker base under a coloured face, gloss on top, squashes when pressed. </summary>
        static Button Button3D(string name, Transform parent, Vector2 size, Color face, Sprite shape, float depth = 12f)
        {
            var root = Rect(name, parent);
            root.sizeDelta = size;
            var shadow = Img("Shadow", root, Ui("shadow"), new Color(0, 0, 0, 0.28f));
            Stretch(shadow.rectTransform, -22, -22 - depth, -22, -10);
            var baseImg = Img("Base", root, shape, CardShade(face, 0.66f));
            Stretch(baseImg.rectTransform, 0, -depth, 0, depth);
            var faceImg = Img("Face", root, shape, face, raycast: true);
            Stretch(faceImg.rectTransform);
            var gloss = Img("Gloss", faceImg.transform, shape, new Color(1, 1, 1, 0.22f));
            gloss.rectTransform.anchorMin = new Vector2(0, 0.52f); gloss.rectTransform.anchorMax = Vector2.one;
            float inset = Mathf.Min(size.x, size.y) * 0.07f;
            gloss.rectTransform.offsetMin = new Vector2(inset, 0); gloss.rectTransform.offsetMax = new Vector2(-inset, -inset * 0.6f);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.disabledColor = new Color(0.62f, 0.62f, 0.62f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            btn.colors = colors;
            root.gameObject.AddComponent<UiPress>();
            return btn;
        }

        static Color CardShade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}
