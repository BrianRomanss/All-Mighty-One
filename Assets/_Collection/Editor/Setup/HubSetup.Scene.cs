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
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    public static partial class HubSetup
    {
        [MenuItem("All Mighty One/Hub/Build Hub And Home Buttons")]
        public static void BuildAll()
        {
            AddHomeButtons();
            PolishGames();
            Run();
        }

        [MenuItem("All Mighty One/Hub/Build Hub Scene")]
        public static void Run()
        {
            PrepareText();
            var catalog = BuildCatalog();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.27f, 0.25f, 0.8f);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            AddMusic();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // TMP's SDF shader reads scale data from these channels: without them every label renders as a solid block
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f; // portrait: width is fixed, taller phones get a longer game list
            var root = (RectTransform)canvasGo.transform;

            // ---- background
            // generated art (ArtPrompts -> cutout.py) when present, else the drawn gradient with the shape pattern
            string bgArt = $"{Art}/Background.png";
            if (File.Exists(bgArt))
            {
                ImportSprite(bgArt, Vector4.zero);
                var bg = Img("Background", root, AssetDatabase.LoadAssetAtPath<Sprite>(bgArt), Color.white);
                Stretch(bg.rectTransform);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bg.sprite.rect.width / bg.sprite.rect.height;
            }
            else
            {
                Stretch(Img("Background", root, Ui("bg_gradient"), Color.white).rectTransform);
                var pattern = Img("Pattern", root, Ui("bg_pattern"), new Color(1, 1, 1, 0.9f));
                pattern.type = Image.Type.Tiled;
                pattern.pixelsPerUnitMultiplier = 0.9f;
                Stretch(pattern.rectTransform);
            }

            var safe = Stretch(Rect("Safe Area", root));
            safe.gameObject.AddComponent<SafeArea>();
            var content = Stretch(Rect("Content", safe));

            var hub = new GameObject("Hub").AddComponent<HubController>();
            hub.Catalog = catalog;
            hub.Audio = AssetDatabase.LoadAssetAtPath<MOST_AudioManager>(AudioPath);
            hub.Content = content;

            BuildTopBar(hub, content);
            BuildLogo(content);
            BuildFeatured(hub, content, catalog);
            BuildTabs(hub, content);
            BuildGrid(hub, content);

            // banner strip (hidden after Remove Ads)
            var banner = Img("Banner Space", safe, null, new Color(0.08f, 0.05f, 0.25f, 0.35f));
            Place(banner.rectTransform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 150), new Vector2(0.5f, 0));
            banner.rectTransform.anchorMin = new Vector2(0, 0); banner.rectTransform.anchorMax = new Vector2(1, 0);
            hub.BannerSpace = banner.rectTransform;
            content.offsetMin = new Vector2(0, 150);

            BuildUnlockPopup(hub, root);
            BuildFreeGemsPopup(hub, root);
            BuildSettingsPopup(hub, root);
            BuildNoAdsPopup(hub, root);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
            Debug.Log($"[AMO] Hub built: {catalog.Games.Length} games, {catalog.Games.Count(g => g.Icon)} with art");
        }

        // ------------------------------------------------------------------ top bar

        static void BuildTopBar(HubController hub, RectTransform content)
        {
            var bar = Rect("Top Bar", content);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(0.5f, 1);
            bar.anchoredPosition = new Vector2(0, -24); bar.sizeDelta = new Vector2(0, 130);

            // gem wallet: dark pill, big gem on the left, + button on the right
            var wallet = Rect("Wallet", bar);
            Place(wallet, new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(330, 96), new Vector2(0, 0.5f));
            Stretch(Img("Pill", wallet, Ui("pill"), new Color(0.1f, 0.05f, 0.3f, 0.45f)).rectTransform);
            var gem = Img("Gem", wallet, Kit("Gem"), Color.white);
            Place(gem.rectTransform, new Vector2(0, 0.5f), new Vector2(-6, 2), new Vector2(110, 96));
            gem.preserveAspect = true;
            hub.GemIcon = gem.rectTransform;
            var gemText = Txt("Gems", wallet, "0", 52, Color.white);
            Stretch(gemText.rectTransform, 62, 0, 84, 0);
            hub.GemText = gemText;
            var plus = Button3D("Get Gems", wallet, new Vector2(84, 84), Green, Ui("circle"), 8);
            Place((RectTransform)plus.transform, new Vector2(1, 0.5f), new Vector2(-8, 4), new Vector2(84, 84), new Vector2(1, 0.5f));
            Icon(plus, Ui("ic_plus"), 0.5f);
            AddListener(plus.onClick, hub, nameof(HubController.OpenFreeGems));

            var settings = Button3D("Settings Button", bar, new Vector2(112, 112), Violet, Ui("circle"), 10);
            Place((RectTransform)settings.transform, new Vector2(1, 0.5f), new Vector2(-60, 6), new Vector2(112, 112), new Vector2(1, 0.5f));
            Icon(settings, Kit("Settings_Small"), 0.56f);
            AddListener(settings.onClick, hub, nameof(HubController.OpenSettings));

            var noAds = Button3D("No Ads Button", bar, new Vector2(112, 112), Red, Ui("circle"), 10);
            Place((RectTransform)noAds.transform, new Vector2(1, 0.5f), new Vector2(-196, 6), new Vector2(112, 112), new Vector2(1, 0.5f));
            Icon(noAds, Ui("ic_noads"), 0.62f);
            AddListener(noAds.onClick, hub, nameof(HubController.OpenNoAds));
            hub.NoAdsButton = noAds.gameObject;
        }

        static Image Icon(Button b, Sprite sprite, float scale)
        {
            var face = b.transform.Find("Face");
            var img = Img("Icon", face, sprite, Color.white);
            img.preserveAspect = true;
            var size = ((RectTransform)b.transform).sizeDelta;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), size * scale);
            return img;
        }

        // ------------------------------------------------------------------ logo

        static void BuildLogo(RectTransform content)
        {
            var logo = Rect("Logo", content);
            Place(logo, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(1000, 250));
            logo.gameObject.AddComponent<UiFloat>().Height = 8f;
            if (File.Exists($"{Art}/Logo.png")) ImportSprite($"{Art}/Logo.png", Vector4.zero);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/Logo.png");
            if (art)
            {
                var img = Img("Logo Art", logo, art, Color.white);
                Stretch(img.rectTransform);
                img.preserveAspect = true;
                return;
            }
            var top = Txt("Line 1", logo, "ALL MIGHTY", 116, Color.white);
            Place(top.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(1000, 130), new Vector2(0.5f, 1));
            top.characterSpacing = 4;
            var one = Txt("Line 2", logo, "ONE", 150, Color.white);
            Place(one.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -6), new Vector2(600, 150), new Vector2(0.5f, 0));
            one.enableVertexGradient = true;
            one.colorGradient = new VertexGradient(new Color(1f, 0.93f, 0.35f), new Color(1f, 0.93f, 0.35f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.55f, 0.1f));
            one.characterSpacing = 10;
        }

        // ------------------------------------------------------------------ continue card

        static void BuildFeatured(HubController hub, RectTransform content, GameCatalog catalog)
        {
            var label = Txt("Featured Label", content, "CONTINUE PLAYING", 40, new Color(1, 1, 1, 0.92f));
            Place(label.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(900, 50));
            hub.FeaturedLabel = label;

            var root = Rect("Featured", content);
            Place(root, new Vector2(0.5f, 1), new Vector2(0, -505), new Vector2(980, 300), new Vector2(0.5f, 1));
            var card = Card(root, featured: true);
            hub.Featured = card;
        }

        // ------------------------------------------------------------------ tabs

        static void BuildTabs(HubController hub, RectTransform content)
        {
            var row = Rect("Tabs", content);
            Place(row, new Vector2(0.5f, 1), new Vector2(0, -860), new Vector2(980, 92), new Vector2(0.5f, 1));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = layout.childControlHeight = true;
            var names = new[] { "ALL", "RUNNER", "ARCADE", "PUZZLE" };
            hub.Tabs = new Button[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var tab = Img(names[i], row, Ui("pill"), new Color(1, 1, 1, 0.18f), raycast: true);
                var btn = tab.gameObject.AddComponent<Button>();
                btn.targetGraphic = tab;
                btn.transition = Selectable.Transition.None;
                tab.gameObject.AddComponent<UiPress>();
                var t = Txt("Label", tab.transform, names[i], 38, Color.white, outline: false);
                Stretch(t.rectTransform, 10, 0, 10, 0);
                hub.Tabs[i] = btn;
            }
        }

        // ------------------------------------------------------------------ grid

        static void BuildGrid(HubController hub, RectTransform content)
        {
            var scrollRt = Rect("Games", content);
            Stretch(scrollRt, 0, 0, 0, 975);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;
            var viewport = Stretch(Rect("Viewport", scrollRt));
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 40);
            var raycastCatcher = viewport.gameObject.AddComponent<Image>();
            raycastCatcher.color = new Color(0, 0, 0, 0); // drags anywhere in the list scroll it
            var cards = Rect("Cards", viewport);
            cards.anchorMin = new Vector2(0, 1); cards.anchorMax = Vector2.one; cards.pivot = new Vector2(0.5f, 1);
            cards.sizeDelta = Vector2.zero;
            var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(470, 500);
            grid.spacing = new Vector2(40, 56);
            grid.padding = new RectOffset(40, 40, 36, 80);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            cards.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = cards;
            hub.Scroll = scroll;
            hub.CardsRoot = cards;

            var template = Rect("Card Template", cards);
            template.sizeDelta = grid.cellSize;
            hub.CardTemplate = Card(template, featured: false);
        }

        /// <summary> A game card. Grid card: art on top, title and progress below. Featured: art left, text and PLAY right. </summary>
        static HubCard Card(RectTransform root, bool featured)
        {
            var card = root.gameObject.AddComponent<HubCard>();
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<UiPopIn>();
            const float depth = 16f;

            Stretch(Img("Shadow", root, Ui("shadow"), new Color(0.05f, 0, 0.2f, 0.45f)).rectTransform, -34, -34 - depth, -34, -14);
            card.Base = Img("Base", root, Ui("rr48"), Color.gray);
            Stretch(card.Base.rectTransform, 0, -depth, 0, depth);
            card.Face = Img("Face", root, Ui("rr48"), Color.white, raycast: true);
            Stretch(card.Face.rectTransform);
            var face = card.Face.transform;
            var gloss = Img("Gloss", face, Ui("gloss"), Color.white);
            gloss.rectTransform.anchorMin = new Vector2(0, 0.45f); gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(10, 0); gloss.rectTransform.offsetMax = new Vector2(-10, -10);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = card.Face;
            button.transition = Selectable.Transition.None;
            root.gameObject.AddComponent<UiPress>().PressedScale = 0.95f;
            card.Button = button;

            // art frame: rounded white plate with the icon masked inside
            var frame = Img("Art Frame", face, Ui("rr48"), new Color(1, 1, 1, 0.95f));
            if (featured) Place(frame.rectTransform, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(248, 248), new Vector2(0, 0.5f));
            else Place(frame.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(434, 290), new Vector2(0.5f, 1));
            if (featured)
            {
                var rays = Img("Rays", face, Ui("rays"), new Color(1, 1, 1, 0.55f));
                Place(rays.rectTransform, new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(520, 520));
                rays.gameObject.AddComponent<UiSpin>();
                rays.transform.SetSiblingIndex(frame.transform.GetSiblingIndex());
            }
            var mask = Img("Mask", frame.transform, Ui("rr48"), Color.white);
            Stretch(mask.rectTransform, 8, 8, 8, 8);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            card.Icon = Img("Icon", mask.transform, null, Color.white);
            Stretch(card.Icon.rectTransform);
            var fit = card.Icon.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1;
            card.Initials = Txt("Initials", frame.transform, "AB", featured ? 130 : 150, Color.white);
            Stretch(card.Initials.rectTransform);

            if (featured)
            {
                card.Title = Txt("Title", face, "GAME", 60, Color.white, align: TextAlignmentOptions.Left);
                Place(card.Title.rectTransform, new Vector2(0, 1), new Vector2(300, -34), new Vector2(640, 80), new Vector2(0, 1));
                card.Tagline = Txt("Tagline", face, "Tagline", 34, new Color(1, 1, 1, 0.92f), outline: false, align: TextAlignmentOptions.Left);
                Place(card.Tagline.rectTransform, new Vector2(0, 1), new Vector2(300, -112), new Vector2(640, 50), new Vector2(0, 1));
                card.ProgressPill = Img("Progress Pill", face, Ui("pill"), Color.gray);
                Place(card.ProgressPill.rectTransform, new Vector2(0, 0), new Vector2(300, 34), new Vector2(270, 72), new Vector2(0, 0));
                card.Progress = Txt("Progress", card.ProgressPill.transform, "LEVEL 1", 36, Color.white, outline: false);
                Stretch(card.Progress.rectTransform, 16, 0, 16, 0);
                var play = Button3D("Play", face, new Vector2(270, 112), Green, Ui("pill"), 10);
                Place((RectTransform)play.transform, new Vector2(1, 0), new Vector2(-32, 34), new Vector2(270, 112), new Vector2(1, 0));
                // the whole card is the button: this is only the visual call to action
                var playFace = play.transform.Find("Face");
                Object.DestroyImmediate(play.GetComponent<UiPress>());
                Object.DestroyImmediate(play);
                var pi = Img("Icon", playFace, Ui("ic_play"), Color.white);
                Place(pi.rectTransform, new Vector2(0, 0.5f), new Vector2(42, 3), new Vector2(54, 54), new Vector2(0, 0.5f));
                var pt = Txt("Label", playFace, "PLAY", 52, Color.white);
                Stretch(pt.rectTransform, 92, 0, 18, 0);
                return card;
            }

            card.Title = Txt("Title", face, "GAME", 46, Color.white);
            Place(card.Title.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(440, 64));
            card.ProgressPill = Img("Progress Pill", face, Ui("pill"), Color.gray);
            Place(card.ProgressPill.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(280, 62));
            card.Progress = Txt("Progress", card.ProgressPill.transform, "LEVEL 1", 32, Color.white, outline: false);
            Stretch(card.Progress.rectTransform, 14, 0, 14, 0);

            // locked: dark veil, padlock and the gem price
            var lockRoot = Img("Lock", root, Ui("rr48"), new Color(0.06f, 0.02f, 0.2f, 0.62f));
            Stretch(lockRoot.rectTransform);
            var padlock = Img("Padlock", lockRoot.transform, Kit("Locked"), Color.white);
            padlock.preserveAspect = true;
            Place(padlock.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(120, 150));
            var cost = Img("Cost", lockRoot.transform, Ui("pill"), new Color(0.12f, 0.06f, 0.36f, 0.95f));
            Place(cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(230, 80));
            var cgem = Img("Gem", cost.transform, Kit("Gem"), Color.white);
            cgem.preserveAspect = true;
            Place(cgem.rectTransform, new Vector2(0, 0.5f), new Vector2(44, 2), new Vector2(70, 62));
            card.CostText = Txt("Price", cost.transform, "100", 44, Color.white);
            Stretch(card.CostText.rectTransform, 84, 0, 18, 0);
            card.LockRoot = lockRoot.gameObject;

            var badge = Img("New Badge", root, Ui("pill"), Red);
            Place(badge.rectTransform, new Vector2(1, 1), new Vector2(18, 18), new Vector2(150, 66), new Vector2(1, 1));
            badge.rectTransform.localEulerAngles = new Vector3(0, 0, -12);
            var nt = Txt("Label", badge.transform, "NEW", 36, Color.white);
            Stretch(nt.rectTransform, 10, 0, 10, 0);
            badge.gameObject.AddComponent<UiFloat>().Height = 6;
            card.NewBadge = badge.gameObject;
            return card;
        }

        // ------------------------------------------------------------------ popups

        /// <summary> Full-screen dim + white window with a coloured title ribbon and a close button. </summary>
        static (HubPopup popup, RectTransform window) Popup(Transform root, string name, string title, Color theme, float height, HubController hub)
        {
            var rt = Stretch(Rect(name, root));
            rt.gameObject.AddComponent<CanvasGroup>();
            var popup = rt.gameObject.AddComponent<HubPopup>();
            var dim = Img("Dim", rt, null, new Color(0.04f, 0.02f, 0.15f, 0.7f), raycast: true);
            Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(dimButton.onClick, popup.Close);

            var window = Rect("Window", rt);
            Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, height));
            Stretch(Img("Shadow", window, Ui("shadow"), new Color(0, 0, 0, 0.45f)).rectTransform, -40, -60, -40, -20);
            Stretch(Img("Base", window, Ui("rr48"), new Color(0.78f, 0.76f, 0.9f)).rectTransform, 0, -14, 0, 14);
            Stretch(Img("Panel", window, Ui("rr48"), Color.white, raycast: true).rectTransform);
            var ribbon = Img("Ribbon", window, Ui("pill"), theme);
            Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 6), new Vector2(640, 124));
            var rbase = Img("Ribbon Base", window, Ui("pill"), CardShade(theme, 0.66f));
            Place(rbase.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(640, 124));
            rbase.transform.SetSiblingIndex(ribbon.transform.GetSiblingIndex());
            var t = Txt("Title", ribbon.transform, title, 58, Color.white);
            Stretch(t.rectTransform, 30, 0, 30, 6);

            var close = Button3D("Close", window, new Vector2(104, 104), Red, Ui("circle"), 8);
            Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(18, 18), new Vector2(104, 104));
            Icon(close, Ui("ic_close"), 0.5f);
            UnityEventTools.AddPersistentListener(close.onClick, popup.Close);

            popup.Window = window;
            rt.gameObject.SetActive(false);
            return (popup, window);
        }

        static void BuildUnlockPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "Unlock Popup", "NEW GAME", Violet, 1000, hub);
            hub.UnlockPopup = popup;
            hub.UnlockHeader = w.Find("Ribbon").GetComponent<Image>();
            var frame = Img("Art Frame", w, Ui("rr48"), new Color(0.93f, 0.92f, 1f));
            Place(frame.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(340, 340), new Vector2(0.5f, 1));
            var mask = Img("Mask", frame.transform, Ui("rr48"), Color.white);
            Stretch(mask.rectTransform, 10, 10, 10, 10);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            hub.UnlockIcon = Img("Icon", mask.transform, null, Color.white);
            Stretch(hub.UnlockIcon.rectTransform);
            hub.UnlockIcon.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            hub.UnlockInitials = Txt("Initials", frame.transform, "A", 200, Color.white);
            Stretch(hub.UnlockInitials.rectTransform);
            hub.UnlockTitle = Txt("Game", w, "GAME", 64, Ink, outline: false);
            Place(hub.UnlockTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -510), new Vector2(820, 80), new Vector2(0.5f, 1));
            hub.UnlockMessage = Txt("Message", w, "", 38, new Color(Ink.r, Ink.g, Ink.b, 0.75f), outline: false);
            Place(hub.UnlockMessage.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -600), new Vector2(820, 60), new Vector2(0.5f, 1));

            var unlock = Button3D("Unlock Button", w, new Vector2(600, 140), Green, Ui("pill"), 12);
            Place((RectTransform)unlock.transform, new Vector2(0.5f, 0), new Vector2(0, 220), new Vector2(600, 140));
            var face = unlock.transform.Find("Face");
            var label = Txt("Label", face, "UNLOCK", 54, Color.white);
            Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 4), new Vector2(300, 100), new Vector2(0, 0.5f));
            var gem = Img("Gem", face, Kit("Gem"), Color.white);
            gem.preserveAspect = true;
            Place(gem.rectTransform, new Vector2(1, 0.5f), new Vector2(-205, 4), new Vector2(78, 68));
            hub.UnlockCost = Txt("Cost", face, "100", 54, Color.white, align: TextAlignmentOptions.Left);
            Place(hub.UnlockCost.rectTransform, new Vector2(1, 0.5f), new Vector2(-28, 4), new Vector2(130, 100), new Vector2(1, 0.5f));
            hub.UnlockButton = unlock;

            var getGems = Button3D("Get Gems Button", w, new Vector2(600, 120), Orange, Ui("pill"), 10);
            Place((RectTransform)getGems.transform, new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(600, 120));
            var gf = getGems.transform.Find("Face");
            var tv = Img("Icon", gf, Ui("ic_tv"), Color.white);
            Place(tv.rectTransform, new Vector2(0, 0.5f), new Vector2(46, 4), new Vector2(70, 70), new Vector2(0, 0.5f));
            var gl = Txt("Label", gf, "FREE GEMS", 46, Color.white);
            Stretch(gl.rectTransform, 120, 0, 30, 0);
            hub.UnlockGetGemsButton = getGems;
        }

        static void BuildFreeGemsPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "Free Gems Popup", "FREE GEMS", Orange, 820, hub);
            hub.FreeGemsPopup = popup;
            var rays = Img("Rays", w, Ui("rays"), new Color(1f, 0.8f, 0.3f, 0.9f));
            Place(rays.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(520, 520));
            rays.gameObject.AddComponent<UiSpin>();
            var gem = Img("Gem", w, Kit("Gem"), Color.white);
            gem.preserveAspect = true;
            Place(gem.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(260, 230));
            gem.gameObject.AddComponent<UiFloat>();
            var msg = Txt("Message", w, "Watch a short video\nfor free gems!", 46, Ink, outline: false);
            msg.textWrappingMode = TextWrappingModes.Normal;
            Place(msg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(800, 130), new Vector2(0.5f, 1));
            var watch = Button3D("Watch Button", w, new Vector2(560, 140), Green, Ui("pill"), 12);
            Place((RectTransform)watch.transform, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(560, 140));
            var wf = watch.transform.Find("Face");
            var tv = Img("Icon", wf, Ui("ic_tv"), Color.white);
            Place(tv.rectTransform, new Vector2(0, 0.5f), new Vector2(50, 4), new Vector2(80, 80), new Vector2(0, 0.5f));
            hub.FreeGemsTimer = Txt("Amount", wf, "+25", 60, Color.white);
            Stretch(hub.FreeGemsTimer.rectTransform, 150, 0, 40, 0);
            hub.FreeGemsWatchButton = watch;
        }

        static void BuildSettingsPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "Settings Popup", "SETTINGS", Violet, 900, hub);
            hub.SettingsPopup = popup;
            hub.MusicSwitch = SettingRow(w, "MUSIC", Kit("Music"), -160);
            hub.SoundSwitch = SettingRow(w, "SOUND", Kit("Sound_On"), -310);
            hub.VibrationSwitch = SettingRow(w, "VIBRATION", Kit("Vibro_On"), -460);

            var restore = Button3D("Restore Button", w, new Vector2(370, 110), new Color(0.25f, 0.6f, 1f), Ui("pill"), 10);
            Place((RectTransform)restore.transform, new Vector2(0.5f, 0), new Vector2(-200, 120), new Vector2(370, 110));
            Stretch(Txt("Label", restore.transform.Find("Face"), "RESTORE", 40, Color.white).rectTransform, 20, 0, 20, 4);
            AddListener(restore.onClick, hub, nameof(HubController.RestorePurchases));
            var privacy = Button3D("Privacy Button", w, new Vector2(370, 110), new Color(0.25f, 0.6f, 1f), Ui("pill"), 10);
            Place((RectTransform)privacy.transform, new Vector2(0.5f, 0), new Vector2(200, 120), new Vector2(370, 110));
            Stretch(Txt("Label", privacy.transform.Find("Face"), "PRIVACY", 40, Color.white).rectTransform, 20, 0, 20, 4);
            AddListener(privacy.onClick, hub, nameof(HubController.OpenPrivacy));
            hub.VersionText = Txt("Version", w, "v1.0", 30, new Color(Ink.r, Ink.g, Ink.b, 0.45f), outline: false);
            Place(hub.VersionText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(400, 40));
        }

        static HubSwitch SettingRow(RectTransform window, string label, Sprite icon, float y)
        {
            var row = Rect(label + " Row", window);
            Place(row, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(800, 120), new Vector2(0.5f, 1));
            Stretch(Img("Background", row, Ui("pill"), new Color(0.93f, 0.92f, 1f)).rectTransform);
            var badge = Img("Badge", row, Ui("circle"), Violet);
            Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(92, 92), new Vector2(0, 0.5f));
            var ic = Img("Icon", badge.transform, icon, Color.white);
            ic.preserveAspect = true;
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54));
            var t = Txt("Label", row, label, 44, Ink, outline: false, align: TextAlignmentOptions.Left);
            Place(t.rectTransform, new Vector2(0, 0.5f), new Vector2(136, 2), new Vector2(400, 80), new Vector2(0, 0.5f));

            var sw = Rect("Switch", row);
            Place(sw, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(190, 92), new Vector2(1, 0.5f));
            var track = Img("Track", sw, Ui("pill"), Green, raycast: true);
            Stretch(track.rectTransform);
            var knob = Img("Knob", sw, Ui("circle"), Color.white);
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(48, 0), new Vector2(76, 76));
            var s = sw.gameObject.AddComponent<HubSwitch>();
            s.Track = track;
            s.Knob = knob.rectTransform;
            s.OnColor = Green;
            s.Travel = 48;
            return s;
        }

        static void BuildNoAdsPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "No Ads Popup", "NO ADS", Red, 860, hub);
            hub.NoAdsPopup = popup;
            var rays = Img("Rays", w, Ui("rays"), new Color(1f, 0.45f, 0.5f, 0.8f));
            Place(rays.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(480, 480));
            rays.gameObject.AddComponent<UiSpin>();
            var badge = Img("Badge", w, Ui("circle"), Red);
            Place(badge.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(220, 220));
            var ic = Img("Icon", badge.transform, Ui("ic_noads"), Color.white);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));
            var msg = Txt("Message", w, "Play without interruptions!\n<size=80%>Bonus videos stay optional.</size>", 44, Ink, outline: false);
            msg.textWrappingMode = TextWrappingModes.Normal;
            Place(msg.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -440), new Vector2(820, 140), new Vector2(0.5f, 1));
            var buy = Button3D("Buy Button", w, new Vector2(600, 140), Green, Ui("pill"), 12);
            Place((RectTransform)buy.transform, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(600, 140));
            var bf = buy.transform.Find("Face");
            var bl = Txt("Label", bf, "REMOVE ADS", 46, Color.white, align: TextAlignmentOptions.Left);
            Place(bl.rectTransform, new Vector2(0, 0.5f), new Vector2(40, 4), new Vector2(330, 100), new Vector2(0, 0.5f));
            hub.NoAdsPrice = Txt("Price", bf, "$2.99", 50, new Color(1f, 0.95f, 0.5f));
            Place(hub.NoAdsPrice.rectTransform, new Vector2(1, 0.5f), new Vector2(-34, 4), new Vector2(190, 100), new Vector2(1, 0.5f));
            AddListener(buy.onClick, hub, nameof(HubController.BuyNoAds));
        }

        // ------------------------------------------------------------------ build settings

        static void SetBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            foreach (var path in ScenePaths.Values)
                if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
