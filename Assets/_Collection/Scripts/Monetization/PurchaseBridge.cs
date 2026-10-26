using System;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Single entry point for in-app purchases. Placeholder until the store SDK is installed:
    /// without GBP_IAP every purchase succeeds immediately (for testing) and grants its content.
    /// </summary>
    public static class PurchaseBridge
    {
        public const string NoAdsProduct = "no_ads";

        public static event Action<string> OnPurchased;

        /// <summary> Localized price from the store once it is wired; a fixed label until then. </summary>
        public static string PriceLabel(string productId) => productId == NoAdsProduct ? "$2.99" : "";

        public static bool Owns(string productId) => productId == NoAdsProduct && AdsBridge.NoAds;

        public static void Buy(string productId, Action<bool> onComplete)
        {
#if GBP_IAP
            // TODO: route to the store SDK and call Grant(productId) from its success callback
#endif
            Debug.Log($"[PurchaseBridge] (placeholder) Purchase '{productId}' granted");
            Grant(productId);
            onComplete?.Invoke(true);
        }

        public static void Restore(Action<bool> onComplete)
        {
            Debug.Log("[PurchaseBridge] (placeholder) Restore purchases");
            onComplete?.Invoke(true);
        }

        static void Grant(string productId)
        {
            CollectionAnalytics.Purchase(productId);
            if (productId == NoAdsProduct)
            {
                AdsBridge.NoAds = true;
                AdsBridge.HideBanner();
            }
            OnPurchased?.Invoke(productId);
        }
    }
}
