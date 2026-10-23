using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AllMightyOne
{
    /// <summary>
    /// Shared sound + vibration for moments every game has: button presses, winning, losing, unlocking and earning gems.
    /// Lives on the persistent music object (CollectionMusic starts it), so it exists once for the whole session.
    /// Sounds go through the audio manager's SFX/UI groups (the hub's Sound switch), haptics through MOST_HapticFeedback
    /// (the Vibration switch).
    /// </summary>
    public class CollectionFeedback : MonoBehaviour
    {
        public enum Cue { Click, Win, Lose, Unlock, Gems }

        public AudioClip Click, Win, Lose, Unlock, Gems;
        [Range(0f, 1f)] public float ClickVolume = 0.5f, CueVolume = 0.8f;

        static CollectionFeedback _instance;
        AudioSource _ui, _sfx;
        float _lastClick;
        readonly HashSet<Button> _hooked = new();

        /// <summary> Called by CollectionMusic on the instance that survives scene loads. </summary>
        public void Init(MOST_AudioManager audio)
        {
            _instance = this;
            _ui = gameObject.AddComponent<AudioSource>();
            _sfx = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { _ui, _sfx })
            {
                s.playOnAwake = false;
                s.spatialBlend = 0f;
            }
            if (audio)
            {
                _ui.outputAudioMixerGroup = audio.UIGroup;
                _sfx.outputAudioMixerGroup = audio.SFXGroup;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(HookButtons());
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(HookButtons());

        // every button in the new scene clicks (runtime listener: nothing is saved into the scenes). Waits for the
        // scene's Start, which builds buttons too (hub cards are copies, and copies don't carry runtime listeners).
        IEnumerator HookButtons()
        {
            yield return null;
            _hooked.RemoveWhere(b => !b);
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (_hooked.Add(b)) b.onClick.AddListener(OnButton);
        }

        void OnButton()
        {
            if (Time.unscaledTime - _lastClick < 0.05f) return; // one press can reach two hooked buttons
            _lastClick = Time.unscaledTime;
            Play(Cue.Click);
        }

        /// <summary> Plays a cue with its matching vibration. Safe to call when no instance exists (tests, a scene run alone). </summary>
        public static void Play(Cue cue)
        {
            if (!_instance) return;
            _instance.PlayCue(cue);
        }

        /// <summary> Called by UniversalGameManager the moment a run is won or lost. </summary>
        public static void RunEnded(bool won) => Play(won ? Cue.Win : Cue.Lose);

        void PlayCue(Cue cue)
        {
            var (clip, source, volume, haptic) = cue switch
            {
                Cue.Click => (Click, _ui, ClickVolume, MOST_HapticFeedback.HapticTypes.Selection),
                Cue.Win => (Win, _sfx, CueVolume, MOST_HapticFeedback.HapticTypes.Success),
                Cue.Lose => (Lose, _sfx, CueVolume, MOST_HapticFeedback.HapticTypes.Failure),
                Cue.Unlock => (Unlock, _sfx, CueVolume, MOST_HapticFeedback.HapticTypes.Success),
                _ => (Gems, _sfx, CueVolume, MOST_HapticFeedback.HapticTypes.LightImpact),
            };
            if (clip && source) source.PlayOneShot(clip, volume);
            if (MOST_HapticFeedback.HapticsEnabled) MOST_HapticFeedback.GenerateWithCooldown(haptic);
        }
    }
}
