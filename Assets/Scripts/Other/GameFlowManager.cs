using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerSystem;

namespace GameFlowSystem
{
    /// <summary>
    /// ศูนย์กลาง Gameplay Flow: Pause, Game Lose, Scene Transition และ Final Fade
    ///
    /// Setup ที่จำเป็นใน Hierarchy (สำคัญมาก):
    /// GameFlowManager (GameObject ที่แปะสคริปต์นี้ - จะถูก DontDestroyOnLoad)
    ///  ├─ PauseCanvas      (Inactive โดย default ใน Editor ได้ - โค้ดจะคุมเปิด/ปิดเอง)
    ///  │    └─ ปุ่ม: Resume / Restart / StartOver / ToMainMenu -> เรียก method บน GameFlowManager นี้ตรงๆ
    ///  └─ GameLoseCanvas   (Inactive โดย default ใน Editor ได้)
    ///       └─ ปุ่ม: Continue / Restart / StartOver / ToMainMenu -> เรียก method บน GameFlowManager นี้ตรงๆ
    ///
    /// ทำไม Canvas ต้องเป็นลูกของ GameObject นี้เท่านั้น:
    /// DontDestroyOnLoad() ย้ายเฉพาะตัวเองกับ "ลูกในลำดับชั้นเดียวกัน" ข้าม Scene เท่านั้น
    /// ถ้า Canvas ถูกวางแยกไว้คนละที่ในแต่ละ Scene มันจะถูกทำลายทิ้งตอนเปลี่ยน Scene
    /// แล้ว Reference (pauseCanvas / gameLoseCanvas) จะกลายเป็น null ทันทีที่ข้าม Scene แรกไป
    /// -> Awake() ด้านล่างมี ValidateCanvasHierarchy() เตือนใน Console ให้ถ้าตั้งผิด
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public enum FlowState
        {
            Playing,
            Paused,
            PendingGameLose,   // ผู้เล่นตายแล้ว แต่กำลังรอ Delay (เล่น Death Animation/Effect) ก่อนขึ้นจอ Game Lose
            GameLost,
            Ending
        }

        [Header("Scene")]
        [Tooltip("Scene แรกของเกม / Stage 1")]
        [SerializeField] private string firstSceneName = "Stage1";

        [Tooltip("Main Menu Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Final Fade")]
        [Tooltip("CanvasGroup ของ Square/Panel สีดำที่ใช้ Fade จอทั้งหมด")]
        [SerializeField] private CanvasGroup finalFadeCanvasGroup;

        [Min(0f)]
        [SerializeField] private float finalFadeDuration = 2f;

        [Header("UI")]
        [Tooltip("Canvas/GameObject ของหน้า Pause - ต้องเป็นลูกของ GameObject นี้ (ดู comment ด้านบน)")]
        [SerializeField] private GameObject pauseCanvas;

        [Tooltip("Canvas/GameObject ของหน้า Game Lose - ต้องเป็นลูกของ GameObject นี้ (ดู comment ด้านบน)")]
        [SerializeField] private GameObject gameLoseCanvas;

        [Header("Pause Input")]
        [Tooltip("ปุ่มที่ใช้ Toggle Pause (กดซ้ำเพื่อ Resume) - ใช้ Input Manager เดิม")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        [Header("Game Lose Delay")]
        [Tooltip("หน่วงเวลา (วินาที) หลังผู้เล่นตายก่อนขึ้นจอ Game Lose จริง " +
                 "เพื่อให้ Death Animation/Effect เล่นจบก่อน - ระหว่างนี้เกมยังไม่ Freeze (Time.timeScale = 1)")]
        [Min(0f)]
        [SerializeField] private float gameLoseDelay = 1.5f;

        public FlowState CurrentState { get; private set; } = FlowState.Playing;
        public bool IsPaused => CurrentState == FlowState.Paused;
        public bool IsPendingGameLose => CurrentState == FlowState.PendingGameLose;
        public bool IsGameLost => CurrentState == FlowState.GameLost;
        public bool IsEnding => CurrentState == FlowState.Ending;

        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action OnGameLost;
        public event Action OnGameLoseContinued;
        public event Action OnGameEnded;

        private float finalFadeTimer;
        private float gameLoseTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ValidateCanvasHierarchy();

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);

            if (finalFadeCanvasGroup != null)
            {
                finalFadeCanvasGroup.alpha = 0f;
                finalFadeCanvasGroup.blocksRaycasts = false;
                finalFadeCanvasGroup.interactable = false;
            }
        }

        /// <summary>
        /// เตือนใน Console ถ้า pauseCanvas / gameLoseCanvas ไม่ได้เป็นลูกของ GameObject นี้
        /// เพราะจะพังตอนเปลี่ยน Scene (ดูรายละเอียดใน comment บนสุดของคลาส)
        /// </summary>
        private void ValidateCanvasHierarchy()
        {
            if (pauseCanvas != null && !pauseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError(
                    "GameFlowManager: pauseCanvas ไม่ได้เป็นลูกของ GameFlowManager GameObject " +
                    "- ย้ายเข้าไปเป็น Child ใน Hierarchy ก่อน ไม่งั้น Reference จะเป็น null " +
                    "ทันทีที่เปลี่ยน Scene (DontDestroyOnLoad ย้ายแค่ลูกในลำดับชั้นเดียวกัน)", this);
            }

            if (gameLoseCanvas != null && !gameLoseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError(
                    "GameFlowManager: gameLoseCanvas ไม่ได้เป็นลูกของ GameFlowManager GameObject " +
                    "- ย้ายเข้าไปเป็น Child ใน Hierarchy ก่อน ไม่งั้น Reference จะเป็น null " +
                    "ทันทีที่เปลี่ยน Scene (DontDestroyOnLoad ย้ายแค่ลูกในลำดับชั้นเดียวกัน)", this);
            }
        }

        private void Start()
        {
            SubscribeToPlayerStats();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            UnsubscribeFromPlayerStats();
            Instance = null;
        }

        private void SubscribeToPlayerStats()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnGameOver += HandlePlayerGameOver;
        }

        private void UnsubscribeFromPlayerStats()
        {
            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.OnGameOver -= HandlePlayerGameOver;
        }

        private void Update()
        {
            // Pause Input ใช้ได้เฉพาะตอนกำลังเล่นอยู่ หรือ Pause อยู่แล้ว (เพื่อ Resume)
            if (CurrentState == FlowState.Playing || CurrentState == FlowState.Paused)
                HandlePauseInput();

            // รอ Delay ก่อนขึ้นจอ Game Lose จริง - เกมยังไม่ Freeze ระหว่างนี้
            if (CurrentState == FlowState.PendingGameLose)
                UpdatePendingGameLose();

            // Final Fade ต้องทำงานขณะ Time.timeScale = 0
            if (CurrentState == FlowState.Ending)
                UpdateFinalFade();
        }

        // ============================================================
        // PAUSE
        // ============================================================

        private void HandlePauseInput()
        {
            if (!Input.GetKeyDown(pauseKey))
                return;

            if (CurrentState == FlowState.Playing)
                PauseGame();
            else if (CurrentState == FlowState.Paused)
                ResumeGame();
        }

        public void PauseGame()
        {
            // เงื่อนไข CurrentState != Playing กันไม่ให้ Pause ทับ GameLost/PendingGameLose/Ending
            // ทำให้ Pause กับ Game Lose ขึ้นพร้อมกันไม่ได้เด็ดขาด
            if (CurrentState != FlowState.Playing || IsLoading())
                return;

            CurrentState = FlowState.Paused;
            Time.timeScale = 0f;

            SetCanvasVisible(pauseCanvas, true);
            SetCanvasVisible(gameLoseCanvas, false);
            OnGamePaused?.Invoke();
        }

        public void ResumeGame()
        {
            if (CurrentState != FlowState.Paused)
                return;

            SetCanvasVisible(pauseCanvas, false);
            CurrentState = FlowState.Playing;
            Time.timeScale = 1f;
            OnGameResumed?.Invoke();
        }

        // ============================================================
        // GAME LOSE
        // ============================================================

        private void HandlePlayerGameOver()
        {
            BeginPendingGameLose();
        }

        /// <summary>
        /// เริ่มนับ Delay หลังผู้เล่นตาย (ไม่ Freeze เกมทันที) เพื่อให้ Death Animation/Effect เล่นจบก่อน
        /// แล้วค่อยเรียก ShowGameLose() จริงตอนหมดเวลา
        /// </summary>
        private void BeginPendingGameLose()
        {
            // กันการ Trigger ซ้ำถ้าอยู่ในสถานะที่เกี่ยวกับ Game Lose/Ending อยู่แล้ว
            if (CurrentState == FlowState.PendingGameLose ||
                CurrentState == FlowState.GameLost ||
                CurrentState == FlowState.Ending)
                return;

            CurrentState = FlowState.PendingGameLose;
            gameLoseTimer = 0f;

            // เผื่อกรณี Pause ค้างอยู่ตอนตาย (ปกติไม่เกิดเพราะ Time.timeScale = 0 ตอน Pause)
            SetCanvasVisible(pauseCanvas, false);
        }

        private void UpdatePendingGameLose()
        {
            // ใช้ deltaTime ปกติ (ไม่ใช่ unscaled) เพราะ Time.timeScale ยังเป็น 1 อยู่ระหว่างรอ Delay นี้
            gameLoseTimer += Time.deltaTime;

            if (gameLoseTimer >= gameLoseDelay)
                ShowGameLose();
        }

        /// <summary>
        /// แสดงจอ Game Lose ทันที (ข้าม Delay) - ปกติควรถูกเรียกผ่าน BeginPendingGameLose()
        /// เก็บเป็น public ไว้เผื่อกรณีอยากเรียกตรงๆ จากที่อื่น (เช่น Debug)
        /// </summary>
        public void ShowGameLose()
        {
            if (CurrentState == FlowState.Ending || CurrentState == FlowState.GameLost)
                return;

            CurrentState = FlowState.GameLost;
            Time.timeScale = 0f;

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, true);
            OnGameLost?.Invoke();
        }

        public void ContinueAfterGameLose()
        {
            if (CurrentState != FlowState.GameLost)
                return;

            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.ResetForGameLoseContinue();

            RespawnPlayer();

            SetCanvasVisible(gameLoseCanvas, false);
            CurrentState = FlowState.Playing;
            Time.timeScale = 1f;
            OnGameLoseContinued?.Invoke();
        }

        private void RespawnPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player == null)
                return;

            // PlayerControllerV1 สามารถ implement IPlayerRespawnable ได้
            // เพื่อให้ GameFlow ไม่ต้องรู้รายละเอียดภายใน Player
            if (player.TryGetComponent<IPlayerRespawnable>(out var respawnable))
            {
                respawnable.Respawn();
                return;
            }

            Debug.LogWarning(
                "GameFlowManager: Player ยังไม่ได้ implement IPlayerRespawnable " +
                "จึงยังไม่สามารถทำ Respawn logic ที่สมบูรณ์ได้");
        }

        // ============================================================
        // SCENE TRANSITIONS
        // ============================================================

        public void Restart()
        {
            if (IsLoading())
                return;

            BeginSceneTransition(SceneManager.GetActiveScene().name);
        }

        public void StartOver()
        {
            if (IsLoading())
                return;

            BeginSceneTransition(firstSceneName);
        }

        public void GoBackToMenu()
        {
            if (IsLoading())
                return;

            BeginSceneTransition(mainMenuSceneName);
        }

        /// <summary>
        /// BossV2 เรียกหลัง Boss ถูกกำจัดครบทุก Phase
        /// ถ้า Scene ปัจจุบันเป็น Scene สุดท้ายและ fadeOnFinalScene = true
        /// จะเริ่ม Final Fade; มิฉะนั้นไป Scene ถัดไป
        /// </summary>
        public void HandleBossDefeated(bool fadeOnFinalScene)
        {
            if (CurrentState == FlowState.Ending)
                return;

            if (IsFinalScene())
            {
                if (fadeOnFinalScene)
                    BeginFinalFade();

                return;
            }

            if (TryGetNextSceneName(out string nextSceneName))
                BeginSceneTransition(nextSceneName);
            else if (fadeOnFinalScene)
                BeginFinalFade();
            else
                Debug.LogWarning(
                    "GameFlowManager: ไม่พบ Scene ถัดไปใน Build Settings");
        }

        private void BeginSceneTransition(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("GameFlowManager: Scene name ว่าง");
                return;
            }

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);
            CurrentState = FlowState.Playing;

            if (LoadingScreenManager.Instance == null)
            {
                Debug.LogError(
                    "GameFlowManager: ไม่พบ LoadingScreenManager Instance " +
                    "- ต้องมี LoadingScreenManager อยู่ใน Scene แรกสุดที่เกมบูตขึ้นมา (เช่น MainMenu)");
                Time.timeScale = 1f;
                return;
            }

            LoadingScreenManager.Instance.LoadScene(sceneName);
        }

        private bool TryGetNextSceneName(out string sceneName)
        {
            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

            if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            {
                sceneName = null;
                return false;
            }

            string path = SceneUtility.GetScenePathByBuildIndex(nextIndex);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            return !string.IsNullOrWhiteSpace(sceneName);
        }

        private bool IsFinalScene()
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            return currentIndex == SceneManager.sceneCountInBuildSettings - 1;
        }

        private bool IsLoading()
        {
            return LoadingScreenManager.Instance != null &&
                   LoadingScreenManager.Instance.IsLoading;
        }

        // ============================================================
        // FINAL FADE
        // ============================================================

        private void BeginFinalFade()
        {
            CurrentState = FlowState.Ending;
            finalFadeTimer = 0f;
            Time.timeScale = 0f;

            if (finalFadeCanvasGroup == null)
            {
                FinishFinalFade();
                return;
            }

            finalFadeCanvasGroup.alpha = 0f;
            finalFadeCanvasGroup.blocksRaycasts = true;
            finalFadeCanvasGroup.interactable = true;
        }

        private void UpdateFinalFade()
        {
            if (finalFadeCanvasGroup == null)
                return;

            finalFadeTimer += Time.unscaledDeltaTime;

            float t = finalFadeDuration > 0f
                ? Mathf.Clamp01(finalFadeTimer / finalFadeDuration)
                : 1f;

            finalFadeCanvasGroup.alpha = t;

            if (t >= 1f)
                FinishFinalFade();
        }

        private void FinishFinalFade()
        {
            Time.timeScale = 0f;

            if (finalFadeCanvasGroup != null)
            {
                finalFadeCanvasGroup.alpha = 1f;
                finalFadeCanvasGroup.blocksRaycasts = true;
                finalFadeCanvasGroup.interactable = true;
            }

            OnGameEnded?.Invoke();
        }

        private static void SetCanvasVisible(GameObject canvasObject, bool visible)
        {
            if (canvasObject != null)
                canvasObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Contract สำหรับ Player ที่สามารถ Respawn ได้
    /// </summary>
    public interface IPlayerRespawnable
    {
        void Respawn();
    }
}