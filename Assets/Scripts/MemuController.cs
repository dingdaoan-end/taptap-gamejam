using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public sealed class MemuController : MonoBehaviour
    {
        [SerializeField] string gameplayScene = "Map";
        [SerializeField] Button startButton;
        [SerializeField] Button quitButton;
        [SerializeField] AudioSource clickSound;
        bool loading;

        void Awake()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void StartGame()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(gameplayScene))
            {
                Debug.LogError("Add the gameplay scene to Build Settings: " + gameplayScene, this);
                return;
            }
            StartCoroutine(Confirm(true));
        }

        public void QuitGame()
        {
            if (loading) return;
            StartCoroutine(Confirm(false));
        }

        IEnumerator Confirm(bool startGame)
        {
            loading = true;
            if (startButton != null) startButton.interactable = false;
            if (quitButton != null) quitButton.interactable = false;
            if (clickSound != null && clickSound.clip != null)
            {
                clickSound.Play();
                // Keep the scene alive through the short audio tail, even if time is paused.
                yield return new WaitForSecondsRealtime(clickSound.clip.length / Mathf.Max(.01f, Mathf.Abs(clickSound.pitch)) + .03f);
            }
            if (startGame)
            {
                Time.timeScale = 1;
                SceneManager.LoadSceneAsync(gameplayScene, LoadSceneMode.Single);
                yield break;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
