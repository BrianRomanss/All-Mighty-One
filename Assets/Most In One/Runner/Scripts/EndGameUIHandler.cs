using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Solo.MOST_IN_ONE
{
    [HideScriptField]
    public class EndGameUIHandler : MonoBehaviour
    {
        public MOST_Database DataHolder;
        public string ScoreMultiplyerDataName;
        public string CurrentScoreDataName;
        [Line]
        public Text ScoreText;
        public Text MultiplyerText;
        public GameObject ContinueButton;
        public float DelayBeforeStart = 3;

        [BigHeader("Collection")]
        [InnerHint("(Optional)"), Tooltip("Rewarded \"x3\" button, shown together with the Continue button.")]
        public GameObject TripleRewardButton;
        [Min(2)] public int RewardMultiplier = 3;

        int _currentScore, _totalScore;
        bool _rewardClaimed;

        /// <summary> Currency earned by the last finished run (after the end multiplier). </summary>
        public int LastEarned { get; private set; }
        public bool CountFinished { get; private set; }

        /// <summary> Rewarded ad, then multiplies the gems earned this run. </summary>
        public void RequestTripleReward()
        {
            if (_rewardClaimed || !CountFinished || LastEarned <= 0) return;
            AllMightyOne.AdsBridge.ShowRewarded("triple_reward", ok =>
            {
                if (!ok || !this || _rewardClaimed) return;
                _rewardClaimed = true;
                DataHolder.Get<IntData>(CurrentScoreDataName).Add(LastEarned * (RewardMultiplier - 1));
                DataHolder.SaveToJson();
                LastEarned *= RewardMultiplier;
                ScoreText.text = LastEarned.ToString();
                if (TripleRewardButton) TripleRewardButton.SetActive(false);
            });
        }

        public void RecordStartScore()
        {
            _currentScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value;
            if(!string.IsNullOrEmpty(ScoreMultiplyerDataName)) DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value = .8f;
        }

        public void StartCount()
        {
            StartCoroutine(ScoreCount());
        }

        IEnumerator ScoreCount()
        {
            yield return new WaitForSeconds(Time.deltaTime);
            if (MultiplyerText && !string.IsNullOrEmpty(ScoreMultiplyerDataName))
            {
                _totalScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value - _currentScore;
                ScoreText.text = _totalScore.ToString();
                MultiplyerText.text = DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value.ToString("0.0");
                _currentScore = (int)(DataHolder.Get<FloatData>(ScoreMultiplyerDataName).Value * _totalScore) + 1;
                DataHolder.Get<IntData>(CurrentScoreDataName).Add(_currentScore);
            }
            else
            {
                _currentScore = DataHolder.Get<IntData>(CurrentScoreDataName).Value - _currentScore;
                _totalScore = 0;
                ScoreText.text = _totalScore.ToString();

            }
            ContinueButton.SetActive(false);
            if (TripleRewardButton) TripleRewardButton.SetActive(false);
            LastEarned = Mathf.Max(0, _currentScore);
            CountFinished = false;
            yield return new WaitForSeconds(DelayBeforeStart);
            while(_totalScore < _currentScore)
            {
                yield return new WaitForSeconds(.01f);
                _totalScore += Mathf.Max(1, (_currentScore - _totalScore) / 60); // large rewards count up in ~1s instead of 1 per frame
                ScoreText.text = Mathf.Min(_totalScore, _currentScore).ToString();
            }
            ContinueButton.SetActive(true);
            CountFinished = true;
            if (TripleRewardButton) TripleRewardButton.SetActive(LastEarned > 0 && !_rewardClaimed);
        }
    }
}