using UnityEngine;

namespace Hwi.Foundation.UI
{
    /// <summary>
    /// Orthographic 카메라의 <c>orthographicSize</c>를 다양한 화면비에 맞춰 동적으로 조정한다.
    /// 프로젝트 표준(모든 게임 오브젝트 = SpriteRenderer, 좌표/화면 = MainCamera Orthographic Size 기반 동적 대응)의
    /// 월드 버전 — uGUI의 <see cref="CanvasScalerPreset"/>가 Canvas를 맞추듯 이 컴포넌트는 월드 뷰포트를 맞춘다.
    ///
    /// 설계 기준 월드 영역(<see cref="referenceWorldSize"/>, 단위=월드 유닛)을 정하고, 모드에 따라
    /// 그 영역이 항상 보이도록(Fit) 또는 항상 화면을 채우도록(Envelope) orthographicSize를 계산한다.
    /// 외부 패키지 의존 없음. Camera 하나에 부착.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class OrthographicCameraFitter : MonoBehaviour
    {
        public enum FitMode
        {
            /// <summary>기준 영역 전체가 보이도록(레터박스형). 잘림 없음.</summary>
            Fit,
            /// <summary>기준 영역이 화면을 꽉 채우도록(크롭형). 여백 없음.</summary>
            Envelope,
            /// <summary>기준 폭을 항상 맞춤(세로는 화면비 따라 가변).</summary>
            FitWidth,
            /// <summary>기준 높이를 항상 맞춤(가로는 화면비 따라 가변).</summary>
            FitHeight,
        }

        [Tooltip("설계 기준 월드 영역 크기 (월드 유닛). 세로 모바일 기준 예: (10.8, 19.2).")]
        [SerializeField] private Vector2 referenceWorldSize = new Vector2(10.8f, 19.2f);

        [SerializeField] private FitMode mode = FitMode.Fit;

        [Tooltip("계산된 orthographicSize의 하한(0 이하면 미적용).")]
        [SerializeField] private float minOrthographicSize = 0f;

        private Camera _camera;
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private FitMode _lastMode;
        private Vector2 _lastReference;

        public Vector2 ReferenceWorldSize
        {
            get => referenceWorldSize;
            set { referenceWorldSize = value; Apply(force: true); }
        }

        public FitMode Mode
        {
            get => mode;
            set { mode = value; Apply(force: true); }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Apply(force: true);
        }

        private void OnEnable() => Apply(force: true);

        private void Update()
        {
            // 화면 크기/설정 변경 시에만 재계산 (매 프레임 orthographicSize 대입 비용 회피)
            if (HasChanged()) Apply(force: false);
        }

        private bool HasChanged()
        {
            return Screen.width != _lastScreenSize.x
                || Screen.height != _lastScreenSize.y
                || mode != _lastMode
                || referenceWorldSize != _lastReference;
        }

        private void Apply(bool force)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null || Screen.width == 0 || Screen.height == 0) return;

            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastMode = mode;
            _lastReference = referenceWorldSize;

            if (!_camera.orthographic) return; // perspective 카메라는 대상 아님

            float aspect = (float)Screen.width / Screen.height;
            float size = ComputeOrthographicSize(mode, referenceWorldSize, aspect);
            if (minOrthographicSize > 0f) size = Mathf.Max(size, minOrthographicSize);
            if (size > 0f) _camera.orthographicSize = size;
        }

        /// <summary>
        /// 순수 계산부(테스트 가능). orthographicSize = 화면에 보이는 월드 세로 높이의 절반.
        /// 가로 절반 = size * aspect.
        /// </summary>
        public static float ComputeOrthographicSize(FitMode mode, Vector2 referenceWorldSize, float aspect)
        {
            if (aspect <= 0f) return referenceWorldSize.y * 0.5f;

            float sizeForHeight = referenceWorldSize.y * 0.5f;             // 세로를 맞추는 size
            float sizeForWidth = (referenceWorldSize.x * 0.5f) / aspect;   // 가로를 맞추는 size

            switch (mode)
            {
                case FitMode.FitWidth:  return sizeForWidth;
                case FitMode.FitHeight: return sizeForHeight;
                case FitMode.Envelope:  return Mathf.Min(sizeForHeight, sizeForWidth);
                case FitMode.Fit:
                default:                return Mathf.Max(sizeForHeight, sizeForWidth);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            Apply(force: true);
        }
#endif
    }
}
