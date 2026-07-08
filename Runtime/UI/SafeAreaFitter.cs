using UnityEngine;

namespace Hwi.Foundation.UI
{
    /// <summary>
    /// RectTransform을 Screen.safeArea에 맞춰 자동 조정. 회전·노치·펀치홀 대응.
    /// Canvas의 자식 RectTransform에 부착. anchors는 본 컴포넌트가 덮어쓴다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool conformX = true;
        [SerializeField] private bool conformY = true;

        private RectTransform _rect;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply(ForceUpdate: true);
        }

        private void OnEnable()
        {
            Apply(ForceUpdate: true);
        }

        private void Update()
        {
            // 화면 회전/크기 변경 감지 시에만 갱신 (매 프레임 RectTransform 변경 비용 회피)
            if (HasChanged()) Apply(ForceUpdate: false);
        }

        private bool HasChanged()
        {
            return Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenSize.x
                || Screen.height != _lastScreenSize.y
                || Screen.orientation != _lastOrientation;
        }

        private void Apply(bool ForceUpdate)
        {
            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            if (Screen.width == 0 || Screen.height == 0) return;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!conformX) { anchorMin.x = 0; anchorMax.x = 1; }
            if (!conformY) { anchorMin.y = 0; anchorMax.y = 1; }

            _rect.anchorMin = anchorMin;
            _rect.anchorMax = anchorMax;
        }

#if UNITY_EDITOR
        // Editor에서 인스펙터 값 변경 시 즉시 반영
        private void OnValidate()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect != null) Apply(ForceUpdate: true);
        }
#endif
    }
}
