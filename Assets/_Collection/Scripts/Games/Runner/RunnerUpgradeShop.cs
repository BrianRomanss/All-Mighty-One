using System;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Runner
{
    /// <summary>
    /// Persistent meta upgrades bought on a runner's menu screen. Each card pays with gems; when the player cannot afford it,
    /// the card offers the upgrade for a rewarded ad. Card visuals follow the kit's upgrade card layout:
    /// "CurrentAmount text", "Cost" (affordable), "Cost (Not Enough)" and "OnMaxout".
    /// </summary>
    public class RunnerUpgradeShop : MonoBehaviour
    {
        [Serializable]
        public class Card
        {
            public string Id = "upgrade";
            public Button Button;
            [Min(1)] public int MaxLevel = 10;
            [Min(0)] public int BaseCost = 40;
            [Min(1f)] public float CostGrowth = 1.35f;
            [Tooltip("Value at level 1; each level adds ValuePerLevel.")]
            public float BaseValue = 1;
            public float ValuePerLevel = 1;
            public string ValueFormat = "0";
            [Tooltip("The card shows value x this (0 counts as 1), e.g. head scale 2.5 x 40 = IQ 100.")]
            public float DisplayMultiplier = 1;
        }

        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        [Tooltip("PlayerPrefs key prefix, unique per game.")]
        public string SaveKeyPrefix = "AMO_Upgrade_";
        public string FreeUpgradeText = "FREE (AD)";
        public List<Card> Cards = new();

        /// <summary> Raised after a card levels up (card id, new level). </summary>
        public event Action<string, int> Upgraded;

        void Awake()
        {
            foreach (var card in Cards)
            {
                var c = card;
                if (c.Button) c.Button.onClick.AddListener(() => Buy(c.Id));
            }
        }

        void OnEnable() => Refresh();

        public int GetLevel(string id) => Mathf.Max(1, PlayerPrefs.GetInt(SaveKeyPrefix + id, 1));
        public float GetValue(string id) { var c = Find(id); return c == null ? 0 : c.BaseValue + c.ValuePerLevel * (GetLevel(id) - 1); }
        public int GetCost(string id) { var c = Find(id); return c == null ? 0 : Mathf.RoundToInt(c.BaseCost * Mathf.Pow(c.CostGrowth, GetLevel(id) - 1)); }
        public bool IsMaxed(string id) { var c = Find(id); return c == null || GetLevel(id) >= c.MaxLevel; }

        Card Find(string id) => Cards.Find(c => c.Id == id);
        IntData Wallet => Database ? Database.Get<IntData>(CurrencyName) : null;

        public void Buy(string id)
        {
            if (IsMaxed(id)) return;
            var wallet = Wallet;
            int cost = GetCost(id);
            if (wallet != null && wallet.Value >= cost)
            {
                wallet.Subtract(cost);
                Database.SaveToJson();
                LevelUp(id);
            }
            else AdsBridge.ShowRewarded($"free_upgrade_{id}", ok => { if (ok && this) LevelUp(id); });
        }

        void LevelUp(string id)
        {
            int level = GetLevel(id) + 1;
            PlayerPrefs.SetInt(SaveKeyPrefix + id, level);
            PlayerPrefs.Save();
            Refresh();
            Upgraded?.Invoke(id, level);
        }

        public void Refresh()
        {
            int gems = Wallet?.Value ?? 0;
            foreach (var c in Cards)
            {
                if (!c.Button) continue;
                var t = c.Button.transform;
                bool maxed = IsMaxed(c.Id);
                int cost = GetCost(c.Id);
                bool affordable = gems >= cost;

                SetText(t.Find("CurrentAmount text"), (GetValue(c.Id) * (c.DisplayMultiplier > 0 ? c.DisplayMultiplier : 1)).ToString(c.ValueFormat));
                var max = t.Find("OnMaxout");
                if (max) max.gameObject.SetActive(maxed);

                var costRoot = t.Find("Cost");
                if (costRoot)
                {
                    costRoot.gameObject.SetActive(!maxed && affordable);
                    foreach (var txt in costRoot.GetComponentsInChildren<TMP_Text>(true)) txt.text = cost.ToString();
                }
                var notEnough = t.Find("Cost (Not Enough)");
                if (notEnough)
                {
                    notEnough.gameObject.SetActive(!maxed && !affordable);
                    foreach (var txt in notEnough.GetComponentsInChildren<TMP_Text>(true))
                    {
                        txt.text = FreeUpgradeText;
                        txt.enableAutoSizing = true;
                        txt.fontSizeMin = 8;
                    }
                    var gem = notEnough.Find("Gem ing") ?? notEnough.Find("Gem img");
                    if (gem) gem.gameObject.SetActive(false);
                }
            }
        }

        static void SetText(Transform t, string value)
        {
            if (t && t.TryGetComponent<TMP_Text>(out var txt)) txt.text = value;
        }
    }
}
