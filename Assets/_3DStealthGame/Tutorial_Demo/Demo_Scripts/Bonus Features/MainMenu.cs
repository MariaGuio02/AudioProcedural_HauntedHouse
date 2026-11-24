using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace StealthGame
{
    public class MainMenu : MonoBehaviour
    {
        private UIDocument m_UIDocument;

        private Button m_StartButton;
        private Button m_ExitButton;

        private void Awake()
        {
            m_UIDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            m_StartButton = m_UIDocument.rootVisualElement.Q<Button>("StartButton");
            m_ExitButton = m_UIDocument.rootVisualElement.Q<Button>("ExitButton");

            m_StartButton.clicked += () =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayStart();
                //The game scene needs to be set in the Build Profile Scene List
                SceneManager.LoadScene(1);
            };

            m_ExitButton.clicked += () =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayExit();
                StartCoroutine(QuitAfterDelay(0.6f));   // deja sonar el efecto antes de cerrar
            };
        }

        private IEnumerator QuitAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Application.Quit();
        }
    }
}
