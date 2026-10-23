using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;

namespace AllMightyOne.Hub
{
    /// <summary> The shared gem balance in a game's top corner, for the games whose kit UI has no wallet. </summary>
    public class GemCounter : MonoBehaviour
    {
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        public TMP_Text Text;

        int _shown = int.MinValue;

        void Update()
        {
            int gems = Database ? Database.Get<IntData>(CurrencyName)?.Value ?? 0 : 0;
            if (gems == _shown || !Text) return;
            _shown = gems;
            Text.text = gems.ToString("N0");
        }
    }
}
