using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne.Runner
{
    /// <summary>
    /// Applies the Squad Runner meta upgrades: "squad" = starting soldier count, "tier" = starting soldier tier.
    /// Runs before the squad's Start() so the first soldiers already spawn with the upgraded values.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SquadRunnerMeta : MonoBehaviour
    {
        public CharacterControl_ShootRunner Runner;
        public RunnerUpgradeShop Shop;
        public string SquadId = "squad";
        public string TierId = "tier";

        void Awake()
        {
            if (!Runner || !Shop) return;
            Runner.StartAmount = Mathf.Max(1, Mathf.RoundToInt(Shop.GetValue(SquadId)));
            SetTier(Mathf.RoundToInt(Shop.GetValue(TierId)));
            Shop.Upgraded += OnUpgraded;
        }

        void OnDestroy()
        {
            if (Shop) Shop.Upgraded -= OnUpgraded;
        }

        // Upgrades bought on the menu show up on the squad standing there right away
        void OnUpgraded(string id, int level)
        {
            if (Runner.IsActive) return;
            if (id == SquadId)
            {
                int target = Mathf.RoundToInt(Shop.GetValue(SquadId));
                Runner.StartAmount = target;
                if (target > Runner.ChildHolder.childCount) Runner.InstantAddChilds(target - Runner.ChildHolder.childCount);
            }
            else if (id == TierId) SetTier(Mathf.RoundToInt(Shop.GetValue(TierId)));
        }

        void SetTier(int tier)
        {
            tier = Mathf.Clamp(tier, 1, Runner.ChildPrefabs.Length);
            if (tier == Runner.CurrentLevel) return;
            Runner.CurrentLevel = tier;
            // swap soldiers already standing in the holder for the new tier
            var holder = Runner.ChildHolder;
            for (int i = holder.childCount - 1; i >= 0; i--)
            {
                var old = holder.GetChild(i);
                var pos = old.position;
                old.SetParent(null);
                Destroy(old.gameObject);
                Instantiate(Runner.ChildPrefabs[tier - 1], pos, Quaternion.identity, holder);
            }
            if (holder.childCount > 0) Runner.UpdateIdlePositions();
        }
    }
}
