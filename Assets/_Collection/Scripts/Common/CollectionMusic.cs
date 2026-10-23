using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Background music for the whole collection: the first instance survives scene loads and keeps the loop going
    /// from the hub into every game and back; copies placed in the other scenes remove themselves. Plays through the
    /// audio manager's Music group, so the hub's Music switch mutes it.
    /// </summary>
    public class CollectionMusic : MonoBehaviour
    {
        public AudioClip Clip;
        public MOST_AudioManager Audio;
        [Range(0f, 1f)] public float Volume = 0.55f;

        static CollectionMusic _instance;
        AudioSource _source;

        void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = Clip;
            _source.loop = true;
            _source.volume = Volume;
            _source.spatialBlend = 0f;
            _source.playOnAwake = false;
            _source.ignoreListenerPause = true;
            if (Audio) _source.outputAudioMixerGroup = Audio.MusicGroup;
            if (Clip) _source.Play();
            if (TryGetComponent(out CollectionFeedback feedback)) feedback.Init(Audio);
        }

        void Start()
        {
            // the saved mute/volume state is only pushed to the mixer by the game manager's settings panel:
            // apply it here too so a muted player starts muted in the hub (mixer values are ignored in Awake)
            if (_instance == this && Audio) Audio.ApplyAll();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
