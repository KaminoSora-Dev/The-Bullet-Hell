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
    /// หน้าเมนูหลัก - เหลือแค่ Start กับ Exit ตามที่ต้องการ
    /// Start จะรีเซ็ตคะแนน/สถิติ/ทรัพยากรผู้เล่นกลับเป็นค่าเริ่มต้น (เริ่มเกมใหม่)
    /// แล้วเรียก LoadingScreenManager.Instance.LoadScene() แทนการ SceneManager.LoadScene ตรงๆ
    /// เพื่อให้ผ่านหน้าโหลด + Fade ตามระบบที่ตั้งไว้
    /// </summary>
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
            // เพิ่มใหม่: รีเซ็ตคะแนน/ชีวิต/บอมบ์/พาวเวอร์/สถิติ กลับเป็นค่าเริ่มต้นทุกครั้งที่เริ่มเกมใหม่จากเมนู
            // (ถ้าไม่รีเซ็ต ค่าจากรอบเล่นก่อนหน้าจะค้างอยู่ เพราะ ScoreManager/PlayerStatsManager เป็น DontDestroyOnLoad)
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
