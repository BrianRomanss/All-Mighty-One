using AllMightyOne.Hub;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    public static partial class HubSetup
    {
        static readonly Color Teal = new(0.1f, 0.72f, 0.66f);

        // ------------------------------------------------------------------ the two round buttons beside the logo

        static void BuildDailyButtons(HubController hub, RectTransform content)
        {
            hub.DailyBadge = SideButton(content, "Daily Button", "DAILY", Orange, Ui("ic_gift"), 135f, hub, nameof(HubController.OpenDaily));
            hub.MissionsBadge = SideButton(content, "Missions Button", "MISSIONS", Teal, Ui("ic_check"), -135f, hub, nameof(HubController.OpenMissions));
        }

        /// <summary> A round button left or right of the logo with a caption and a red dot; returns the dot. </summary>
        static GameObject SideButton(RectTransform content, string name, string caption, Color color, Sprite icon, float x, HubController hub, string method)
        {
            var anchor = new Vector2(x > 0 ? 0 : 1, 1);
            var holder = Rect(name + " Holder", content);
            Place(holder, anchor, new Vector2(x, -270), new Vector2(170, 210));
            holder.gameObject.AddComponent<UiFloat>().Height = 6f;
            var b = Button3D(name, holder, new Vector2(140, 140), color, Ui("circle"), 12);
            Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(140, 140), new Vector2(0.5f, 1));
            Icon(b, icon, 0.58f);
            AddListener(b.onClick, hub, method);
            var label = Txt("Caption", holder, caption, 34, Color.white);
            Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(200, 48), new Vector2(0.5f, 0));
            var dot = Img("Badge", b.transform, Ui("circle"), Red);
            Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(46, 46));
            var mark = Txt("Mark", dot.transform, "!", 34, Color.white);
            Stretch(mark.rectTransform);
            dot.gameObject.AddComponent<UiFloat>().Height = 4f;
            return dot.gameObject;
        }

        // ------------------------------------------------------------------ daily reward popup

        static void BuildDailyPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "Daily Popup", "DAILY REWARD", Orange, 1060, hub);
            hub.DailyPopup = popup;
            var sub = Txt("Subtitle", w, "Come back every day for bigger rewards!", 38, new Color(Ink.r, Ink.g, Ink.b, 0.8f), outline: false);
            Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 60), new Vector2(0.5f, 1));

            const int n = 7;
            hub.DailyTiles = new Image[n];
            hub.DailyAmounts = new TMP_Text[n];
            hub.DailyChecks = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                // four small days on top, then two small and the big day 7
                bool big = i == 6;
                int row = i < 4 ? 0 : 1, col = i < 4 ? i : i - 4;
                var size = new Vector2(big ? 390 : 185, 250);
                float x = row == 0 ? -307.5f + col * 205 : (big ? 307.5f - 102.5f : -307.5f + col * 205);
                var tile = Img($"Day {i + 1}", w, Ui("rr24"), Color.white, raycast: false);
                Place(tile.rectTransform, new Vector2(0.5f, 1), new Vector2(x, -200 - row * 275), size, new Vector2(0.5f, 1));
                var day = Txt("Day", tile.transform, $"DAY {i + 1}", 34, Color.white);
                Place(day.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(size.x - 20, 46), new Vector2(0.5f, 1));
                var gem = Img("Gem", tile.transform, Kit("Gem"), Color.white);
                gem.preserveAspect = true;
                Place(gem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), big ? new Vector2(150, 120) : new Vector2(96, 84));
                var amount = Txt("Amount", tile.transform, "20", big ? 56 : 46, Color.white);
                Place(amount.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(size.x - 20, 60), new Vector2(0.5f, 0));
                // claimed days: a big tick over the gem
                var check = Img("Check", tile.transform, Ui("circle"), Green);
                Place(check.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(84, 84));
                var tick = Img("Tick", check.transform, Ui("ic_check"), Color.white);
                tick.preserveAspect = true;
                Place(tick.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
                hub.DailyTiles[i] = tile;
                hub.DailyAmounts[i] = amount;
                hub.DailyChecks[i] = check.gameObject;
            }

            hub.DailyClaimButton = RewardButton(w, "Daily Claim Button", Green, null, new Vector2(-205, 90));
            hub.DailyDoubleButton = RewardButton(w, "Daily Double Button", Orange, Ui("ic_tv"), new Vector2(205, 90));
            var x2 = Img("x2 Badge", hub.DailyDoubleButton.transform, Ui("pill"), Red);
            Place(x2.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 26), new Vector2(110, 56));
            Stretch(Txt("Label", x2.transform, "x2", 36, Color.white).rectTransform);

            hub.DailyNextText = Txt("Next", w, "Come back tomorrow!", 44, Ink, outline: false);
            hub.DailyNextText.textWrappingMode = TextWrappingModes.Normal;
            Place(hub.DailyNextText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 80), new Vector2(820, 140), new Vector2(0.5f, 0));
        }

        /// <summary> A pill button with an optional icon, a gem and an amount (its only text). </summary>
        static Button RewardButton(Transform parent, string name, Color color, Sprite icon, Vector2 pos)
        {
            var b = Button3D(name, parent, new Vector2(380, 140), color, Ui("pill"), 12);
            Place((RectTransform)b.transform, new Vector2(0.5f, 0), pos, new Vector2(380, 140), new Vector2(0.5f, 0));
            var face = b.transform.Find("Face");
            float left = 40;
            if (icon)
            {
                var ic = Img("Icon", face, icon, Color.white);
                ic.preserveAspect = true;
                Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(34, 4), new Vector2(70, 70), new Vector2(0, 0.5f));
                left = 110;
            }
            var gem = Img("Gem", face, Kit("Gem"), Color.white);
            gem.preserveAspect = true;
            Place(gem.rectTransform, new Vector2(0, 0.5f), new Vector2(left, 4), new Vector2(80, 70), new Vector2(0, 0.5f));
            var amount = Txt("Amount", face, "+20", 56, Color.white, align: TextAlignmentOptions.Left);
            Place(amount.rectTransform, new Vector2(0, 0.5f), new Vector2(left + 90, 4), new Vector2(380 - left - 110, 100), new Vector2(0, 0.5f));
            return b;
        }

        // ------------------------------------------------------------------ missions popup

        static void BuildMissionsPopup(HubController hub, Transform root)
        {
            var (popup, w) = Popup(root, "Missions Popup", "DAILY MISSIONS", Teal, 1100, hub);
            hub.MissionsPopup = popup;
            hub.MissionRows = new HubMissionRow[DailyMissions.Count];
            for (int i = 0; i < DailyMissions.Count; i++)
            {
                var panel = Img($"Mission {i + 1}", w, Ui("rr24"), new Color(0.93f, 0.92f, 1f));
                Place(panel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130 - i * 260), new Vector2(840, 230), new Vector2(0.5f, 1));
                var row = panel.gameObject.AddComponent<HubMissionRow>();

                row.Text = Txt("Text", panel.transform, "Play 3 different games", 40, Ink, outline: false, align: TextAlignmentOptions.Left);
                row.Text.textWrappingMode = TextWrappingModes.Normal;
                Place(row.Text.rectTransform, new Vector2(0, 1), new Vector2(34, -22), new Vector2(540, 110), new Vector2(0, 1));

                var bar = Img("Bar", panel.transform, Ui("pill"), new Color(0.78f, 0.76f, 0.9f));
                Place(bar.rectTransform, new Vector2(0, 0), new Vector2(34, 34), new Vector2(520, 56), new Vector2(0, 0));
                var fill = Img("Fill", bar.transform, Ui("pill"), Green);
                fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                row.ProgressFill = fill;
                row.ProgressText = Txt("Progress", bar.transform, "0/3", 34, Color.white);
                Stretch(row.ProgressText.rectTransform);

                var claim = Button3D("Claim Button", panel.transform, new Vector2(220, 120), Green, Ui("pill"), 10);
                Place((RectTransform)claim.transform, new Vector2(1, 0.5f), new Vector2(-26, 4), new Vector2(220, 120), new Vector2(1, 0.5f));
                var face = claim.transform.Find("Face");
                row.ClaimFace = face.GetComponent<Image>();
                var gem = Img("Gem", face, Kit("Gem"), Color.white);
                gem.preserveAspect = true;
                Place(gem.rectTransform, new Vector2(0, 0.5f), new Vector2(22, 4), new Vector2(70, 62), new Vector2(0, 0.5f));
                row.RewardText = Txt("Reward", face, "30", 50, Color.white);
                Place(row.RewardText.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 4), new Vector2(110, 90), new Vector2(1, 0.5f));
                row.ClaimButton = claim;
                // a disabled button keeps its colour: the row tints it grey while the mission is not finished
                var colors = claim.colors;
                colors.disabledColor = Color.white;
                claim.colors = colors;

                var done = Img("Done", panel.transform, Ui("circle"), Green);
                Place(done.rectTransform, new Vector2(1, 0.5f), new Vector2(-82, 4), new Vector2(110, 110), new Vector2(1, 0.5f));
                var tick = Img("Tick", done.transform, Ui("ic_check"), Color.white);
                tick.preserveAspect = true;
                Place(tick.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64));
                row.DoneMark = done.gameObject;
                hub.MissionRows[i] = row;
            }
            hub.MissionsTimer = Txt("Timer", w, "New missions in 00:00:00", 38, new Color(Ink.r, Ink.g, Ink.b, 0.75f), outline: false);
            Place(hub.MissionsTimer.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(820, 60), new Vector2(0.5f, 0));
        }
    }
}
