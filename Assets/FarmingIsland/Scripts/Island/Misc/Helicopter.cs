using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class Helicopter : MonoBehaviour
    {
        [Tooltip("Reference to the helicopter's main propeller.")]
        [SerializeField] private Transform mainPropeler;

        [Tooltip("Reference to the helicopter's rear propeller.")]
        [SerializeField] private Transform rearPropeler;

        private void Start()
        {
            // Animate the main propeller to rotate continuously around the Y-axis
            mainPropeler.DOLocalRotate(Vector3.up * 180f, 0.15f) // Rotates 180 degrees around Y-axis
                .SetEase(Ease.Linear) // Smooth and constant rotation
                .SetLoops(-1, LoopType.Incremental); // Infinite looping with cumulative rotation

            // Animate the rear propeller to rotate continuously around the X-axis
            rearPropeler.DOLocalRotate(Vector3.right * 180f, 0.15f) // Rotates 180 degrees around X-axis
                .SetEase(Ease.Linear) // Smooth and constant rotation
                .SetLoops(-1, LoopType.Incremental); // Infinite looping with cumulative rotation
        }
    }
}
