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
    public class MenuManager : MonoBehaviour
    {
        [Header("Buttons")]
        public Button startButton;
        public Button exitButton;

        [Header("Scene")]
        [Tooltip("ชื่อ Scene ที่จะโหลดตอนกด Start (ต้องอยู่ใน Build Settings ด้วย)")]
        [SerializeField] private string stageSceneName = "Stage1";

        private void Start()
        {
            startButton.onClick.AddListener(OnStartClicked);
            exitButton.onClick.AddListener(OnExitClicked);
        }

        public void OnStartClicked()
        {

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScore();

            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.ResetRun();

            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.LoadScene(stageSceneName);
            }
            else
            {
                Debug.LogError("MenuManager: ไม่พบ LoadingScreenManager.Instance ใน Scene - ตรวจว่าวาง LoadingScreenManager ไว้แล้วหรือยัง");
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
