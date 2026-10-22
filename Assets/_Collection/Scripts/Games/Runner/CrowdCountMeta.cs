using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne.Runner
{
    /// <summary>
    /// Crowd Count meta: the persistent "crowd" upgrade sets the starting crowd; BiggerStart() is the one-run
    /// rewarded head start shown on the menu.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CrowdCountMeta : MonoBehaviour
    {
        public CharacterControl_Count Runner;
        public RunnerUpgradeShop Shop;
        public string CrowdId = "crowd";
        [Min(1)] public int BiggerStartAmount = 25;
        [Tooltip("Hidden once the head start was used or the run started.")]
        public GameObject BiggerStartButton;

        void Awake()
        {
            if (!Runner || !Shop) return;
            Runner.StartAmount = Mathf.Max(1, Mathf.RoundToInt(Shop.GetValue(CrowdId)));
            Shop.Upgraded += OnUpgraded;
        }

        void OnDestroy()
        {
            if (Shop) Shop.Upgraded -= OnUpgraded;
        }

        void OnUpgraded(string id, int level)
        {
            if (id != CrowdId || Runner.IsActive) return;
            int target = Mathf.RoundToInt(Shop.GetValue(CrowdId));
            if (target > Runner.ChildHolder.childCount) Runner.InstantAddChilds(target - Runner.ChildHolder.childCount);
            Runner.StartAmount = target;
        }

        /// <summary> Granted by the rewarded "BIGGER START" button: extra crowd for this run only. </summary>
        public void BiggerStart()
        {
            if (Runner.IsActive) return;
            Runner.InstantAddChilds(BiggerStartAmount);
            if (BiggerStartButton) BiggerStartButton.SetActive(false);
        }
    }
}
