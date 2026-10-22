using System;
using UnityEngine;
#if GBP_ADS
using GBP.Core.Modules;
#endif

namespace AllMightyOne
{
    /// <summary>
    /// Single entry point for every ad call in the collection.
    /// Placeholder until the GBP ads SDK is installed: define GBP_ADS to route calls to Ads_Core.
    /// Without GBP_ADS, interstitials are logged and rewarded ads always succeed (for testing).
    /// </summary>
    public static class AdsBridge
    {
        // ---------- Tunables (move to RemoteConfig later) ----------
        public static int LevelEndsPerInterstitial = 2;
        public static float MinSecondsBetweenInterstitials = 45f;
        public static float FirstSessionGraceSeconds = 180f;
        public static float GraceAfterRewardedSeconds = 30f;

        const string NoAdsKey = "AMO_NoAds";
        const string SessionKey = "AMO_SessionCount";

        static int _levelEnds;
        static float _lastInterstitialTime = -999f;
        static float _lastRewardedTime = -999f;

        public static event Action<string> OnInterstitialShown;
        public static event Action<string, bool> OnRewardedFinished;

        public static int SessionCount => PlayerPrefs.GetInt(SessionKey, 0);

        /// <summary> True once the player bought "Remove Ads". Rewarded ads stay available. </summary>
        public static bool NoAds
        {
            get => PlayerPrefs.GetInt(NoAdsKey, 0) == 1;
            set { PlayerPrefs.SetInt(NoAdsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            _levelEnds = 0;
            _lastInterstitialTime = -999f;
            _lastRewardedTime = -999f;
            PlayerPrefs.SetInt(SessionKey, SessionCount + 1);
        }

        /// <summary> Call on every level win / lose / run end. Shows an interstitial when the caps allow it. </summary>
        public static void OnLevelEnd(string placement = "level_end")
        {
            _levelEnds++;
            if (_levelEnds % Mathf.Max(1, LevelEndsPerInterstitial) != 0) return;
            TryShowInterstitial(placement);
        }

        public static bool CanShowInterstitial()
        {
            if (NoAds) return false;
            float now = Time.realtimeSinceStartup;
            if (SessionCount <= 1 && now < FirstSessionGraceSeconds) return false;
            if (now - _lastInterstitialTime < MinSecondsBetweenInterstitials) return false;
            if (now - _lastRewardedTime < GraceAfterRewardedSeconds) return false;
            return true;
        }

        public static void TryShowInterstitial(string placement, Action afterAd = null)
        {
            if (!CanShowInterstitial()) { afterAd?.Invoke(); return; }
            _lastInterstitialTime = Time.realtimeSinceStartup;
            OnInterstitialShown?.Invoke(placement);
#if GBP_ADS
            Ads_Core.ShowInterstitial(afterAd, placement);
#else
            Debug.Log($"[AdsBridge] (placeholder) Interstitial '{placement}'");
            afterAd?.Invoke();
#endif
        }

        public static bool IsRewardedReady()
        {
#if GBP_ADS
            return Ads_Core.IsRewardedAdReady();
#else
            return true;
#endif
        }

        /// <summary> Shows a rewarded ad. onComplete(true) only when the reward must be granted. </summary>
        public static void ShowRewarded(string placement, Action<bool> onComplete)
        {
#if GBP_ADS
            Ads_Core.ShowRewarded(success => Finish(placement, success, onComplete), placement);
#else
            Debug.Log($"[AdsBridge] (placeholder) Rewarded '{placement}' -> granted");
            Finish(placement, true, onComplete);
#endif
        }

        static void Finish(string placement, bool success, Action<bool> onComplete)
        {
            _lastRewardedTime = Time.realtimeSinceStartup;
            OnRewardedFinished?.Invoke(placement, success);
            onComplete?.Invoke(success);
        }

        /// <summary> True while a banner is requested (hub and menus only, never during gameplay). </summary>
        public static bool BannerVisible { get; private set; }

        public static void ShowBanner(string placement = "banner")
        {
            if (NoAds || BannerVisible) return;
            BannerVisible = true;
#if GBP_ADS
            Ads_Core.ShowBanner(placement);
#else
            Debug.Log($"[AdsBridge] (placeholder) Banner '{placement}'");
#endif
        }

        public static void HideBanner()
        {
            if (!BannerVisible) return;
            BannerVisible = false;
#if GBP_ADS
            Ads_Core.HideBanner();
#endif
        }
    }
}
