using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class CharacterControl_Gun : MonoBehaviour
    {
        [BigHeader("Main")]
        [Tooltip("Is Moving and shooting\nCall Activate([Optional] float cooldown) or deactivate() to cntrol it")]
        [ReadOnly] public bool IsActive;

        [Tooltip("The Damage/Health system attached to the character")]
        [Required] public MOST_Damage DamageSystem;

        [BigHeader("Projectile Spawn Settings")]
        [Required] public GameObject Bullet;
        [Required] public Transform SpawnPoint;
        [InnerHint("(Optional)")] public ParticleSystem MuzzleFlare;
        [InnerHint("(Optional)")] public AudioSource SpawnSound;
        
        [BigHeader("Animations")]
        [InnerHint("(Optional)")] public Animation Animation;
        public string OnShootAnimation;
        public string OnDefeatAnimation;

        [BigHeader("Game Events")]
        [GUIColor("red")] public LayerMask EndLineMask;
        [Line]
        [Tooltip("Event When the level passed and win")]
        public UnityEvent EndLineTriggerEvent;
        [Tooltip("Event When the level passed and win")]
        public UnityEvent OnWinEvent;
        [Tooltip("Event When the character defeated before level passed and lose")]
        public UnityEvent OnLoseEvent;

        [BigHeader("Upgrade and Data Display")]
        public MOST_Database Holder;
        public string CurrencyName;
        [Line]
        [Header("Health Settings")]
        public float MaxHealth_IncreasePerLevel = 1;
        public int MaxHealth_MaxLevel = 10;
        public TMP_Text MaxHealthText;
        public GameObject CostDispaly_MaxHealth;
        public GameObject OnMaxDisplay_MaxHealth;
        [Line]
        [Header("Fire Rate Settings")]
        public float FireRate = 3;
        public float FireRate_IncreasePerLevel = .3f;
        public int FireRate_MaxLevel = 10;
        public TMP_Text FireRateText;
        public GameObject CostDispaly_FireRate;
        public GameObject OnMaxDisplay_FireRate;
        [Line]
        [Header("Fire Range Settings")]
        public float FireRange = 1;
        public float FireRange_IncreasePerLevel = .1f;
        public int FireRange_MaxLevel = 10;
        public TMP_Text FireRangeText;
        public GameObject CostDispaly_FireRange;
        public GameObject OnMaxDisplay_FireRange;
        [Line]
        [Header("Cost Settings")]
        [Tooltip("Cost of the first upgrade of each stat.")]
        public int CostPerUpgrade;
        [Tooltip("Each further upgrade of the same stat costs this much more (1 = flat price).")]
        [Min(1f)] public float CostGrowth = 1.35f;
        [Tooltip("Legacy shared events. Not invoked any more: each upgrade card refreshes its own Cost / Cost (Not Enough) / MAX state.")]
        public UnityEvent<bool> EnoughCurrency;
        [Tooltip("Legacy shared events. Not invoked any more (see EnoughCurrency).")]
        public UnityEvent<bool> NotEnoughCurrency;
        public UnityEvent OnUpgradeEvent;

        [BigHeader("Collection")]
        [Tooltip("Seconds of invulnerability after a revive.")]
        [Min(0)] public float ReviveInvulnerability = 2f;
        [Tooltip("Enemies (tag Enemy) this far ahead are removed on revive.")]
        [Min(0)] public float ReviveClearDistance = 12f;
        [Tooltip("Text shown on a card that can be upgraded for free with a rewarded ad.")]
        public string FreeUpgradeText = "FREE (AD)";

        float _shootCooldown,_maxHealth_lvl1, _fireRate_lvl1, _fireRange_lvl1,
            _maxHealth_maxLevel, _fireRate_maxLevel, _fireRange_maxLevel;

        enum Stat { Health, FireRate, FireRange }

        void Start()
        {
            LoadData();
        }

        void Update()
        {
            if (IsActive && _shootCooldown <= 0)
            {
                Destroy( Instantiate(Bullet, SpawnPoint.position, transform.rotation),FireRange);
                if (MuzzleFlare) MuzzleFlare.Play();
                if (SpawnSound) SpawnSound.Play();
                if(Animation) Animation.Play(OnShootAnimation);
                _shootCooldown = 1 / FireRate;
            }
            else _shootCooldown -= Time.deltaTime;
        }

        public void Activate([Optional] float cooldown)
        {
            IsActive = true;
            _shootCooldown = cooldown;
            GetComponent<MOST_RoadMovement>().EnableState(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
        }

        public void UpdateHealth(float amount)
        {
            if (DamageSystem)
            {
                if (amount > 0) DamageSystem.AddHealth(amount);
                else DamageSystem.OnDamage(-amount);
            }
            else Debug.Log("The healing function required MOST_Damage system attached to the object");
        }

        public void OnDefeat()
        {
            IsActive = false;
            GetComponent<MOST_RoadMovement>().EnableState(false);
            if(Animation) Animation.Play(OnDefeatAnimation);
            OnLoseEvent?.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Gate")) // Gate Triggerd
            {
                if (other.GetComponent<MOST_Gate>()) // accepts olny if the object has Most_Gate system
                    GateTriggered(other.GetComponent<MOST_Gate>());
            }
            else if (EndLineMask == (EndLineMask | (1 << other.gameObject.layer))) // EndPoint
            {
                EndLineTriggerEvent?.Invoke();
            }
        }

        void GateTriggered(MOST_Gate gate)
        {
            if (gate.IsCollected) return;
            if (gate.Type == MOST_Gate.GateType.Health)
            {
                if (DamageSystem)
                {
                    float health = gate.Calculation(DamageSystem.Health);
                    UpdateHealth(health - DamageSystem.Health);
                }
                else Debug.Log("There is no Damage component attached to the character to get the health value");
            }
            else if (gate.Type == MOST_Gate.GateType.FireRate)
            {
                FireRate = gate.Calculation(FireRate);
            }
            else if (gate.Type == MOST_Gate.GateType.FireRange)
            {
                FireRange = gate.Calculation(FireRange);
            }
            else if (gate.Type == MOST_Gate.GateType.Upgrade)
            {
                // Debug.Log("Upgrade gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.AddChilds)
            {
                // Debug.Log("AddChilds gate Triggered");
            }
            else if (gate.Type == MOST_Gate.GateType.Currency)
            {
                gate.Calculation();
            }
            else
            {
                Debug.Log("This Gate type is not defined on this character control");
            }
        }

        #region Cherecter Upgrade and Data
        void LoadData()
        {
            if (DamageSystem)
            {
                _maxHealth_lvl1 = DamageSystem.MaxHealth; // Start/Level 1 value
                _maxHealth_maxLevel = Mathf.Round((_maxHealth_lvl1 + MaxHealth_IncreasePerLevel * (MaxHealth_MaxLevel - 1)) *100) / 100;

                if (!PlayerPrefs.HasKey("CharacterGun_MaxHealth"))
                    PlayerPrefs.SetFloat("CharacterGun_MaxHealth", DamageSystem.MaxHealth);
                else DamageSystem.MaxHealth = PlayerPrefs.GetFloat("CharacterGun_MaxHealth");
                MaxHealthText.text = DamageSystem.MaxHealth.ToString("0.00");
                if (DamageSystem.MaxHealth >= _maxHealth_maxLevel)
                {
                    CostDispaly_MaxHealth.SetActive(false);
                    OnMaxDisplay_MaxHealth.SetActive(true);
                }
            }

            _fireRate_lvl1 = FireRate; // Start/Level 1 value
            _fireRate_maxLevel = Mathf.Round((_fireRate_lvl1 + FireRate_IncreasePerLevel * (FireRate_MaxLevel - 1)) * 100) / 100;
            if (!PlayerPrefs.HasKey("CharacterGun_FireRate"))
                PlayerPrefs.SetFloat("CharacterGun_FireRate", FireRate);
            else FireRate = PlayerPrefs.GetFloat("CharacterGun_FireRate");
            FireRateText.text = FireRate.ToString("0.00");
            if (FireRate >= _fireRate_maxLevel)
            {
                CostDispaly_FireRate.SetActive(false);
                OnMaxDisplay_FireRate.SetActive(true);
            }

            _fireRange_lvl1 = FireRange; // Start/Level 1 value
            _fireRange_maxLevel = Mathf.Round((_fireRange_lvl1 + FireRange_IncreasePerLevel * (FireRange_MaxLevel - 1)) *100) / 100;
            if (!PlayerPrefs.HasKey("CharacterGun_FireRange"))
                PlayerPrefs.SetFloat("CharacterGun_FireRange", FireRange);
            else FireRange = PlayerPrefs.GetFloat("CharacterGun_FireRange");
            FireRangeText.text = FireRange.ToString("0.00");
            if (FireRange >= _fireRange_maxLevel)
            {
                CostDispaly_FireRange.SetActive(false);
                OnMaxDisplay_FireRange.SetActive(true);
            }

            RefreshUpgradeUI();
        }

        // Upgrade buttons: pay with gems when affordable, otherwise offer a free upgrade for a rewarded ad
        public void UpgradeMaxHealth([Optional] float value) => TryUpgrade(Stat.Health, value);
        public void UpgradeFireRate([Optional] float value) => TryUpgrade(Stat.FireRate, value);
        public void UpgradeFireRange([Optional] float value) => TryUpgrade(Stat.FireRange, value);

        void TryUpgrade(Stat stat, float value)
        {
            if (stat == Stat.Health && !DamageSystem) { Debug.Log("There is no Damage component attached to the character to get the health value"); return; }
            if (IsMaxed(stat)) { Debug.Log($"{stat} already at max level."); return; }

            var wallet = Holder.Get<IntData>(CurrencyName);
            int cost = GetUpgradeCost(stat);
            if (wallet.Value >= cost)
            {
                wallet.Subtract(cost);
                ApplyUpgrade(stat, value);
            }
            else
            {
                AllMightyOne.AdsBridge.ShowRewarded("pistol_free_upgrade", ok => { if (ok && this) ApplyUpgrade(stat, value); });
            }
        }

        void ApplyUpgrade(Stat stat, float value)
        {
            switch (stat)
            {
                case Stat.Health:
                    value = value == 0 ? MaxHealth_IncreasePerLevel : value;
                    DamageSystem.UpdateMaxHealth(DamageSystem.MaxHealth + value);
                    MaxHealthText.text = DamageSystem.MaxHealth.ToString("0.00");
                    PlayerPrefs.SetFloat("CharacterGun_MaxHealth", DamageSystem.MaxHealth);
                    break;
                case Stat.FireRate:
                    FireRate += value == 0 ? FireRate_IncreasePerLevel : value;
                    FireRateText.text = FireRate.ToString("0.00");
                    PlayerPrefs.SetFloat("CharacterGun_FireRate", FireRate);
                    break;
                case Stat.FireRange:
                    FireRange += value == 0 ? FireRange_IncreasePerLevel : value;
                    FireRangeText.text = FireRange.ToString("0.00");
                    PlayerPrefs.SetFloat("CharacterGun_FireRange", FireRange);
                    break;
            }
            Holder.SaveToJson();
            OnUpgradeEvent?.Invoke();
            RefreshUpgradeUI();
        }

        bool IsMaxed(Stat stat) => stat switch
        {
            Stat.Health => !DamageSystem || DamageSystem.MaxHealth >= _maxHealth_maxLevel - 0.001f,
            Stat.FireRate => FireRate >= _fireRate_maxLevel - 0.001f,
            _ => FireRange >= _fireRange_maxLevel - 0.001f,
        };

        /// <summary> 1-based upgrade level of a stat, from its current value. </summary>
        public int GetUpgradeLevel(int statIndex) => GetUpgradeLevel((Stat)statIndex);

        int GetUpgradeLevel(Stat stat)
        {
            float current, start, step;
            switch (stat)
            {
                case Stat.Health: current = DamageSystem ? DamageSystem.MaxHealth : 0; start = _maxHealth_lvl1; step = MaxHealth_IncreasePerLevel; break;
                case Stat.FireRate: current = FireRate; start = _fireRate_lvl1; step = FireRate_IncreasePerLevel; break;
                default: current = FireRange; start = _fireRange_lvl1; step = FireRange_IncreasePerLevel; break;
            }
            return 1 + Mathf.Max(0, Mathf.RoundToInt((current - start) / Mathf.Max(0.0001f, step)));
        }

        int GetUpgradeCost(Stat stat) => Mathf.RoundToInt(CostPerUpgrade * Mathf.Pow(CostGrowth, GetUpgradeLevel(stat) - 1));

        void RefreshUpgradeUI()
        {
            int gems = Holder.Get<IntData>(CurrencyName).Value;
            RefreshCard(Stat.Health, CostDispaly_MaxHealth, OnMaxDisplay_MaxHealth, gems);
            RefreshCard(Stat.FireRate, CostDispaly_FireRate, OnMaxDisplay_FireRate, gems);
            RefreshCard(Stat.FireRange, CostDispaly_FireRange, OnMaxDisplay_FireRange, gems);
        }

        // Each card has siblings "Cost" (affordable), "Cost (Not Enough)" and a MAX display
        void RefreshCard(Stat stat, GameObject costDisplay, GameObject maxDisplay, int gems)
        {
            if (!costDisplay) return;
            var notEnough = costDisplay.transform.parent.Find("Cost (Not Enough)");
            bool maxed = IsMaxed(stat);
            int cost = GetUpgradeCost(stat);
            bool affordable = gems >= cost;

            if (maxDisplay) maxDisplay.SetActive(maxed);
            costDisplay.SetActive(!maxed && affordable);
            foreach (var t in costDisplay.GetComponentsInChildren<TMP_Text>(true)) t.text = cost.ToString();

            if (!notEnough) return;
            notEnough.gameObject.SetActive(!maxed && !affordable);
            foreach (var t in notEnough.GetComponentsInChildren<TMP_Text>(true))
            {
                t.text = FreeUpgradeText;
                t.enableAutoSizing = true;
                t.fontSizeMin = 8;
            }
            var gemIcon = notEnough.Find("Gem ing") ?? notEnough.Find("Gem img");
            if (gemIcon) gemIcon.gameObject.SetActive(false);
        }
        #endregion

        #region Collection: revive
        /// <summary> Revive button on the lose screen: rewarded ad, then continue the run. </summary>
        public void RequestRevive()
        {
            var gm = FindObjectOfType<UniversalGameManager>();
            if (gm && !gm.CanRevive) return;
            AllMightyOne.AdsBridge.ShowRewarded("pistol_revive", ok => { if (ok && this) Revive(); });
        }

        public void Revive()
        {
            if (!DamageSystem) return;
            if (DamageSystem.HealthBar) DamageSystem.HealthBar.gameObject.SetActive(true); // hidden on the killing hit; must be active before Revive() updates it
            DamageSystem.Revive();

            // undo the defeat animation (it shrinks the model)
            if (Animation && !string.IsNullOrEmpty(OnDefeatAnimation) && Animation[OnDefeatAnimation])
            {
                var state = Animation[OnDefeatAnimation];
                Animation.Play(OnDefeatAnimation);
                state.time = 0f;
                Animation.Sample();
                Animation.Stop();
            }

            foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                float dz = enemy.transform.position.z - transform.position.z;
                if (dz > -2f && dz < ReviveClearDistance) enemy.SetActive(false);
            }

            var gm = FindObjectOfType<UniversalGameManager>();
            if (gm) gm.Revive();
            Activate();
            StartCoroutine(Invulnerable());
        }

        System.Collections.IEnumerator Invulnerable()
        {
            bool trigger = DamageSystem.UseOnTrigger, collision = DamageSystem.UseOnCollision;
            DamageSystem.UseOnTrigger = DamageSystem.UseOnCollision = false;
            yield return new WaitForSeconds(ReviveInvulnerability);
            DamageSystem.UseOnTrigger = trigger;
            DamageSystem.UseOnCollision = collision;
        }
        #endregion
    }
}
