using System;
using UnityEngine;
using UnityEngine.SceneManagement;

using PlayerSystem;
using AudioSystem;
using GameSystem;
using BossSystem;

namespace GameFlowSystem
{
    /// <summary>
    /// ศูนย์กลาง Gameplay Flow ของเกม
    ///
    /// รับผิดชอบ:
    /// - Pause / Resume
    /// - Game Lose
    /// - Boss Defeat -> Scene Transition
    /// - Final Fade
    ///
    /// สำคัญ:
    /// BossV2 จะไม่เป็นคนกำหนด Scene อีกต่อไป
    /// Boss เพียงประกาศว่า "Boss ถูกกำจัดแล้ว"
    /// แล้ว GameFlowManager เป็นผู้ตัดสินใจทั้งหมด
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public enum FlowState
        {
            Playing,
            Paused,
            PendingGameLose,
            GameLost,

            // รอช่วงพักหลัง Boss ตายก่อนเปลี่ยน Scene
            PendingBossTransition,

            // กำลัง Fade เพื่อจบเกม
            Ending
        }

        // ============================================================
        // SCENE
        // ============================================================

        [Header("Scene")]
        [Tooltip("Scene แรกของเกม / Stage 1")]
        [SerializeField] private string firstSceneName = "Stage1";

        [Tooltip("Main Menu Scene")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        // ============================================================
        // BOSS FLOW
        // ============================================================

        [Header("Boss Defeat Flow")]
        [Tooltip(
            "ถ้าเปิดไว้ เมื่อ Boss ถูกกำจัดครบทุก Phase " +
            "GameFlowManager จะเริ่มระบบเปลี่ยน Scene อัตโนมัติ"
        )]
        [SerializeField] private bool transitionOnBossDefeated = true;

        [Min(0f)]
        [Tooltip(
            "เวลาพักหลัง Boss ตายก่อนเริ่มเปลี่ยน Scene\n" +
            "ใช้ Unscaled Time เพื่อให้ทำงานได้แม้ Time.timeScale ถูก Freeze"
        )]
        [SerializeField] private float bossDefeatDelay = 1.5f;

        [Tooltip(
            "ถ้า Boss อยู่ Scene สุดท้ายและเปิดไว้ " +
            "จะ Fade จอดำเพื่อจบเกมแทนการโหลด Scene ถัดไป"
        )]
        [SerializeField] private bool fadeOnFinalScene = true;

        // ============================================================
        // FINAL FADE
        // ============================================================

        [Header("Final Fade")]
        [Tooltip(
            "CanvasGroup ของ Square/Panel สีดำที่ใช้ Fade จอทั้งหมด"
        )]
        [SerializeField] private CanvasGroup finalFadeCanvasGroup;

        [Min(0f)]
        [SerializeField] private float finalFadeDuration = 2f;

        // ============================================================
        // UI
        // ============================================================

        [Header("UI")]
        [Tooltip(
            "Canvas/GameObject ของหน้า Pause - ต้องเป็นลูกของ GameFlowManager"
        )]
        [SerializeField] private GameObject pauseCanvas;

        [Tooltip(
            "Canvas/GameObject ของหน้า Game Lose - ต้องเป็นลูกของ GameFlowManager"
        )]
        [SerializeField] private GameObject gameLoseCanvas;

        // ============================================================
        // PAUSE
        // ============================================================

        [Header("Pause Input")]
        [Tooltip(
            "ปุ่มที่ใช้ Toggle Pause (กดซ้ำเพื่อ Resume) - ใช้ Input Manager เดิม"
        )]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        // ============================================================
        // GAME LOSE
        // ============================================================

        [Header("Game Lose Delay")]
        [Tooltip(
            "หน่วงเวลา (วินาที) หลังผู้เล่นตายก่อนขึ้นจอ Game Lose จริง " +
            "เพื่อให้ Death Animation/Effect เล่นจบก่อน"
        )]
        [Min(0f)]
        [SerializeField] private float gameLoseDelay = 1.5f;

        // ============================================================
        // STATE
        // ============================================================

        public FlowState CurrentState { get; private set; } =
            FlowState.Playing;

        public bool IsPaused =>
            CurrentState == FlowState.Paused;

        public bool IsPendingGameLose =>
            CurrentState == FlowState.PendingGameLose;

        public bool IsGameLost =>
            CurrentState == FlowState.GameLost;

        public bool IsPendingBossTransition =>
            CurrentState == FlowState.PendingBossTransition;

        public bool IsEnding =>
            CurrentState == FlowState.Ending;

        // ============================================================
        // EVENTS
        // ============================================================

        public event Action OnGamePaused;
        public event Action OnGameResumed;
        public event Action OnGameLost;
        public event Action OnGameLoseContinued;
        public event Action OnGameEnded;

        // ============================================================
        // RUNTIME
        // ============================================================

        private float finalFadeTimer;
        private float gameLoseTimer;
        private float bossDefeatTimer;

        private bool finalFadeFinished;
        private bool runFinishRecorded;

        // ============================================================
        // UNITY
        // ============================================================

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

            finalFadeFinished = false;
            runFinishRecorded = false;
        }

        private void OnEnable()
        {
            BossV2.OnAnyBossDefeatedAny += HandleAnyBossDefeated;
        }

        private void Start()
        {
            SubscribeToPlayerStats();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            BossV2.OnAnyBossDefeatedAny -= HandleAnyBossDefeated;

            UnsubscribeFromPlayerStats();

            Instance = null;
        }

        private void Update()
        {
            switch (CurrentState)
            {
                case FlowState.Playing:
                case FlowState.Paused:
                    HandlePauseInput();
                    break;

                case FlowState.PendingGameLose:
                    UpdatePendingGameLose();
                    break;

                case FlowState.PendingBossTransition:
                    UpdatePendingBossTransition();
                    break;

                case FlowState.Ending:
                    UpdateFinalFade();
                    break;
            }
        }

        // ============================================================
        // PLAYER STATS
        // ============================================================

        private void SubscribeToPlayerStats()
        {
            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.OnGameOver +=
                    HandlePlayerGameOver;
            }
        }

        private void UnsubscribeFromPlayerStats()
        {
            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.OnGameOver -=
                    HandlePlayerGameOver;
            }
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
            if (CurrentState != FlowState.Playing ||
                IsLoading())
            {
                return;
            }

            CurrentState = FlowState.Paused;
            Time.timeScale = 0f;

            AudioManager.Instance?.PauseSFX();
            MusicManager.Instance?.PauseMusic();

            SetCanvasVisible(pauseCanvas, true);
            SetCanvasVisible(gameLoseCanvas, false);

            OnGamePaused?.Invoke();
        }

        public void ResumeGame()
        {
            if (CurrentState != FlowState.Paused)
                return;

            AudioManager.Instance?.ResumeSFX();
            MusicManager.Instance?.ResumeMusic();

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

        private void BeginPendingGameLose()
        {
            if (CurrentState == FlowState.PendingGameLose ||
                CurrentState == FlowState.GameLost ||
                CurrentState == FlowState.PendingBossTransition ||
                CurrentState == FlowState.Ending)
            {
                return;
            }

            CurrentState = FlowState.PendingGameLose;
            gameLoseTimer = 0f;

            SetCanvasVisible(pauseCanvas, false);
        }

        private void UpdatePendingGameLose()
        {
            // ใช้ Unscaled Time เพื่อให้ delay นี้ไม่ขึ้นกับ Time.timeScale
            gameLoseTimer += Time.unscaledDeltaTime;

            if (gameLoseTimer >= gameLoseDelay)
                ShowGameLose();
        }

        public void ShowGameLose()
        {
            if (CurrentState == FlowState.Ending ||
                CurrentState == FlowState.GameLost)
            {
                return;
            }

            CurrentState = FlowState.GameLost;

            Time.timeScale = 0f;

            AudioManager.Instance?.PauseSFX();
            MusicManager.Instance?.PauseMusic();

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, true);

            OnGameLost?.Invoke();
        }

        public void ContinueAfterGameLose()
        {
            if (CurrentState != FlowState.GameLost)
                return;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.SavePersistentHiScoreIfHigher();
                ScoreManager.Instance.ResetScore();
            }

            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.ResetForGameLoseContinue();

            RespawnPlayer();

            SetCanvasVisible(gameLoseCanvas, false);

            AudioManager.Instance?.ResumeSFX();
            MusicManager.Instance?.ResumeMusic();

            CurrentState = FlowState.Playing;
            Time.timeScale = 1f;

            OnGameLoseContinued?.Invoke();
        }

        private void RespawnPlayer()
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player == null)
                return;

            if (player.TryGetComponent<IPlayerRespawnable>(
                out var respawnable))
            {
                respawnable.Respawn();
                return;
            }

            Debug.LogWarning(
                "GameFlowManager: Player ยังไม่ได้ implement " +
                "IPlayerRespawnable จึงยังไม่สามารถทำ Respawn logic " +
                "ที่สมบูรณ์ได้");
        }

        // ============================================================
        // BOSS DEFEAT
        // ============================================================

        /// <summary>
        /// รับ event จาก BossV2 เมื่อ Boss ถูกกำจัดครบทุก Phase
        ///
        /// Boss ไม่ได้บอกว่าจะไป Scene ไหน
        /// GameFlowManager เป็นคนตัดสินใจเองทั้งหมด
        /// </summary>
        private void HandleAnyBossDefeated()
        {
            if (!transitionOnBossDefeated)
                return;

            if (CurrentState == FlowState.PendingBossTransition ||
                CurrentState == FlowState.Ending)
            {
                return;
            }

            // ถ้ากำลัง Game Lose อยู่ ไม่ควรให้ Boss flow มาแทรก
            if (CurrentState == FlowState.GameLost ||
                CurrentState == FlowState.PendingGameLose)
            {
                return;
            }

            BeginPendingBossTransition();
        }

        private void BeginPendingBossTransition()
        {
            CurrentState = FlowState.PendingBossTransition;
            bossDefeatTimer = 0f;

            // สำคัญ:
            // ยังไม่ Freeze เกมตรงนี้
            //
            // เพื่อให้ Death Visual ของ Boss,
            // Item Drop และ Effect ต่างๆ มีเวลาเล่น
            // ก่อน LoadingScreenManager จะ Freeze เกม
            Time.timeScale = 1f;

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);
        }

        private void UpdatePendingBossTransition()
        {
            bossDefeatTimer += Time.unscaledDeltaTime;

            if (bossDefeatTimer < bossDefeatDelay)
                return;

            ExecuteBossDefeatTransition();
        }

        private void ExecuteBossDefeatTransition()
        {
            if (CurrentState != FlowState.PendingBossTransition)
                return;

            // --------------------------------------------------------
            // Scene สุดท้าย
            // --------------------------------------------------------

            if (IsFinalScene())
            {
                if (fadeOnFinalScene)
                {
                    BeginFinalFade();
                }
                else
                {
                    // ถ้าไม่ใช้ Fade ก็ถือว่าจบ Run ทันที
                    FinishGameWithoutFade();
                }

                return;
            }

            // --------------------------------------------------------
            // Scene ปกติ
            // GameFlowManager เป็นคนหา Scene ถัดไปเอง
            // --------------------------------------------------------

            if (TryGetNextSceneName(out string nextSceneName))
            {
                BeginSceneTransition(nextSceneName);
            }
            else
            {
                Debug.LogWarning(
                    "GameFlowManager: Boss ถูกกำจัดแล้ว แต่ไม่พบ Scene ถัดไป " +
                    "ใน Build Settings");

                FinishGameWithoutFade();
            }
        }

        // ============================================================
        // SCENE TRANSITIONS
        // ============================================================

        public void Restart()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
                RecordRunAsFinished(false);

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.SavePersistentHiScoreIfHigher();
                ScoreManager.Instance.ResetScore();
            }

            ResetEndingRuntimeState();

            BeginSceneTransition(
                SceneManager.GetActiveScene().name
            );
        }

        public void StartOver()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
                RecordRunAsFinished(false);

            ResetEndingRuntimeState();

            BeginSceneTransition(firstSceneName);
        }

        public void GoBackToMenu()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
                RecordRunAsFinished(false);

            ResetEndingRuntimeState();

            BeginSceneTransition(mainMenuSceneName);
        }

        /// <summary>
        /// Compatibility API สำหรับ Script เก่าที่อาจยังเรียก method นี้
        ///
        /// Parameter เดิมจะถูก Ignore
        /// เพราะตั้งแต่เวอร์ชันนี้ GameFlowManager เป็นผู้ควบคุม Scene เอง
        /// </summary>
        public void HandleBossDefeated(
            string ignoredNextSceneName,
            bool ignoredFadeOnFinalScene)
        {
            HandleBossDefeated();
        }

        /// <summary>
        /// Compatibility overload เดิม
        /// </summary>
        public void HandleBossDefeated(
            bool ignoredFadeOnFinalScene)
        {
            HandleBossDefeated();
        }

        /// <summary>
        /// API ใหม่:
        /// แจ้ง GameFlowManager ว่า Boss จบแล้ว
        ///
        /// โดยปกติไม่จำเป็นต้องเรียกเอง เพราะ BossV2
        /// ยิง OnAnyBossDefeatedAny อยู่แล้ว
        /// </summary>
        public void HandleBossDefeated()
        {
            HandleAnyBossDefeated();
        }

        private void BeginSceneTransition(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError(
                    "GameFlowManager: Scene name ว่าง");
                return;
            }

            if (!IsSceneInBuildSettings(sceneName))
            {
                Debug.LogError(
                    "GameFlowManager: ไม่พบ Scene '" +
                    sceneName +
                    "' ใน Build Settings\n" +
                    "ตรวจสอบชื่อ Scene และตรวจสอบว่า Scene " +
                    "ถูกเพิ่มเข้า Build Settings แล้ว",
                    this);

                return;
            }

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);

            AudioManager.Instance?.StopAllSFX();
            MusicManager.Instance?.StopMusic();

            CurrentState = FlowState.Playing;

            if (LoadingScreenManager.Instance == null)
            {
                Debug.LogError(
                    "GameFlowManager: ไม่พบ LoadingScreenManager Instance " +
                    "- ต้องมี LoadingScreenManager อยู่ใน Scene แรกสุด " +
                    "ที่เกมบูตขึ้นมา (เช่น MainMenu)");

                Time.timeScale = 1f;
                return;
            }

            // LoadingScreenManager จะ Freeze เกมและจัดการ Async Load
            LoadingScreenManager.Instance.LoadScene(sceneName);
        }

        private bool TryGetNextSceneName(
            out string sceneName)
        {
            int currentIndex =
                SceneManager.GetActiveScene().buildIndex;

            int nextIndex = currentIndex + 1;

            if (nextIndex >=
                SceneManager.sceneCountInBuildSettings)
            {
                sceneName = null;
                return false;
            }

            string path =
                SceneUtility.GetScenePathByBuildIndex(
                    nextIndex
                );

            sceneName =
                System.IO.Path.GetFileNameWithoutExtension(
                    path
                );

            return !string.IsNullOrWhiteSpace(sceneName);
        }

        private bool IsSceneInBuildSettings(
            string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            for (int i = 0;
                 i < SceneManager.sceneCountInBuildSettings;
                 i++)
            {
                string path =
                    SceneUtility.GetScenePathByBuildIndex(i);

                string buildSceneName =
                    System.IO.Path.GetFileNameWithoutExtension(
                        path
                    );

                if (string.Equals(
                    buildSceneName,
                    sceneName,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFinalScene()
        {
            int currentIndex =
                SceneManager.GetActiveScene().buildIndex;

            return currentIndex >= 0 &&
                   currentIndex ==
                   SceneManager.sceneCountInBuildSettings - 1;
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
            if (CurrentState == FlowState.Ending)
                return;

            CurrentState = FlowState.Ending;

            finalFadeTimer = 0f;

            // Reset เพื่อป้องกัน state จาก Run ก่อนหน้า
            finalFadeFinished = false;

            // ตอน Fade จบแล้วเกมจะถูก Freeze อย่างถาวร
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
            if (finalFadeFinished)
                return;

            // ห้ามใช้ Time.deltaTime เพราะ Time.timeScale = 0
            finalFadeTimer += Time.unscaledDeltaTime;

            float t =
                finalFadeDuration > 0f
                    ? Mathf.Clamp01(
                        finalFadeTimer /
                        finalFadeDuration
                    )
                    : 1f;

            if (finalFadeCanvasGroup != null)
                finalFadeCanvasGroup.alpha = t;

            if (t >= 1f)
                FinishFinalFade();
        }

        private void FinishFinalFade()
        {
            if (finalFadeFinished)
                return;

            finalFadeFinished = true;

            Time.timeScale = 0f;

            if (finalFadeCanvasGroup != null)
            {
                finalFadeCanvasGroup.alpha = 1f;
                finalFadeCanvasGroup.blocksRaycasts = true;
                finalFadeCanvasGroup.interactable = true;
            }

            RecordRunAsFinished(true);

            OnGameEnded?.Invoke();
        }

        private void FinishGameWithoutFade()
        {
            if (finalFadeFinished)
                return;

            finalFadeFinished = true;

            Time.timeScale = 0f;

            RecordRunAsFinished(true);

            OnGameEnded?.Invoke();

            CurrentState = FlowState.Ending;
        }

        // ============================================================
        // PLAYER STATS
        // ============================================================

        private void RecordRunAsFinished(bool completed)
        {
            if (runFinishRecorded)
                return;

            runFinishRecorded = true;

            PlayerStatsManager.Instance?.RecordRunFinished(
                completed
            );
        }

        private void ResetEndingRuntimeState()
        {
            finalFadeFinished = false;
            runFinishRecorded = false;
            finalFadeTimer = 0f;
            bossDefeatTimer = 0f;

            if (finalFadeCanvasGroup != null)
            {
                finalFadeCanvasGroup.alpha = 0f;
                finalFadeCanvasGroup.blocksRaycasts = false;
                finalFadeCanvasGroup.interactable = false;
            }

            Time.timeScale = 1f;
            CurrentState = FlowState.Playing;
        }

        // ============================================================
        // CANVAS
        // ============================================================

        private void ValidateCanvasHierarchy()
        {
            if (pauseCanvas != null &&
                !pauseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError(
                    "GameFlowManager: pauseCanvas ไม่ได้เป็นลูกของ " +
                    "GameFlowManager GameObject - " +
                    "ย้ายเข้าไปเป็น Child ใน Hierarchy ก่อน",
                    this);
            }

            if (gameLoseCanvas != null &&
                !gameLoseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError(
                    "GameFlowManager: gameLoseCanvas ไม่ได้เป็นลูกของ " +
                    "GameFlowManager GameObject - " +
                    "ย้ายเข้าไปเป็น Child ใน Hierarchy ก่อน",
                    this);
            }
        }

        private static void SetCanvasVisible(
            GameObject canvasObject,
            bool visible)
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