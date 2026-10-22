using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;

namespace AllMightyOne.ThreeRoads
{
    /// <summary>
    /// 3 Roads meta: the gems picked up on the track are counted per run end (a revived run only counts the new ones),
    /// shown on the end screen with a rewarded x3.
    /// </summary>
    public class ThreeRoadsMeta : MonoBehaviour
    {
        public ThreeRoadsController Controller;
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        [Min(2)] public int RewardMultiplier = 3;

        [Header("End screen")]
        public TMP_Text GemsText;
        public GameObject TripleRewardButton;

        int _gemsAtLastEnd;
        bool _rewardClaimed;

        /// <summary> Gems picked up since the run started or was revived (after the rewarded multiplier, if taken). </summary>
        public int LastEarned { get; private set; }

        IntData Gems => Database.Get<IntData>(CurrencyName);

        /// <summary> Wired to the controller's OnGameStart. </summary>
        public void OnRunStarted() => _gemsAtLastEnd = Gems.Value;

        /// <summary> Wired to the game manager's OnLoseEvent. </summary>
        public void OnRunEnded()
        {
            LastEarned = Mathf.Max(0, Gems.Value - _gemsAtLastEnd);
            _gemsAtLastEnd = Gems.Value;
            _rewardClaimed = false;
            Database.SaveToJson();
            if (GemsText) GemsText.text = $"+{LastEarned} GEMS";
            if (TripleRewardButton) TripleRewardButton.SetActive(LastEarned > 0);
        }

        /// <summary> Rewarded ad, then multiplies the gems picked up in this run. </summary>
        public void RequestTripleReward()
        {
            if (_rewardClaimed || LastEarned <= 0) return;
            AdsBridge.ShowRewarded("triple_reward", ok =>
            {
                if (!ok || !this || _rewardClaimed) return;
                _rewardClaimed = true;
                Gems.Add(LastEarned * (RewardMultiplier - 1));
                _gemsAtLastEnd = Gems.Value; // the bonus is not counted again after a revive
                Database.SaveToJson();
                LastEarned *= RewardMultiplier;
                if (GemsText) GemsText.text = $"+{LastEarned} GEMS";
                if (TripleRewardButton) TripleRewardButton.SetActive(false);
            });
        }
    }
}
