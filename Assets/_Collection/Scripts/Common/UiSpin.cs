using UnityEngine;

namespace AllMightyOne
{
    /// <summary> Spins a UI element (light rays). </summary>
    public class UiSpin : MonoBehaviour
    {
        public float DegreesPerSecond = 14f;
        void Update() => transform.Rotate(0, 0, -DegreesPerSecond * Time.unscaledDeltaTime);
    }
}
