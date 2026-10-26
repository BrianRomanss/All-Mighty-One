using System;
using UnityEngine;
using UnityEngine.Advertisements;

namespace AllMightyOne
{
    /// <summary>
    /// Unity Ads (com.unity.ads) behind AdsBridge: initializes on launch, keeps one interstitial and one rewarded ad loaded,
    /// and reloads each after it is shown. Game code never calls this directly, only AdsBridge.
    /// </summary>
    public class UnityAdsService : MonoBehaviour, IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
    {
        // ---------- Unity Cloud dashboard > Monetization > Ad Units ----------
        public const string AndroidGameId = "";          // TODO: paste the Android Game ID
        public const string IosGameId = "";              // TODO: paste the iOS Game ID (if the app ships on iOS)
        const string InterstitialAndroid = "Interstitial_Android";
        const string RewardedAndroid = "Rewarded_Android";
        const string BannerAndroid = "Banner_Android";
        const string InterstitialIos = "Interstitial_iOS";
        const string RewardedIos = "Rewarded_iOS";
        const string BannerIos = "Banner_iOS";

        /// <summary> Test ads in the editor and development builds; live ads in release builds. </summary>
        static bool TestMode => Application.isEditor || Debug.isDebugBuild;

        const float RetryDelay = 10f;

        public static UnityAdsService Instance { get; private set; }

        static string GameId => Application.platform == RuntimePlatform.IPhonePlayer ? IosGameId : AndroidGameId;
        static bool Ios => Application.platform == RuntimePlatform.IPhonePlayer;
        static string InterstitialId => Ios ? InterstitialIos : InterstitialAndroid;
        static string RewardedId => Ios ? RewardedIos : RewardedAndroid;
        static string BannerId => Ios ? BannerIos : BannerAndroid;

        /// <summary> False when no Game ID is set or the platform has no Unity Ads: AdsBridge then uses its placeholders. </summary>
        public static bool Configured => !string.IsNullOrEmpty(GameId) && Advertisement.isSupported;

        public bool InterstitialReady { get; private set; }
        public bool RewardedReady { get; private set; }

        Action _afterInterstitial;
        Action<bool> _afterRewarded;
        bool _bannerWanted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (!Configured)
            {
                Debug.LogWarning("[UnityAds] No Game ID set in UnityAdsService: ads use AdsBridge placeholders.");
                return;
            }
            var go = new GameObject(nameof(UnityAdsService));
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<UnityAdsService>();
            Advertisement.Initialize(GameId, TestMode, Instance);
        }

        public void OnInitializationComplete()
        {
            Debug.Log($"[UnityAds] Initialized (test mode: {TestMode})");
            Load(InterstitialId);
            Load(RewardedId);
            if (_bannerWanted) ShowBanner();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogWarning($"[UnityAds] Init failed: {error} {message}");
            Invoke(nameof(RetryInit), RetryDelay);
        }

        void RetryInit() => Advertisement.Initialize(GameId, TestMode, this);

        void Load(string adUnitId) => Advertisement.Load(adUnitId, this);

        void LoadInterstitial() => Load(InterstitialId);
        void LoadRewarded() => Load(RewardedId);

        public void OnUnityAdsAdLoaded(string adUnitId)
        {
            if (adUnitId == InterstitialId) InterstitialReady = true;
            else if (adUnitId == RewardedId) RewardedReady = true;
        }

        public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
        {
            Debug.LogWarning($"[UnityAds] Load failed '{adUnitId}': {error} {message}");
            Invoke(adUnitId == RewardedId ? nameof(LoadRewarded) : nameof(LoadInterstitial), RetryDelay);
        }

        // ---------- Interstitial ----------

        /// <summary> Shows the interstitial if one is loaded; afterAd always runs, right away when nothing is shown. </summary>
        public bool ShowInterstitial(Action afterAd)
        {
            if (!InterstitialReady) { afterAd?.Invoke(); return false; }
            InterstitialReady = false;
            _afterInterstitial = afterAd;
            Advertisement.Show(InterstitialId, this);
            return true;
        }

        // ---------- Rewarded ----------

        public void ShowRewarded(Action<bool> onComplete)
        {
            if (!RewardedReady) { onComplete?.Invoke(false); return; }
            RewardedReady = false;
            _afterRewarded = onComplete;
            Advertisement.Show(RewardedId, this);
        }

        public void OnUnityAdsShowStart(string adUnitId) { }
        public void OnUnityAdsShowClick(string adUnitId) { }

        public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState state) =>
            Finish(adUnitId, state == UnityAdsShowCompletionState.COMPLETED);

        public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            Debug.LogWarning($"[UnityAds] Show failed '{adUnitId}': {error} {message}");
            Finish(adUnitId, false);
        }

        void Finish(string adUnitId, bool completed)
        {
            if (adUnitId == RewardedId)
            {
                var callback = _afterRewarded;
                _afterRewarded = null;
                LoadRewarded();
                callback?.Invoke(completed);
            }
            else if (adUnitId == InterstitialId)
            {
                var callback = _afterInterstitial;
                _afterInterstitial = null;
                LoadInterstitial();
                callback?.Invoke();
            }
        }

        // ---------- Banner ----------

        public void ShowBanner()
        {
            _bannerWanted = true;
            if (!Advertisement.isInitialized) return; // shown once init completes
            Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
            Advertisement.Banner.Load(BannerId, new BannerLoadOptions
            {
                loadCallback = () => { if (_bannerWanted) Advertisement.Banner.Show(BannerId); },
                errorCallback = message => Debug.LogWarning($"[UnityAds] Banner load failed: {message}")
            });
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            if (Advertisement.isInitialized) Advertisement.Banner.Hide();
        }
    }
}
