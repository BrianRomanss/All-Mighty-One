using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    public static class StoreSettings
    {
        public const string AppId = "com.HyperSpace.AllMightyOne";
        public const string Company = "HyperSpace Games Studio";
        public const string ProductName = "All Mighty One";
        const AndroidSdkVersions TargetSdk = (AndroidSdkVersions)35; // Google Play's required target level

        const string StoreArt = "Assets/_Collection/Art/Store";
        const string IconPath = StoreArt + "/app_icon.png";
        const string RoundIconPath = StoreArt + "/icon_round.png";
        // adaptive layers made from app_icon: the background holds the whole icon inside the 72dp safe zone
        const string AdaptiveBackgroundPath = StoreArt + "/icon_adaptive_bg.png";
        const string AdaptiveForegroundPath = StoreArt + "/icon_adaptive_fg.png";
        const string SplashLogoPath = "Assets/_Collection/Art/Hub/Logo.png";
        static readonly Color SplashBackground = new Color32(74, 52, 170, 255);

        /// <summary>
        /// Everything Google Play needs from Player Settings: package name, company and product name, target SDK,
        /// IL2CPP + ARM64, app icons (default, Android adaptive / round / legacy) and the splash logo.
        /// </summary>
        [MenuItem("All Mighty One/Project/Apply Store Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = Company;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, AppId);

            PlayerSettings.Android.targetSdkVersion = TargetSdk;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            ApplyIcons();
            ApplySplash();

            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Store settings: {AppId}, {Company}, \"{ProductName}\", target SDK {(int)TargetSdk}");
        }

        static void ApplyIcons()
        {
            var icon = Load<Texture2D>(IconPath);
            if (!icon) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            Fill(android, AndroidPlatformIconKind.Legacy, icon);
            Fill(android, AndroidPlatformIconKind.Round, Load<Texture2D>(RoundIconPath));
            Fill(android, AndroidPlatformIconKind.Adaptive, Load<Texture2D>(AdaptiveBackgroundPath), Load<Texture2D>(AdaptiveForegroundPath));
        }

        /// <summary> Every size of one icon kind gets the same textures (layer order: background, foreground). </summary>
        static void Fill(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var slot in icons)
                for (int i = 0; i < slot.maxLayerCount && i < layers.Length; i++)
                    slot.SetTexture(layers[i], i);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        static void ApplySplash()
        {
            var logo = Load<Sprite>(SplashLogoPath);
            if (!logo) return;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.backgroundColor = SplashBackground;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2f, logo) };
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) Debug.LogWarning($"[AMO] Store settings: missing {path}");
            return asset;
        }
    }
}
