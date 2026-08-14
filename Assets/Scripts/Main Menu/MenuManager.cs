using UnityEngine;
using UnityEngine.UI;
using GameFlowSystem;
using GameSystem;
using PlayerSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MenuSystemV1
{
    /// <summary>
    /// Scene-local Main Menu controller
    ///
    /// IMPORTANT:
    /// MenuManager ไม่ควรเป็น DontDestroyOnLoad
    /// เพราะ Button / Background / Menu UI เป็นของ MainMenu Scene โดยตรง
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Scene")]
        [Tooltip(
            "ชื่อ Scene ที่จะโหลดตอนกด Start " +
            "(ต้องอยู่ใน Build Settings ด้วย)"
        )]
        [SerializeField] private string stageSceneName = "Stage1";

        private void OnEnable()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnDisable()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartClicked);

            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExitClicked);
        }

        public void OnStartClicked()
        {
            // Start = New Run
            // Score กลับ 0 และ PlayerStats กลับ Default
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScore();

            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.ResetRun();

            if (GameFlowManager.Instance != null)
            {
                // ใช้ GameFlowManager เป็นศูนย์กลาง transition
                // เพื่อให้ state / timeScale / fade ถูก reset ถูกต้อง
                GameFlowManager.Instance.StartOver();
                return;
            }

            // Fallback เดิม ถ้า GameFlowManager ไม่มีอยู่จริง
            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.LoadScene(stageSceneName);
            }
            else
            {
                Debug.LogError(
                    "MenuManager: ไม่พบ GameFlowManager หรือ " +
                    "LoadingScreenManager.Instance");
            }
        }

        public void OnExitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}