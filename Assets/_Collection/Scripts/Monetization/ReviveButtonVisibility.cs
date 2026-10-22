using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Put on a "Continue (AD)" button inside a lose panel: it only shows while a revive is still available. </summary>
    public class ReviveButtonVisibility : MonoBehaviour
    {
        UniversalGameManager _gm;

        void OnEnable()
        {
            if (!_gm) _gm = FindObjectOfType<UniversalGameManager>();
            if (_gm && !_gm.CanRevive) gameObject.SetActive(false);
        }
    }
}
