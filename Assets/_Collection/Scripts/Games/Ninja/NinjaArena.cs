using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AllMightyOne.Ninja
{
    /// <summary>
    /// Ninja Arena game loop (the kit scene was a sandbox): each level spawns waves of zombies around the ninja; clearing every
    /// wave wins, losing all health loses. Zombies (the kit's WalkEnemyManager) chase the ninja and hit on contact.
    /// Gems are paid per kill, plus a level bonus with a rewarded x3; one rewarded revive per level.
    /// </summary>
    public class NinjaArena : MonoBehaviour
    {
        [Header("Scene")]
        public UniversalGameManager Manager;
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        public Transform Player;
        public MOST_FreeMovement Movement;
        public NinjaMode Modes;
        [Tooltip("Inactive zombie used as the spawn template.")]
        public GameObject ZombieTemplate;
        [Tooltip("Joysticks and buttons, shown only while playing.")]
        public GameObject PlayControls;

        [Header("Levels")]
        public string LevelPrefsKey = "AMO_Ninja_Level";
        [Min(1)] public int BaseZombiesPerWave = 4;
        [Min(1)] public int MaxZombiesPerWave = 12;
        [Tooltip("A new wave every this many levels.")]
        [Min(1)] public int LevelsPerExtraWave = 4;
        [Min(1)] public int MaxWaves = 4;
        public float ZombieSpeed = 3.2f, ZombieSpeedPerLevel = 0.08f, MaxZombieSpeed = 5.2f;
        [Tooltip("Seconds between zombie spawns within a wave.")]
        public float SpawnInterval = 0.5f;
        public float SpawnRadiusMin = 11f, SpawnRadiusMax = 16f;
        [Tooltip("Zombies spawn inside +- this on X and Z (the arena walls are at 48).")]
        public float ArenaHalfSize = 44f;

        [Header("Player")]
        public float MaxHealth = 100f;
        public float ZombieDamage = 10f;
        [Tooltip("Seconds between two hits of the same zombie.")]
        public float HitCooldown = 1f;
        public float HitRange = 1.3f;
        [Tooltip("A zombie that hits is pushed back this far.")]
        public float KnockBack = 1.5f;
        public float ReviveClearRadius = 8f;
        public float ReviveInvulnerability = 2f;

        [Header("Rewards")]
        [Min(0)] public int GemsPerKill = 1;
        [Min(0)] public int LevelBonus = 10, LevelBonusPerLevel = 2;
        [Min(2)] public int RewardMultiplier = 3;

        [Header("UI")]
        public Image HealthFill;
        public Text LevelText, MenuLevelText, WaveText, WinGemsText, LoseGemsText;
        public GameObject TripleRewardButton;

        public UnityEvent OnRevive;

        readonly List<MOST_Damage> _zombies = new();
        readonly Dictionary<MOST_Damage, float> _nextHit = new();
        bool _playing, _ended, _rewardClaimed;
        float _invulnerableUntil;
        int _killGems;

        public int Level => Mathf.Max(1, PlayerPrefs.GetInt(LevelPrefsKey, 1));
        public int WaveCount => Mathf.Min(MaxWaves, 1 + (Level - 1) / LevelsPerExtraWave);
        public int ZombiesPerWave => Mathf.Min(MaxZombiesPerWave, BaseZombiesPerWave + (Level - 1) / 2);
        public float CurrentZombieSpeed => Mathf.Min(MaxZombieSpeed, ZombieSpeed + ZombieSpeedPerLevel * (Level - 1));
        public int Wave { get; private set; }
        public float Health { get; private set; }
        public bool IsPlaying => _playing;
        public int AliveZombies { get { _zombies.RemoveAll(z => !z || z.IsDefeated()); return _zombies.Count; } }
        /// <summary> Gems paid by this level so far (kills, plus the level bonus and the rewarded multiplier once won). </summary>
        public int LastEarned { get; private set; }

        void Start()
        {
            Health = MaxHealth;
            UpdateHud();
            if (ZombieTemplate) ZombieTemplate.SetActive(false);
            if (PlayControls) PlayControls.SetActive(false);
        }

        /// <summary> Wired to the game manager's OnStartPlay. </summary>
        public void Begin()
        {
            if (_playing || _ended) return;
            _playing = true;
            if (PlayControls) PlayControls.SetActive(true);
            StartCoroutine(RunWaves());
        }

        IEnumerator RunWaves()
        {
            for (Wave = 1; Wave <= WaveCount; Wave++)
            {
                UpdateHud();
                for (int i = 0; i < ZombiesPerWave; i++)
                {
                    yield return new WaitUntil(() => _playing);
                    SpawnZombie();
                    yield return new WaitForSeconds(SpawnInterval);
                }
                yield return new WaitUntil(() => _playing && AliveZombies == 0);
                if (Wave < WaveCount) yield return new WaitForSeconds(1.5f);
            }
            Win();
        }

        public MOST_Damage SpawnZombie()
        {
            var dir = Random.insideUnitCircle.normalized;
            var p = Player.position + new Vector3(dir.x, 0, dir.y) * Random.Range(SpawnRadiusMin, SpawnRadiusMax);
            p.x = Mathf.Clamp(p.x, -ArenaHalfSize, ArenaHalfSize);
            p.z = Mathf.Clamp(p.z, -ArenaHalfSize, ArenaHalfSize);
            p.y = ZombieTemplate.transform.position.y;
            var go = Instantiate(ZombieTemplate, p, Quaternion.identity, ZombieTemplate.transform.parent);
            go.name = "Zombie";
            var walker = go.GetComponent<WalkEnemyManager>();
            walker.MoveSpeed = CurrentZombieSpeed;
            walker.FollowOnStart = true;
            walker.StartMove = true;
            go.SetActive(true);
            var damage = go.GetComponent<MOST_Damage>();
            damage.Health = damage.MaxHealth; // MOST_Damage only sets it in Start: until then the zombie would count as defeated
            damage.OnDefeat.AddListener(() => OnZombieKilled(damage));
            _zombies.Add(damage);
            return damage;
        }

        void OnZombieKilled(MOST_Damage zombie)
        {
            _zombies.Remove(zombie);
            if (GemsPerKill <= 0) return;
            Database.Get<IntData>(CurrencyName).Add(GemsPerKill);
            _killGems += GemsPerKill;
        }

        void Update()
        {
            if (!_playing) return;
            foreach (var z in _zombies)
            {
                if (!z || z.IsDefeated()) continue;
                var d = z.transform.position - Player.position; d.y = 0;
                if (d.magnitude > HitRange) continue;
                if (_nextHit.TryGetValue(z, out float t) && Time.time < t) continue;
                _nextHit[z] = Time.time + HitCooldown;
                z.transform.position += (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * KnockBack;
                TakeDamage(ZombieDamage);
                if (!_playing) break;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!_playing || Time.time < _invulnerableUntil) return;
            Health = Mathf.Max(0, Health - amount);
            UpdateHud();
            if (Health <= 0) Lose();
        }

        void Lose()
        {
            _playing = false;
            SetPlayerActive(false);
            foreach (var a in Animators()) a.SetBool("Defeat", true);
            if (LoseGemsText) LoseGemsText.text = $"+{_killGems} GEMS";
            Database.SaveToJson();
            Manager.OnLose();
        }

        void Win()
        {
            _playing = false;
            _ended = true;
            SetPlayerActive(false, keepCollider: true);
            int bonus = LevelBonus + LevelBonusPerLevel * (Level - 1);
            Database.Get<IntData>(CurrencyName).Add(bonus);
            Database.SaveToJson();
            LastEarned = _killGems + bonus;
            _rewardClaimed = false;
            PlayerPrefs.SetInt(LevelPrefsKey, Level + 1);
            PlayerPrefs.Save();
            if (WinGemsText) WinGemsText.text = $"+{LastEarned} GEMS";
            if (TripleRewardButton) TripleRewardButton.SetActive(true);
            Manager.OnWin();
        }

        // zombies stop chasing a ninja whose collider is off (WalkEnemyManager)
        void SetPlayerActive(bool active, bool keepCollider = false)
        {
            if (PlayControls) PlayControls.SetActive(active);
            Movement.EnableState(active);
            if (!keepCollider) foreach (var c in Player.GetComponents<Collider>()) c.enabled = active;
        }

        IEnumerable<Animator> Animators()
        {
            if (Modes.KatanaAnimator) yield return Modes.KatanaAnimator;
            if (Modes.ShurikenAnimator) yield return Modes.ShurikenAnimator;
        }

        /// <summary> Revive button: rewarded ad, then the level goes on with full health. </summary>
        public void RequestRevive()
        {
            if (_playing || _ended || !Manager.CanRevive) return;
            AdsBridge.ShowRewarded("ninja_revive", ok => { if (ok && this) Revive(); });
        }

        public void Revive()
        {
            if (_playing || _ended) return;
            // zombies close to the ninja are pushed back out to the spawn ring
            foreach (var z in _zombies)
            {
                if (!z || z.IsDefeated()) continue;
                var d = z.transform.position - Player.position; d.y = 0;
                if (d.magnitude < ReviveClearRadius)
                    z.transform.position = Player.position + (d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward) * SpawnRadiusMin;
            }
            Health = MaxHealth;
            _invulnerableUntil = Time.time + ReviveInvulnerability;
            foreach (var a in Animators()) { a.Rebind(); a.SetBool("Defeat", false); }
            SetPlayerActive(true);
            Manager.Revive();
            _playing = true;
            UpdateHud();
            OnRevive?.Invoke();
        }

        /// <summary> Rewarded ad, then multiplies the gems of this level. </summary>
        public void RequestTripleReward()
        {
            if (!_ended || _rewardClaimed || LastEarned <= 0) return;
            AdsBridge.ShowRewarded("triple_reward", ok =>
            {
                if (!ok || !this || _rewardClaimed) return;
                _rewardClaimed = true;
                Database.Get<IntData>(CurrencyName).Add(LastEarned * (RewardMultiplier - 1));
                Database.SaveToJson();
                LastEarned *= RewardMultiplier;
                if (WinGemsText) WinGemsText.text = $"+{LastEarned} GEMS";
                if (TripleRewardButton) TripleRewardButton.SetActive(false);
            });
        }

        /// <summary> Next / retry buttons: reloads the arena (the level number is kept in PlayerPrefs). </summary>
        public void Reload() => Manager.RestartCurrent();

        void UpdateHud()
        {
            if (HealthFill) HealthFill.fillAmount = Health / MaxHealth;
            if (LevelText) LevelText.text = $"LEVEL {Level}";
            if (MenuLevelText) MenuLevelText.text = $"LEVEL {Level}";
            if (WaveText) WaveText.text = $"WAVE {Mathf.Clamp(Wave, 1, WaveCount)}/{WaveCount}";
        }
    }
}
