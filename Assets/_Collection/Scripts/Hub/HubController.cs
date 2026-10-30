using System;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary>
    /// The collection's home screen: game grid with category tabs, a "continue" card for the last game played, the shared
    /// gem wallet, unlocking games with gems, free gems for a rewarded ad, settings, daily reward and missions.
    /// </summary>
    public class HubController : MonoBehaviour
    {
        public GameCatalog Catalog;

        [Header("Grid")]
        public HubCard CardTemplate;
        public RectTransform CardsRoot;
        public ScrollRect Scroll;
        [Tooltip("Category tabs in order: All, Runner, Arcade, Puzzle.")]
        public Button[] Tabs;
        public Color TabOn = Color.white, TabOff = new(1, 1, 1, 0.18f), TabTextOn = new(0.3f, 0.2f, 0.65f), TabTextOff = Color.white;

        [Header("Continue card")]
        public HubCard Featured;
        public TMP_Text FeaturedLabel;

        [Header("Wallet")]
        public TMP_Text GemText;
        public RectTransform GemIcon;

        [Header("Unlock popup")]
        public HubPopup UnlockPopup;
        public Image UnlockIcon, UnlockHeader;
        public TMP_Text UnlockInitials, UnlockTitle, UnlockMessage, UnlockCost;
        public Button UnlockButton, UnlockGetGemsButton;

        [Header("Free gems popup")]
        public HubPopup FreeGemsPopup;
        public Button FreeGemsWatchButton;
        public TMP_Text FreeGemsTimer;
        [Min(1)] public int FreeGemsAmount = 25;
        [Tooltip("Seconds between two free-gems ads.")]
        [Min(0)] public float FreeGemsCooldown = 180f;

        [Header("Settings popup")]
        public HubPopup SettingsPopup;
        public HubSwitch MusicSwitch, SoundSwitch, VibrationSwitch;
        [Tooltip("On = no confetti, menu pops or bumps and a softer lose flash (CollectionFeedback.ReducedMotion).")]
        public HubSwitch ReducedMotionSwitch;
        public MOST_AudioManager Audio;
        public string PrivacyUrl = "https://brianromanss.github.io/All-Mighty-One/";
        public TMP_Text VersionText;

        [Header("Daily reward")]
        public HubPopup DailyPopup;
        [Tooltip("The seven day tiles, day 1 first.")]
        public Image[] DailyTiles;
        public TMP_Text[] DailyAmounts;
        public GameObject[] DailyChecks;
        public Button DailyClaimButton, DailyDoubleButton;
        public TMP_Text DailyNextText;
        public GameObject DailyBadge;
        public Color DayDone = new(0.55f, 0.85f, 0.6f), DayToday = new(1f, 0.6f, 0.15f), DayLater = new(0.86f, 0.84f, 0.97f);

        [Header("Daily missions")]
        public HubPopup MissionsPopup;
        public HubMissionRow[] MissionRows;
        public TMP_Text MissionsTimer;
        public GameObject MissionsBadge;

        [Header("Banner")]
        [Tooltip("Bottom strip kept free for the banner ad.")]
        public RectTransform BannerSpace;
        public RectTransform Content;

        [Tooltip("Gems won in the games since the hub was last open fly into the wallet on return.")]
        [Range(0, 16)] public int MaxFlyingGems = 8;

        const string FreeGemsKey = "AMO_FreeGemsAt", SeenGemsKey = "AMO_HubGems";

        readonly List<HubCard> _cards = new();
        GameEntry _pendingUnlock;
        int _tab;
        float _shownGems;
        int _gemTextValue = -1;
        bool _holdWallet; // gems are flying in: the wallet counts up as they land

        // set when the hub starts a game: back in the hub, the featured card offers to continue it; a fresh app start
        // shows the game of the day instead. The daily popup opens by itself once per app session.
        static bool s_playedThisSession, s_dailyOffered;

        public IReadOnlyList<HubCard> Cards => _cards;
        public int CurrentTab => _tab;

        void Start()
        {
            CardTemplate.gameObject.SetActive(false);
            foreach (var g in Catalog.Games)
            {
                var card = Instantiate(CardTemplate, CardsRoot);
                if (card.TryGetComponent(out UiPopIn pop)) pop.Delay = 0.05f * _cards.Count; // cards pop in one after another
                card.gameObject.SetActive(true);
                card.Bind(g, Catalog, OnCardClicked);
                _cards.Add(card);
            }
            for (int i = 0; i < Tabs.Length; i++)
            {
                int k = i;
                Tabs[i].onClick.AddListener(() => SelectTab(k));
            }
            SelectTab(0);

            UnlockButton.onClick.AddListener(ConfirmUnlock);
            if (DailyClaimButton) DailyClaimButton.onClick.AddListener(() => ClaimDaily(1));
            if (DailyDoubleButton) DailyDoubleButton.onClick.AddListener(ClaimDailyDouble);
            foreach (var row in MissionRows ?? Array.Empty<HubMissionRow>())
            {
                var r = row;
                r.ClaimButton.onClick.AddListener(() => ClaimMission(r));
            }
            UnlockGetGemsButton.onClick.AddListener(OpenFreeGems);
            FreeGemsWatchButton.onClick.AddListener(WatchForGems);
            MusicSwitch.OnChanged.AddListener(on => SetAudio(MOST_AudioManager.Channel.Music, on));
            SoundSwitch.OnChanged.AddListener(on => { SetAudio(MOST_AudioManager.Channel.SFX, on); SetAudio(MOST_AudioManager.Channel.UI, on); });
            VibrationSwitch.OnChanged.AddListener(on => MOST_HapticFeedback.HapticsEnabled = on);
            if (ReducedMotionSwitch) ReducedMotionSwitch.OnChanged.AddListener(on => CollectionFeedback.ReducedMotion = on);
            if (VersionText) VersionText.text = $"v{Application.version}";

            // back from a game with new gems: the wallet starts at what the player saw last, and gems fly in
            int seen = PlayerPrefs.GetInt(SeenGemsKey, -1);
            _shownGems = Catalog.Gems;
            if (seen >= 0 && seen < Catalog.Gems && GemIcon)
            {
                DailyMissions.GemsEarned(Catalog.Gems - seen);
                _shownGems = seen;
                StartCoroutine(FlyGems(Catalog.Gems - seen));
            }
            else RememberGems();
            RefreshAll();
            AdsBridge.ShowBanner("hub");
            if (!s_dailyOffered && DailyPopup && DailyReward.Available)
            {
                s_dailyOffered = true;
                StartCoroutine(OpenDailyLater());
            }
        }

        System.Collections.IEnumerator OpenDailyLater()
        {
            yield return new WaitForSecondsRealtime(0.8f); // after the cards pop in
            if (!AnyPopupOpen) OpenDaily();
        }

        void OnDestroy()
        {
            AdsBridge.HideBanner();
            RememberGems();
        }

        void RememberGems()
        {
            PlayerPrefs.SetInt(SeenGemsKey, Catalog.Gems);
            PlayerPrefs.Save();
        }

        System.Collections.IEnumerator FlyGems(int gained)
        {
            int count = Mathf.Clamp(gained / 5 + 2, 2, MaxFlyingGems);
            float target = Catalog.Gems, start = _shownGems;
            _holdWallet = true;
            var canvas = GemIcon.GetComponentInParent<Canvas>().rootCanvas.transform;
            var source = GemIcon.GetComponent<Image>();
            Vector3 from = canvas.position, to = GemIcon.position;
            yield return new WaitForSecondsRealtime(0.35f); // let the cards pop in first
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                var gem = new GameObject("Flying Gem", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                gem.transform.SetParent(canvas, false);
                gem.sprite = source.sprite;
                gem.preserveAspect = true;
                gem.raycastTarget = false;
                gem.rectTransform.sizeDelta = GemIcon.rect.size * 1.2f;
                var spread = (Vector3)(UnityEngine.Random.insideUnitCircle * GemIcon.rect.width * 2.5f * canvas.lossyScale.x);
                StartCoroutine(FlyOne(gem.rectTransform, from + spread, to, () =>
                {
                    landed++;
                    _shownGems = Mathf.Lerp(start, target, (float)landed / count);
                    if (landed == 1) CollectionFeedback.Play(CollectionFeedback.Cue.Gems);
                }));
                yield return new WaitForSecondsRealtime(0.07f);
            }
            while (landed < count) yield return null;
            _holdWallet = false;
            RememberGems();
        }

        System.Collections.IEnumerator FlyOne(RectTransform gem, Vector3 from, Vector3 to, Action landed)
        {
            // pop out at the scatter point, then arc into the wallet
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                gem.position = from;
                gem.localScale = Vector3.one * Mathf.SmoothStep(0f, 1.15f, t / 0.25f);
                yield return null;
            }
            Vector3 control = Vector3.Lerp(from, to, 0.5f) + (from - to).magnitude * 0.25f * Vector3.left;
            for (float t = 0; t < 0.55f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.55f);
                gem.position = Vector3.Lerp(Vector3.Lerp(from, control, k), Vector3.Lerp(control, to, k), k);
                gem.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.7f, k);
                yield return null;
            }
            Destroy(gem.gameObject);
            if (GemIcon) GemIcon.localScale = Vector3.one * 1.25f;
            landed();
        }

        void Update()
        {
            // the wallet rolls towards the real balance
            int gems = Catalog.Gems;
            if (_holdWallet) { if (GemIcon) GemIcon.localScale = Vector3.Lerp(GemIcon.localScale, Vector3.one, 10f * Time.unscaledDeltaTime); }
            else if (!Mathf.Approximately(_shownGems, gems))
            {
                _shownGems = Mathf.MoveTowards(_shownGems, gems, Mathf.Max(1f, Mathf.Abs(gems - _shownGems) * 6f * Time.unscaledDeltaTime));
                if (GemIcon) GemIcon.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 30f));
            }
            else if (GemIcon) GemIcon.localScale = Vector3.one;
            int shown = Mathf.RoundToInt(_shownGems);
            if (GemText && shown != _gemTextValue) { _gemTextValue = shown; GemText.text = shown.ToString("N0"); } // no string per frame

            if (FreeGemsPopup && FreeGemsPopup.IsOpen) UpdateFreeGems();
            if (Time.frameCount % 15 == 0) UpdateTimers();

            if (Input.GetKeyDown(KeyCode.Escape)) CloseTopPopup();
        }

        // ------------------------------------------------------------------ grid

        public void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool on = i == index;
                Tabs[i].image.color = on ? TabOn : TabOff;
                var label = Tabs[i].GetComponentInChildren<TMP_Text>();
                if (label) label.color = on ? TabTextOn : TabTextOff;
            }
            foreach (var c in _cards)
                c.gameObject.SetActive(index == 0 || (int)c.Entry.Category == index - 1);
            if (Scroll) Scroll.verticalNormalizedPosition = 1;
        }

        void RefreshAll()
        {
            foreach (var c in _cards) c.Refresh(Catalog);
            var last = Catalog.LastPlayed;
            bool returning = s_playedThisSession && last != null && Catalog.IsUnlocked(last);
            var featured = returning ? last : Catalog.GameOfTheDay();
            if (Featured && featured != null)
            {
                Featured.Bind(featured, Catalog, OnCardClicked);
                if (FeaturedLabel) FeaturedLabel.text = returning ? "CONTINUE PLAYING" : "GAME OF THE DAY";
            }
            if (DailyBadge) DailyBadge.SetActive(DailyReward.Available);
            if (MissionsBadge) MissionsBadge.SetActive(DailyMissions.Claimable(Catalog) > 0);
        }

        public void OnCardClicked(GameEntry g)
        {
            if (Catalog.IsUnlocked(g)) Play(g);
            else OpenUnlock(g);
        }

        public void Play(GameEntry g)
        {
            if (SceneFader.IsLoading) return;
            GameCatalog.MarkPlayed(g);
            DailyMissions.GameStarted(g.Id);
            CollectionAnalytics.GameStart(g.Id);
            s_playedThisSession = true;
            SceneFader.Load(g.SceneName);
        }

        // ------------------------------------------------------------------ unlock

        void OpenUnlock(GameEntry g)
        {
            _pendingUnlock = g;
            UnlockTitle.text = g.DisplayName.ToUpperInvariant();
            UnlockCost.text = g.UnlockCost.ToString();
            if (UnlockHeader) UnlockHeader.color = g.Theme;
            if (UnlockIcon) { UnlockIcon.sprite = g.Icon; UnlockIcon.enabled = g.Icon; }
            if (UnlockInitials)
            {
                UnlockInitials.gameObject.SetActive(!g.Icon);
                UnlockInitials.text = g.DisplayName.Substring(0, 1);
                UnlockInitials.color = g.Theme;
            }
            RefreshUnlock();
            UnlockPopup.Open();
        }

        void RefreshUnlock()
        {
            if (_pendingUnlock == null) return;
            int missing = _pendingUnlock.UnlockCost - Catalog.Gems;
            UnlockButton.interactable = missing <= 0;
            UnlockMessage.text = missing <= 0 ? _pendingUnlock.Tagline : $"You need {missing} more gems";
            UnlockGetGemsButton.gameObject.SetActive(missing > 0);
        }

        void ConfirmUnlock()
        {
            if (_pendingUnlock == null || !Catalog.TryUnlock(_pendingUnlock)) { RefreshUnlock(); return; }
            var g = _pendingUnlock;
            _pendingUnlock = null;
            UnlockPopup.Close();
            CollectionAnalytics.GameUnlock(g.Id, "gems");
            CollectionFeedback.Play(CollectionFeedback.Cue.Unlock);
            RefreshAll();
            var card = _cards.Find(c => c.Entry == g);
            if (card && card.TryGetComponent(out UiPopIn pop)) pop.Replay();
            if (card && card.Shine) card.Shine.Play();
            RememberGems();
        }

        // ------------------------------------------------------------------ free gems

        public void OpenFreeGems()
        {
            UpdateFreeGems();
            FreeGemsPopup.Open();
        }

        public float FreeGemsWait
        {
            get
            {
                long at = long.TryParse(PlayerPrefs.GetString(FreeGemsKey, "0"), out long v) ? v : 0;
                double left = FreeGemsCooldown - (DateTime.UtcNow - new DateTime(at, DateTimeKind.Utc)).TotalSeconds;
                return Mathf.Clamp((float)left, 0f, FreeGemsCooldown); // a claim stamped in the future (clock moved) never locks longer
            }
        }

        void UpdateFreeGems()
        {
            float wait = FreeGemsWait;
            FreeGemsWatchButton.interactable = wait <= 0;
            if (FreeGemsTimer) FreeGemsTimer.text = wait <= 0 ? $"+{FreeGemsAmount}" : $"{(int)wait / 60}:{(int)wait % 60:00}";
        }

        void WatchForGems()
        {
            if (FreeGemsWait > 0) return;
            AdsBridge.ShowRewarded("hub_free_gems", ok =>
            {
                if (!ok || !this) return;
                Catalog.Database.Get<IntData>(Catalog.CurrencyName).Add(FreeGemsAmount);
                CollectionFeedback.Play(CollectionFeedback.Cue.Gems);
                Catalog.Database.SaveToJson();
                PlayerPrefs.SetString(FreeGemsKey, DateTime.UtcNow.Ticks.ToString());
                PlayerPrefs.Save();
                FreeGemsPopup.Close();
                RefreshUnlock();
            });
        }

        // ------------------------------------------------------------------ settings

        public void OpenSettings()
        {
            MusicSwitch.Set(!Audio || Audio.Music_IsOn, true);
            SoundSwitch.Set(!Audio || Audio.SFX_IsOn, true);
            VibrationSwitch.Set(MOST_HapticFeedback.HapticsEnabled, true);
            if (ReducedMotionSwitch) ReducedMotionSwitch.Set(CollectionFeedback.ReducedMotion, true);
            SettingsPopup.Open();
        }

        void SetAudio(MOST_AudioManager.Channel channel, bool on)
        {
            if (Audio) Audio.SetEnabled(channel, on);
        }

        public void OpenPrivacy() => Application.OpenURL(PrivacyUrl);

        HubPopup[] Popups => new[] { SettingsPopup, FreeGemsPopup, UnlockPopup, DailyPopup, MissionsPopup };

        bool AnyPopupOpen => Array.Exists(Popups, p => p && p.IsOpen);

        public void CloseTopPopup()
        {
            foreach (var p in Popups)
                if (p && p.IsOpen) { p.Close(); return; }
        }

        // ------------------------------------------------------------------ daily reward

        public void OpenDaily()
        {
            RefreshDaily();
            DailyPopup.Open();
        }

        void RefreshDaily()
        {
            bool available = DailyReward.Available;
            int today = DailyReward.Day;
            for (int i = 0; i < DailyTiles.Length; i++)
            {
                bool done = i < today || (i == today && !available);
                DailyTiles[i].color = done ? DayDone : i == today ? DayToday : DayLater;
                if (i < DailyAmounts.Length) DailyAmounts[i].text = DailyReward.Amounts[i].ToString();
                if (i < DailyChecks.Length) DailyChecks[i].SetActive(done);
            }
            DailyClaimButton.gameObject.SetActive(available);
            DailyDoubleButton.gameObject.SetActive(available);
            var amount = DailyClaimButton.GetComponentInChildren<TMP_Text>(true);
            if (amount) amount.text = $"+{DailyReward.TodayAmount}";
            var doubled = DailyDoubleButton.GetComponentInChildren<TMP_Text>(true);
            if (doubled) doubled.text = $"+{DailyReward.TodayAmount * 2}";
            DailyNextText.gameObject.SetActive(!available);
            UpdateTimers();
        }

        public void ClaimDaily(int multiplier)
        {
            int day = DailyReward.Day + 1;
            int gems = DailyReward.Claim(multiplier);
            if (gems <= 0) return;
            CollectionAnalytics.DailyReward(day, multiplier > 1);
            Catalog.Database.Get<IntData>(Catalog.CurrencyName).Add(gems);
            Catalog.Database.SaveToJson();
            CollectionFeedback.Play(CollectionFeedback.Cue.Unlock);
            RefreshDaily();
            RefreshAll();
            RefreshUnlock();
        }

        void ClaimDailyDouble()
        {
            AdsBridge.ShowRewarded("daily_reward_x2", ok =>
            {
                if (ok && this) ClaimDaily(2);
            });
        }

        // ------------------------------------------------------------------ daily missions

        public void OpenMissions()
        {
            RefreshMissions();
            MissionsPopup.Open();
        }

        void RefreshMissions()
        {
            var missions = DailyMissions.Today(Catalog);
            for (int i = 0; i < MissionRows.Length; i++)
            {
                MissionRows[i].gameObject.SetActive(i < missions.Length);
                if (i < missions.Length) MissionRows[i].Show(missions[i]);
            }
            UpdateTimers();
        }

        void ClaimMission(HubMissionRow row)
        {
            int gems = DailyMissions.Claim(row.Mission);
            if (gems <= 0) return;
            CollectionAnalytics.MissionClaim(row.Mission.Kind.ToString());
            Catalog.Database.Get<IntData>(Catalog.CurrencyName).Add(gems);
            Catalog.Database.SaveToJson();
            CollectionFeedback.Play(CollectionFeedback.Cue.Gems);
            RefreshMissions();
            RefreshAll();
            RefreshUnlock();
        }

        void UpdateTimers()
        {
            var left = CollectionClock.UntilTomorrow;
            string time = $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
            if (DailyNextText && DailyNextText.gameObject.activeInHierarchy) DailyNextText.text = $"Come back tomorrow!\n<size=80%>Next reward in {time}</size>";
            if (MissionsTimer && MissionsTimer.gameObject.activeInHierarchy) MissionsTimer.text = $"New missions in {time}";
        }
    }
}
