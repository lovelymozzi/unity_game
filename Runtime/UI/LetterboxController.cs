using UnityEngine;
using UnityEngine.UI;

namespace Hwi.Foundation.UI
{
    /// <summary>
    /// 화면 비율이 Reference와 다를 때 검은 띠를 만들어 reference aspect를 유지.
    /// Canvas 최상단(Screen Space - Overlay)에 풀스크린 Image 2개 (top/bottom 또는 left/right)를 active/sizing 한다.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class LetterboxController : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);
        [SerializeField] private Color barColor = Color.black;
        [SerializeField] private bool enableLetterbox = false;

        private Image _barA;
        private Image _barB;
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);

        private void Awake()
        {
            if (!enableLetterbox) return;
            CreateBars();
            ApplyBars();
        }

        private void OnEnable()
        {
            if (!enableLetterbox) return;
            if (_barA == null) CreateBars();
            ApplyBars();
        }

        private void Update()
        {
            if (!enableLetterbox) return;
            if (Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y) ApplyBars();
        }

        private void CreateBars()
        {
            _barA = MakeBar("LetterboxBarA");
            _barB = MakeBar("LetterboxBarB");
        }

        private Image MakeBar(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.color = barColor;
            img.raycastTarget = false;
            return img;
        }

        private void ApplyBars()
        {
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (_barA == null || _barB == null) return;

            float refAspect = referenceResolution.x / referenceResolution.y;
            float screenAspect = (float)Screen.width / Screen.height;
            if (Mathf.Approximately(refAspect, screenAspect))
            {
                _barA.gameObject.SetActive(false);
                _barB.gameObject.SetActive(false);
                return;
            }

            _barA.gameObject.SetActive(true);
            _barB.gameObject.SetActive(true);

            var rectA = (RectTransform)_barA.transform;
            var rectB = (RectTransform)_barB.transform;

            if (screenAspect > refAspect)
            {
                // 화면이 더 넓다 → 좌우 검은 띠
                float barFrac = (screenAspect - refAspect) / screenAspect * 0.5f;
                rectA.anchorMin = new Vector2(0, 0);
                rectA.anchorMax = new Vector2(barFrac, 1);
                rectA.offsetMin = rectA.offsetMax = Vector2.zero;
                rectB.anchorMin = new Vector2(1f - barFrac, 0);
                rectB.anchorMax = new Vector2(1, 1);
                rectB.offsetMin = rectB.offsetMax = Vector2.zero;
            }
            else
            {
                // 화면이 더 길다 → 상하 검은 띠
                float barFrac = (refAspect - screenAspect) / refAspect * 0.5f;
                rectA.anchorMin = new Vector2(0, 1f - barFrac);
                rectA.anchorMax = new Vector2(1, 1);
                rectA.offsetMin = rectA.offsetMax = Vector2.zero;
                rectB.anchorMin = new Vector2(0, 0);
                rectB.anchorMax = new Vector2(1, barFrac);
                rectB.offsetMin = rectB.offsetMax = Vector2.zero;
            }
        }
    }
}
