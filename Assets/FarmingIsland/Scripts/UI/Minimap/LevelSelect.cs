using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CryingSnow.FarmingIsland
{
    public class LevelSelect : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("The icon representing the level.")]
        [SerializeField] private Image levelIcon;

        [Tooltip("The sprite used when the level is locked.")]
        [SerializeField] private Sprite lockSprite;

        private bool interactable;
        private string sceneName;

        private void Start()
        {
            // Determine the level index based on the sibling order of the UI element
            int levelIndex = transform.GetSiblingIndex() + 1;
            sceneName = $"Level{levelIndex:D2}";

            // Enable interaction if this is the first level or if progress for this level already exists
            if (levelIndex == 1 || SaveSystem.SaveFileExists(sceneName))
            {
                interactable = true;
            }
            else
            {
                levelIcon.sprite = lockSprite;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // If the level is locked or unavailable for interaction, play decline sound and exit
            if (!interactable)
            {
                AudioManager.Instance.PlaySFX(AudioID.UI_Decline);
                return;
            }

            // If the player is already in the current selected level, play decline sound and exit
            if (sceneName == SceneManager.GetActiveScene().name)
            {
                AudioManager.Instance.PlaySFX(AudioID.UI_Decline);
                return;
            }

            // Play accept sound before starting the scene loading process
            AudioManager.Instance.PlaySFX(AudioID.UI_Accept);

            // Start the process of loading the selected level asynchronously
            StartCoroutine(LoadSceneAsync());
        }

        private IEnumerator LoadSceneAsync()
        {
            // Fade the screen out before loading the new scene
            yield return UIManager.Instance.FadeInScreen();

            // Start loading the scene asynchronously but prevent automatic activation
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.allowSceneActivation = false;

            // Wait until the scene is loaded (but not yet activated)
            while (!asyncOperation.isDone)
            {
                // Once loading reaches 90% (0.9f), consider it ready for activation
                if (asyncOperation.progress >= 0.9f)
                {
                    yield return new WaitForSeconds(1f); // Optional delay before activating the scene
                    asyncOperation.allowSceneActivation = true; // Activate the scene
                }

                yield return null; // Continue waiting in the loop
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Intentionally left blank; necessary to properly detect pointer clicks.
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Intentionally left blank; necessary to properly detect pointer clicks.
        }
    }
}
