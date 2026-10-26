using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;

namespace AllMightyOne.Dodge
{
    /// <summary>
    /// Bull Dodge meta: pays gems for the score when the run ends (1 gem per PointsPerGem; a revived run only pays the new part),
    /// shows score / best / gems on the end screen with a rewarded x3, and removes bulls that left the arena.
    /// </summary>
    public class DodgeMeta : MonoBehaviour
    {
        public CharacterControl_Dodge Character;
        public MOST_Spawn Spawner;
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        [Min(1)] public int PointsPerGem = 10;
        [Min(2)] public int RewardMultiplier = 3;
        [Tooltip("Bulls running away from the centre are removed past this distance.")]
        [Min(10)] public float CleanupRadius = 50f;

        [Header("End screen")]
        public TMP_Text ScoreText;
        public TMP_Text BestText;
        public TMP_Text GemsText;
        public GameObject TripleRewardButton;

        int _paidGems;
        bool _rewardClaimed;

        /// <summary> Gems paid by the last run end (after the rewarded multiplier, if taken). </summary>
        public int LastEarned { get; private set; }

        /// <summary> Wired to the game manager's OnLoseEvent. </summary>
        public void OnRunEnded()
        {
            var score = Database.Get<IntData>(Character.ScoreDataName);
            int total = score.Value / PointsPerGem;
            LastEarned = Mathf.Max(0, total - _paidGems);
            _paidGems = total;
            _rewardClaimed = false;
            if (LastEarned > 0) Database.Get<IntData>(CurrencyName).Add(LastEarned);
            Database.SaveToJson();
            CollectionAnalytics.RunEnd("dodge", score.Value);

            if (ScoreText) ScoreText.text = $"SCORE {score.Value}";
            if (BestText) BestText.text = score.Value >= score.HighestValue && score.Value > 0 ? "NEW BEST!" : $"BEST {score.HighestValue}";
            if (GemsText) GemsText.text = $"+{LastEarned} GEMS";
            if (TripleRewardButton) TripleRewardButton.SetActive(LastEarned > 0);
        }

        /// <summary> Rewarded ad, then multiplies the gems paid by this run end. </summary>
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
                if (GemsText) GemsText.text = $"+{LastEarned} GEMS";
                if (TripleRewardButton) TripleRewardButton.SetActive(false);
            });
        }

        void Update()
        {
            if (!Spawner) return;
            var bulls = Spawner.GetAllActiveOjectsInScene();
            for (int i = bulls.Count - 1; i >= 0; i--)
            {
                var p = bulls[i].transform.position;
                p.y = 0;
                // ForwardMovement has no lifetime: without this every bull ever spawned keeps running off-screen
                if (p.sqrMagnitude > CleanupRadius * CleanupRadius && Vector3.Dot(bulls[i].transform.forward, p) > 0)
                    Destroy(bulls[i]);
            }
        }
    }
}
