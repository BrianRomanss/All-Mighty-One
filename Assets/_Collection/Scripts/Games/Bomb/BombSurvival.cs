using System.Collections;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AllMightyOne.Bomb
{
    /// <summary>
    /// Bomb Survival game loop (the kit scene was a hazard demo): survive the bombs, cannon shots and line blasts as long as
    /// possible. The hazard spawners only run while playing and speed up over time; the time survived is the score
    /// (best kept in PlayerPrefs). Gems per run end (1 per SecondsPerGem; a revived run only pays the new part), rewarded x3,
    /// one rewarded revive.
    /// </summary>
    public class BombSurvival : MonoBehaviour
    {
        [Header("Scene")]
        public UniversalGameManager Manager;
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        public MOST_Damage Player;
        public MOST_GridMovement Movement;
        [Tooltip("Hazard spawners (bombs, cannon, lines): only enabled while playing.")]
        public MOST_Spawn[] Hazards;
        [Tooltip("Joystick, shown only while playing.")]
        public GameObject PlayControls;

        [Header("Rules")]
        public string BestPrefsKey = "AMO_Bomb_Best";
        [Min(1)] public float SecondsPerGem = 5f;
        [Min(2)] public int RewardMultiplier = 3;
        [Tooltip("On revive, bombs and blasts this close to the player are removed.")]
        public float ReviveClearRadius = 5f;
        public float ReviveInvulnerability = 2f;

        [Header("UI")]
        public Text TimeText, MenuBestText, RunTimeText, RunBestText, RunGemsText;
        public GameObject TripleRewardButton;

        public UnityEvent OnRevive;

        bool _playing, _rewardClaimed;
        int _paidGems;

        public float Survived { get; private set; }
        public bool IsPlaying => _playing;
        public int Best => PlayerPrefs.GetInt(BestPrefsKey, 0);
        /// <summary> Gems paid by the last run end (after the rewarded multiplier, if taken). </summary>
        public int LastEarned { get; private set; }

        void Start()
        {
            SetHazards(false);
            Movement.EnableState(false);
            if (PlayControls) PlayControls.SetActive(false);
            if (MenuBestText) MenuBestText.text = $"BEST {Best}s";
            UpdateHud();
        }

        /// <summary> Wired to the game manager's OnStartPlay. </summary>
        public void Begin()
        {
            if (_playing) return;
            _playing = true;
            SetHazards(true);
            Movement.EnableState(true);
            if (PlayControls) PlayControls.SetActive(true);
        }

        void Update()
        {
            if (!_playing) return;
            Survived += Time.deltaTime;
            UpdateHud();
        }

        /// <summary> Wired to the player's MOST_Damage.OnDefeat. </summary>
        public void OnPlayerDefeated()
        {
            if (!_playing) return;
            _playing = false;
            SetHazards(false);
            if (PlayControls) PlayControls.SetActive(false);

            int seconds = Mathf.FloorToInt(Survived);
            bool newBest = seconds > Best;
            if (newBest) { PlayerPrefs.SetInt(BestPrefsKey, seconds); PlayerPrefs.Save(); }
            int total = Mathf.FloorToInt(Survived / SecondsPerGem);
            LastEarned = Mathf.Max(0, total - _paidGems);
            _paidGems = total;
            _rewardClaimed = false;
            if (LastEarned > 0) Database.Get<IntData>(CurrencyName).Add(LastEarned);
            Database.SaveToJson();

            if (RunTimeText) RunTimeText.text = $"{seconds}s";
            if (RunBestText) RunBestText.text = newBest ? "NEW BEST!" : $"BEST {Best}s";
            if (RunGemsText) RunGemsText.text = $"+{LastEarned} GEMS";
            if (TripleRewardButton) TripleRewardButton.SetActive(LastEarned > 0);
            Manager.OnLose();
        }

        void SetHazards(bool on)
        {
            foreach (var s in Hazards) if (s) s.EnableState(on);
        }

        /// <summary> Revive button: rewarded ad, then the run continues. </summary>
        public void RequestRevive()
        {
            if (_playing || !Manager.CanRevive) return;
            AdsBridge.ShowRewarded("bomb_revive", ok => { if (ok && this) Revive(); });
        }

        public void Revive()
        {
            if (_playing) return;
            var p = Player.transform.position;
            // bombs about to go off and blasts still burning next to the player are removed
            foreach (var bomb in FindObjectsByType<ExplosionBomb>(FindObjectsSortMode.None))
                if (Flat(bomb.transform.position - p) < ReviveClearRadius) Destroy(bomb.gameObject);
            foreach (var col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (col.CompareTag("Damage") && Flat(col.bounds.ClosestPoint(p) - p) < ReviveClearRadius) col.enabled = false;

            StartCoroutine(Invulnerable());
            Player.Revive(); // DefeatType Deactivate: re-activates the player with full health
            Movement.EnableState(true);
            if (PlayControls) PlayControls.SetActive(true);
            SetHazards(true);
            Manager.Revive();
            _playing = true;
            OnRevive?.Invoke();
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        IEnumerator Invulnerable()
        {
            Player.UseOnTrigger = Player.UseOnCollision = false;
            yield return new WaitForSeconds(ReviveInvulnerability);
            Player.UseOnTrigger = Player.UseOnCollision = true;
        }

        /// <summary> Rewarded ad, then multiplies the gems of this run end. </summary>
        public void RequestTripleReward()
        {
            if (_rewardClaimed || LastEarned <= 0) return;
            AdsBridge.ShowRewarded("triple_reward", ok =>
            {
                if (!ok || !this || _rewardClaimed) return;
                _rewardClaimed = true;
                Database.Get<IntData>(CurrencyName).Add(LastEarned * (RewardMultiplier - 1));
                Database.SaveToJson();
                LastEarned *= RewardMultiplier;
                if (RunGemsText) RunGemsText.text = $"+{LastEarned} GEMS";
                if (TripleRewardButton) TripleRewardButton.SetActive(false);
            });
        }

        /// <summary> Retry button: reloads the scene (back to the menu). </summary>
        public void Reload() => Manager.RestartCurrent();

        void UpdateHud()
        {
            if (TimeText) TimeText.text = $"{Mathf.FloorToInt(Survived)}s";
        }
    }
}
