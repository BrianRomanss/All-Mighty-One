using System;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Single entry point for every ad call in the collection.
    /// Routes to Unity Ads (UnityAdsService) once its Game ID is set.
    /// Until then, interstitials are logged and rewarded ads always succeed (for testing).
    /// </summary>
    public static class AdsBridge
    {
        // ---------- Tunables (move to RemoteConfig later) ----------
        public static int LevelEndsPerInterstitial = 2;
        public static float MinSecondsBetweenInterstitials = 45f;
        public static float FirstSessionGraceSeconds = 180f;
        public static float GraceAfterRewardedSeconds = 30f;

        const string SessionKey = "AMO_SessionCount";

        static int _levelEnds;
        static float _lastInterstitialTime = -999f;
        static float _lastRewardedTime = -999f;

        public static event Action<string> OnInterstitialShown;
        public static event Action<string, bool> OnRewardedFinished;

        public static int SessionCount => PlayerPrefs.GetInt(SessionKey, 0);

        /// <summary> Level ends reported since launch (every win / lose / run end that could show an interstitial). </summary>
        public static int LevelEndCount => _levelEnds;

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
            float now = Time.realtimeSinceStartup;
            if (SessionCount <= 1 && now < FirstSessionGraceSeconds) return false;
            if (now - _lastInterstitialTime < MinSecondsBetweenInterstitials) return false;
            if (now - _lastRewardedTime < GraceAfterRewardedSeconds) return false;
            return true;
        }

        public static void TryShowInterstitial(string placement, Action afterAd = null)
        {
            if (!CanShowInterstitial()) { afterAd?.Invoke(); return; }
            var service = UnityAdsService.Instance;
            if (service)
            {
                // nothing loaded yet: the caps are not spent, so the next level end tries again
                if (!service.ShowInterstitial(afterAd)) return;
            }
            else
            {
                Debug.Log($"[AdsBridge] (placeholder) Interstitial '{placement}'");
                afterAd?.Invoke();
            }
            _lastInterstitialTime = Time.realtimeSinceStartup;
            CollectionAnalytics.AdInterstitial(placement);
            OnInterstitialShown?.Invoke(placement);
        }

        public static bool IsRewardedReady()
        {
            var service = UnityAdsService.Instance;
            return !service || service.RewardedReady;
        }

        /// <summary> Shows a rewarded ad. onComplete(true) only when the reward must be granted. </summary>
        public static void ShowRewarded(string placement, Action<bool> onComplete)
        {
            var service = UnityAdsService.Instance;
            if (service)
            {
                service.ShowRewarded(success => Finish(placement, success, onComplete));
                return;
            }
            Debug.Log($"[AdsBridge] (placeholder) Rewarded '{placement}' -> granted");
            Finish(placement, true, onComplete);
        }

        static void Finish(string placement, bool success, Action<bool> onComplete)
        {
            _lastRewardedTime = Time.realtimeSinceStartup;
            CollectionAnalytics.AdRewarded(placement, success);
            OnRewardedFinished?.Invoke(placement, success);
            onComplete?.Invoke(success);
        }

        /// <summary> True while a banner is requested (hub and menus only, never during gameplay). </summary>
        public static bool BannerVisible { get; private set; }

        public static void ShowBanner(string placement = "banner")
        {
            if (BannerVisible) return;
            BannerVisible = true;
            if (UnityAdsService.Instance) UnityAdsService.Instance.ShowBanner();
            else Debug.Log($"[AdsBridge] (placeholder) Banner '{placement}'");
        }

        public static void HideBanner()
        {
            if (!BannerVisible) return;
            BannerVisible = false;
            if (UnityAdsService.Instance) UnityAdsService.Instance.HideBanner();
        }
    }
}
