using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne.Runner
{
    /// <summary>
    /// Big Brain meta: the persistent "brain" upgrade sets the starting head size; BiggerStart() is the one-run
    /// rewarded head start shown on the menu.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BigBrainMeta : MonoBehaviour
    {
        public CharacterControl_BigBrain Runner;
        public RunnerUpgradeShop Shop;
        public string BrainId = "brain";
        [Tooltip("Head scale added by the rewarded head start (a +20 gate adds 1).")]
        [Min(0.1f)] public float BiggerStartScale = 2f;
        [Tooltip("Hidden once the head start was used or the run started.")]
        public GameObject BiggerStartButton;

        void Awake()
        {
            if (!Runner || !Shop) return;
            Runner.StartScale = Shop.GetValue(BrainId);
            Shop.Upgraded += OnUpgraded;
        }

        void OnDestroy()
        {
            if (Shop) Shop.Upgraded -= OnUpgraded;
        }

        void OnUpgraded(string id, int level)
        {
            if (id != BrainId || Runner.IsActive) return;
            float target = Shop.GetValue(BrainId);
            Runner.SetHeadScale(Runner.HeadScale + target - Runner.StartScale); // keeps a head start already granted
            Runner.StartScale = target;
        }

        /// <summary> Granted by the rewarded "BIGGER START" button: a bigger head for this run only. </summary>
        public void BiggerStart()
        {
            if (Runner.IsActive) return;
            Runner.SetHeadScale(Runner.HeadScale + BiggerStartScale);
            if (BiggerStartButton) BiggerStartButton.SetActive(false);
        }
    }
}
