using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary> One game tile in the hub grid (also used for the wide "continue" card). </summary>
    public class HubCard : MonoBehaviour
    {
        public Button Button;
        [Tooltip("Tinted with the game's theme colour.")]
        public Image Face;
        [Tooltip("Tinted with a darker theme colour (the chunky 3D edge).")]
        public Image Base;
        public Image Icon;
        [Tooltip("Shown when the game has no icon yet: the game's initials.")]
        public TMP_Text Initials;
        public TMP_Text Title, Tagline, Progress;
        public Image ProgressPill;
        public GameObject LockRoot, NewBadge;
        public TMP_Text CostText;

        public GameEntry Entry { get; private set; }

        public void Bind(GameEntry entry, GameCatalog catalog, System.Action<GameEntry> onClick)
        {
            Entry = entry;
            name = $"Card {entry.Id}";
            var dark = Shade(entry.Theme, 0.68f);
            if (Face) Face.color = entry.Theme;
            if (Base) Base.color = dark;
            if (ProgressPill) ProgressPill.color = new Color(dark.r * 0.8f, dark.g * 0.8f, dark.b * 0.8f, 0.9f);
            if (Title) Title.text = entry.DisplayName.ToUpperInvariant();
            if (Tagline) Tagline.text = entry.Tagline;
            if (Icon)
            {
                Icon.sprite = entry.Icon;
                Icon.enabled = entry.Icon;
            }
            if (Initials)
            {
                Initials.gameObject.SetActive(!entry.Icon);
                Initials.text = InitialsOf(entry.DisplayName);
                Initials.color = Color.Lerp(entry.Theme, Color.white, 0.35f);
            }
            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(() => onClick(entry));
            Refresh(catalog);
        }

        public void Refresh(GameCatalog catalog)
        {
            if (Entry == null) return;
            bool unlocked = catalog.IsUnlocked(Entry);
            if (Progress) Progress.text = unlocked ? catalog.ProgressText(Entry) : GameCatalog.CategoryName(Entry.Category);
            if (LockRoot) LockRoot.SetActive(!unlocked);
            if (CostText) CostText.text = Entry.UnlockCost.ToString();
            if (NewBadge) NewBadge.SetActive(unlocked && !GameCatalog.WasPlayed(Entry));
        }

        public static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static string InitialsOf(string name)
        {
            var words = name.Split(' ');
            if (words.Length == 1) return name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
            return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
        }
    }
}
