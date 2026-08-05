using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace CryingSnow.FarmingIsland
{
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("The Start button that initiates the game.")]
        [SerializeField] private Button startButton;

        [Tooltip("The image used for screen fading effects.")]
        [SerializeField] private Image screenFader;

        [Tooltip("Audio clip to play when the title screen is displayed upon first opening the game.")]
        [SerializeField] private AudioClip titleSong;

        private void Awake()
        {
            // Hide the start button until the fade effect is complete
            startButton.gameObject.SetActive(false);

            // Ensure the screen fader is visible at the start
            screenFader.gameObject.SetActive(true);
        }

        private void Start()
        {
            // Fade out the screen and then hide the fader while showing the start button
            screenFader.DOFade(0f, 1f)
                .OnComplete(() =>
                {
                    screenFader.gameObject.SetActive(false); // Disable the screen fader
                    startButton.gameObject.SetActive(true); // Show the start button
                    InitializeStartButton(); // Set up the start button behavior
                });

            // Play the title screen background music when the game is first opened.
            AudioManager.Instance.PlayBGM(titleSong);
        }

        private void InitializeStartButton()
        {
            // Get the default scene (first level) name
            string defaultScenePath = SceneUtility.GetScenePathByBuildIndex(1);
            string defaultSceneName = System.IO.Path.GetFileNameWithoutExtension(defaultScenePath);

            // Get the name of the latest save file, if any
            string latestFileName = SaveSystem.GetLatestSaveFileName();
            string latestSceneName = string.IsNullOrEmpty(latestFileName) ? defaultSceneName : latestFileName;

            // Add a listener to start loading the scene when the button is clicked
            startButton.onClick.AddListener(() =>
            {
                StartCoroutine(LoadSceneAsync(latestSceneName)); // Start loading the scene asynchronously
                AudioManager.Instance.PlaySFX(AudioID.UI_Accept); // Play 'UI Accept' sound
                DOTween.Kill(startButton.transform); // Stop any ongoing tweening on the button (if any)
            });

            // Apply a pulsing animation to the start button for visual effect
            startButton.transform.DOScale(Vector3.one * 1.2f, 0.5f).SetLoops(-1, LoopType.Yoyo);
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            // Enable the screen fader and start fading in
            screenFader.gameObject.SetActive(true);
            yield return screenFader.DOFade(1f, 1f).WaitForCompletion();

            // Start loading the scene asynchronously but prevent automatic activation
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.allowSceneActivation = false;

            // Wait until the scene loading is complete (but not yet activated)
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
    }
}
