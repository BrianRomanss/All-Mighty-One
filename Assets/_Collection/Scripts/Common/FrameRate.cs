using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Sets the frame rate before the first scene loads. Android caps at 30 fps unless a target is set, and vSync
    /// must be off on mobile for <see cref="Application.targetFrameRate"/> to apply.
    /// </summary>
    /// <example><code>FrameRate.Apply(30); // e.g. a battery saver setting</code></example>
    public static class FrameRate
    {
        /// <summary> Frames per second for the whole collection (hub and games). </summary>
        public static int Target = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Apply(Target);

        /// <summary> Switches vSync off and caps rendering at <paramref name="fps"/>. </summary>
        public static void Apply(int fps)
        {
            Target = fps;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }
    }
}
