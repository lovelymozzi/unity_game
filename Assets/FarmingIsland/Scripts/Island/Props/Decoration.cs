using UnityEngine;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class Decoration : MonoBehaviour, IProp
    {
        public Location Location => Location.None;

        private void Awake()
        {
            transform.localScale = Vector3.zero;
        }

        public void Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                transform.localScale = Vector3.one;
                onFinished?.Invoke();
            }
            else
            {
                transform.DOScale(Vector3.one, 1f)
                    .SetEase(Ease.OutBounce)
                    .OnComplete(() => onFinished?.Invoke());
            }
        }
    }
}
