using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary> One line of the missions popup: what to do, a progress bar, and the reward button. </summary>
    public class HubMissionRow : MonoBehaviour
    {
        public TMP_Text Text, ProgressText, RewardText;
        public Image ProgressFill;
        public Button ClaimButton;
        public Image ClaimFace;
        public GameObject DoneMark;
        public Color ReadyColor = new(0.26f, 0.8f, 0.33f), WaitingColor = new(0.7f, 0.68f, 0.82f);

        public Mission Mission { get; private set; }

        public void Show(Mission m)
        {
            Mission = m;
            Text.text = m.Text;
            ProgressText.text = $"{m.Progress}/{m.Target}";
            // the fill is a rounded bar stretched from the left: its right anchor follows the progress
            float k = (float)m.Progress / m.Target;
            ProgressFill.gameObject.SetActive(k > 0f);
            ProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.12f, k), 1f);
            RewardText.text = m.Reward.ToString();
            ClaimButton.gameObject.SetActive(!m.Claimed);
            ClaimButton.interactable = m.Done;
            if (ClaimFace) ClaimFace.color = m.Done ? ReadyColor : WaitingColor;
            if (DoneMark) DoneMark.SetActive(m.Claimed);
        }
    }
}
