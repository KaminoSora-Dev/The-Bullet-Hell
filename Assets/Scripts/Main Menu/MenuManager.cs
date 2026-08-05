using System.Collections;
using System.Collections.Generic;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MenuSystemV1
{
    public class MenuManager : MonoBehaviour
    {
        [Header("Drag specific Button to here.")]
        public Button startButton;
        public Button stage1Button;
        public Button goBackButton;

        [Header("Input Panel Object")]
        public GameObject MainMenuPanel;
        public GameObject GoBackPanel;

        private void Start()
        {
            startButton.onClick.AddListener(onStartClicked);
            stage1Button.onClick.AddListener(onStage1Clicked);
            goBackButton.onClick.AddListener(onGoBackClicked);
        }

        private void Update()
        {
            
        }

        public void onStartClicked()
        {
            MainMenuPanel.SetActive(false);
            GoBackPanel.SetActive(true);
        }

        public void onStage1Clicked()
        {
            SceneManager.LoadScene("Stage1");
        }

        public void onGoBackClicked()
        {
            MainMenuPanel.SetActive(true);
            GoBackPanel.SetActive(false);
        }
    }
}

