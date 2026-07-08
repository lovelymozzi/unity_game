using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Hwi.Foundation.Assets;

namespace Hwi.Foundation.Samples.AssetsGroup
{
    public class AssetsGroupController : MonoBehaviour
    {
        [SerializeField] private Button loadButton;
        [SerializeField] private Button releaseButton;
        [SerializeField] private Image iconA;
        [SerializeField] private Image iconB;
        [SerializeField] private Text statusText;

        private AssetGroup _group;
        private static readonly string[] Keys = { "sample05/icon_a", "sample05/icon_b", "sample05/mat_simple" };

        private void Start()
        {
            loadButton.onClick.AddListener(() => LoadAll().Forget());
            releaseButton.onClick.AddListener(ReleaseAll);
            UpdateUi(loaded: false);
        }

        private async UniTaskVoid LoadAll()
        {
            ReleaseAll();
            _group = new AssetGroup(releaseOnLowMemory: true);

            var rA = await _group.LoadAsync<Sprite>(Keys[0]);
            var rB = await _group.LoadAsync<Sprite>(Keys[1]);
            var rM = await _group.LoadAsync<Material>(Keys[2]);

            if (!rA.IsOk || !rB.IsOk || !rM.IsOk)
            {
                statusText.text = $"FAIL: {rA.Error} / {rB.Error} / {rM.Error}";
                return;
            }
            iconA.sprite = rA.Value;
            iconB.sprite = rB.Value;
            UpdateUi(loaded: true);
        }

        private void ReleaseAll()
        {
            _group?.Dispose();
            _group = null;
            iconA.sprite = null;
            iconB.sprite = null;
            UpdateUi(loaded: false);
        }

        private void UpdateUi(bool loaded)
        {
            statusText.text = loaded ? "Loaded (Group active)" : "Released";
            releaseButton.interactable = loaded;
        }

        private void OnDestroy() => ReleaseAll();
    }
}
