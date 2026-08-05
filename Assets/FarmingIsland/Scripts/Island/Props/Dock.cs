using UnityEngine;
using Cinemachine;
using DG.Tweening;
using System.Collections;

namespace CryingSnow.FarmingIsland
{
    public class Dock : MonoBehaviour, IProp
    {
        [SerializeField] private Transform skipper;
        [SerializeField] private CinemachineVirtualCamera virtualCamera;

        Location IProp.Location => Location.Dock;

        void Awake()
        {
            transform.localScale = Vector3.zero;
            skipper.localScale = Vector3.zero;
        }

        void IProp.Animate(bool instant, System.Action onFinished)
        {
            if (instant)
            {
                transform.localScale = Vector3.one;
                skipper.localScale = Vector3.one;
                return;
            }

            transform.DOScale(Vector3.one, 1f)
                .SetEase(Ease.OutBounce)
                .OnComplete(() =>
                {
                    skipper.DOScale(Vector3.one, 0.5f)
                        .SetEase(Ease.OutBounce);
                });
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                var player = other.GetComponent<PlayerController>();
                StartCoroutine(RideBoat(player));
            }
        }

        IEnumerator RideBoat(PlayerController player)
        {
            virtualCamera.gameObject.SetActive(true);
            player.IsControllable = false;
            UIManager.Instance.ToggleHUD(false);

            yield return new WaitForSeconds(1f);

            UIManager.Instance.Minimap.Show(true);

            yield return new WaitUntil(() => player.IsControllable);

            virtualCamera.gameObject.SetActive(false);
            UIManager.Instance.ToggleHUD(true);
        }

    }
}
