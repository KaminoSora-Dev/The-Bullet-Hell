using System;
using UnityEngine;
using UnityEngine.SceneManagement;

using PlayerSystem;
using AudioSystem;
using GameSystem;
using BossSystem;
using UISystem;

namespace GameFlowSystem
{
    /// <summary>
    /// ศูนย์กลาง Gameplay Flow
    /// 
    /// เป็น Persistent Manager
    /// และอยู่ข้าม Scene ด้วย DontDestroyOnLoad
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
            PendingBossTransition,
            Ending
        }

        // ============================================================
        // SCENE
        // ============================================================

        [Header("Scene Settings")]

        [Tooltip("ชื่อ Scene ด่านแรกเมื่อเริ่มเล่นใหม่ทั้งหมด (Start Over)")]
        [SerializeField]
        private string firstSceneName = "Stage01";

        [Tooltip("ชื่อ Scene ด่านถัดไปที่จะโหลดเมื่อชนะบอส")]
        [SerializeField]
        private string nextSceneName = "Stage02";

        [Tooltip("ติ๊กช่องนี้ถ้าด่านปัจจุบันเป็นด่านสุดท้ายของเกม")]
        [SerializeField]
        private bool isFinalStage = false;

        [Tooltip("ชื่อ Scene ของ Main Menu")]
        [SerializeField]
        private string mainMenuSceneName = "MainMenu";

        // ============================================================
        // BOSS FLOW
        // ============================================================

        [Header("Boss Defeat Flow")]

        [SerializeField]
        private bool transitionOnBossDefeated = true;

        [Min(0f)]
        [SerializeField]
        private float bossDefeatDelay = 1.5f;

        [SerializeField]
        private bool fadeOnFinalScene = true;

        // ============================================================
        // FINAL FADE
        // ============================================================

        [Header("Final Fade")]

        [SerializeField]
        private CanvasGroup finalFadeCanvasGroup;

        [Min(0f)]
        [SerializeField]
        private float finalFadeDuration = 2f;

        // ============================================================
        // UI
        // ============================================================

        [Header("UI")]

        [SerializeField]
        private GameObject pauseCanvas;

        [SerializeField]
        private GameObject gameLoseCanvas;

        // ============================================================
        // KEYBOARD
        // ============================================================

        [Header("Keyboard Navigation")]

        [SerializeField]
        private KeyboardMenuNavigator pauseMenuNavigator;

        [SerializeField]
        private KeyboardMenuNavigator gameLoseMenuNavigator;

        // ============================================================
        // PAUSE
        // ============================================================

        [Header("Pause Input")]

        [SerializeField]
        private KeyCode pauseKey = KeyCode.Escape;

        // ============================================================
        // GAME LOSE
        // ============================================================

        [Header("Game Lose Delay")]

        [Min(0f)]
        [SerializeField]
        private float gameLoseDelay = 1.5f;

        // ============================================================
        // STATE
        // ============================================================

        public FlowState CurrentState { get; private set; } = FlowState.Playing;

        public bool IsPaused => CurrentState == FlowState.Paused;
        public bool IsPendingGameLose => CurrentState == FlowState.PendingGameLose;
        public bool IsGameLost => CurrentState == FlowState.GameLost;
        public bool IsPendingBossTransition => CurrentState == FlowState.PendingBossTransition;
        public bool IsEnding => CurrentState == FlowState.Ending;

        public bool IsPlayerActionLocked =>
            CurrentState == FlowState.PendingBossTransition ||
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
        // PUBLIC SETUP API
        // ============================================================

        /// <summary>
        /// ใช้สำหรับเปลี่ยนชื่อด่านถัดไปผ่านโค้ดในระหว่างเล่น
        /// </summary>
        public void SetNextSceneName(string sceneName, bool isFinal = false)
        {
            nextSceneName = sceneName;
            isFinalStage = isFinal;
        }

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
            ValidateKeyboardNavigators();

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);

            ResetFinalFadeVisual();

            finalFadeFinished = false;
            runFinishRecorded = false;

            Time.timeScale = 1f;
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
                PlayerStatsManager.Instance.OnGameOver += HandlePlayerGameOver;
            }
        }

        private void UnsubscribeFromPlayerStats()
        {
            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.OnGameOver -= HandlePlayerGameOver;
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
            {
                PauseGame();
            }
            else if (CurrentState == FlowState.Paused)
            {
                ResumeGame();
            }
        }

        public void PauseGame()
        {
            if (CurrentState != FlowState.Playing || IsLoading())
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
            {
                return;
            }

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
            gameLoseTimer += Time.unscaledDeltaTime;

            if (gameLoseTimer >= gameLoseDelay)
            {
                ShowGameLose();
            }
        }

        public void ShowGameLose()
        {
            if (CurrentState == FlowState.Ending || CurrentState == FlowState.GameLost)
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
            {
                return;
            }

            ScoreManager.Instance?.SavePersistentHiScoreIfHigher();
            ScoreManager.Instance?.ResetScore();
            PlayerStatsManager.Instance?.ResetForGameLoseContinue();

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
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player == null)
                return;

            if (player.TryGetComponent<IPlayerRespawnable>(out var respawnable))
            {
                respawnable.Respawn();
                return;
            }

            Debug.LogWarning("GameFlowManager: Player ยังไม่ได้ implement IPlayerRespawnable");
        }

        // ============================================================
        // BOSS
        // ============================================================

        private void HandleAnyBossDefeated()
        {
            if (!transitionOnBossDefeated)
                return;

            if (CurrentState == FlowState.PendingBossTransition || CurrentState == FlowState.Ending)
            {
                return;
            }

            if (CurrentState == FlowState.GameLost || CurrentState == FlowState.PendingGameLose)
            {
                return;
            }

            PlayerStatsManager.Instance?.SaveStageCheckpoint();
            ScoreManager.Instance?.SavePersistentHiScoreIfHigher();

            BeginPendingBossTransition();
        }

        private void BeginPendingBossTransition()
        {
            CurrentState = FlowState.PendingBossTransition;
            bossDefeatTimer = 0f;
            Time.timeScale = 1f;

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);
        }

        private void UpdatePendingBossTransition()
        {
            bossDefeatTimer += Time.unscaledDeltaTime;

            if (bossDefeatTimer < bossDefeatDelay)
            {
                return;
            }

            ExecuteBossDefeatTransition();
        }

        private void ExecuteBossDefeatTransition()
        {
            if (CurrentState != FlowState.PendingBossTransition)
            {
                return;
            }

            // ถ้าเป็นด่านสุดท้าย หรือไม่ได้ตั้งชื่อด่านถัดไป จะเข้าสู่ฉากจบ (Ending)
            if (isFinalStage || string.IsNullOrWhiteSpace(nextSceneName))
            {
                if (fadeOnFinalScene)
                    BeginFinalFade();
                else
                    FinishGameWithoutFade();

                return;
            }

            // เปลี่ยนไปยังด่านตามที่กำหนดใน nextSceneName
            BeginSceneTransition(nextSceneName);
        }

        // ============================================================
        // SCENE TRANSITIONS
        // ============================================================

        public void Restart()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
            {
                RecordRunAsFinished(false);
            }

            if (PlayerStatsManager.Instance != null)
            {
                if (!PlayerStatsManager.Instance.RestoreLastStageCheckpoint())
                {
                    PlayerStatsManager.Instance.ResetRun();
                    ScoreManager.Instance?.ResetScore();
                }
            }
            else
            {
                ScoreManager.Instance?.ResetScore();
            }

            ResetEndingRuntimeState();

            BeginSceneTransition(SceneManager.GetActiveScene().name);
        }

        public void StartOver()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
            {
                RecordRunAsFinished(false);
            }

            PlayerStatsManager.Instance?.ResetRun();
            ScoreManager.Instance?.ResetScore();

            ResetEndingRuntimeState();

            BeginSceneTransition(firstSceneName);
        }

        public void GoBackToMenu()
        {
            if (IsLoading())
                return;

            if (CurrentState == FlowState.GameLost)
            {
                RecordRunAsFinished(false);
            }

            PlayerStatsManager.Instance?.ResetRun();
            ScoreManager.Instance?.ResetScore();

            PrepareForMainMenu();

            BeginSceneTransition(mainMenuSceneName);
        }

        private void PrepareForMainMenu()
        {
            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);

            finalFadeFinished = false;
            runFinishRecorded = false;

            finalFadeTimer = 0f;
            bossDefeatTimer = 0f;
            gameLoseTimer = 0f;

            ResetFinalFadeVisual();

            CurrentState = FlowState.Playing;

            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.ResetForMainMenu();
            }

            Time.timeScale = 1f;
        }

        public void HandleBossDefeated(string ignoredNextSceneName, bool ignoredFadeOnFinalScene) => HandleBossDefeated();
        public void HandleBossDefeated(bool ignoredFadeOnFinalScene) => HandleBossDefeated();
        public void HandleBossDefeated() => HandleAnyBossDefeated();

        private void BeginSceneTransition(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("GameFlowManager: Scene name ว่างเปล่า");
                return;
            }

            if (!IsSceneInBuildSettings(targetSceneName))
            {
                Debug.LogError($"GameFlowManager: ไม่พบ Scene '{targetSceneName}' ใน Build Settings");
                return;
            }

            SetCanvasVisible(pauseCanvas, false);
            SetCanvasVisible(gameLoseCanvas, false);

            ResetFinalFadeVisual();

            AudioManager.Instance?.StopAllSFX();
            MusicManager.Instance?.StopMusic();

            CurrentState = FlowState.Playing;
            Time.timeScale = 1f;

            // Fallback: ถ้าไม่มี LoadingScreenManager ให้ทำการโหลด Scene โดยตรงทันที
            if (LoadingScreenManager.Instance == null)
            {
                Debug.LogWarning("GameFlowManager: ไม่พบ LoadingScreenManager - กำลังโหลด Scene โดยตรง...");
                SceneManager.LoadScene(targetSceneName);
                return;
            }

            LoadingScreenManager.Instance.LoadScene(targetSceneName);
        }

        // ============================================================
        // SCENE HELPERS
        // ============================================================

        private bool IsSceneInBuildSettings(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
                return false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (string.Equals(buildSceneName, targetSceneName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLoading()
        {
            return LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading;
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
            finalFadeFinished = false;
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

            finalFadeTimer += Time.unscaledDeltaTime;

            float t = finalFadeDuration > 0f
                ? Mathf.Clamp01(finalFadeTimer / finalFadeDuration)
                : 1f;

            if (finalFadeCanvasGroup != null)
            {
                finalFadeCanvasGroup.alpha = t;
            }

            if (t >= 1f)
                FinishFinalFade();
        }

        private void FinishFinalFade()
        {
            if (finalFadeFinished)
                return;

            finalFadeFinished = true;
            RecordRunAsFinished(true);
            OnGameEnded?.Invoke();
            GoBackToMenu();
        }

        private void FinishGameWithoutFade()
        {
            if (finalFadeFinished)
                return;

            finalFadeFinished = true;
            RecordRunAsFinished(true);
            OnGameEnded?.Invoke();
            GoBackToMenu();
        }

        // ============================================================
        // PLAYER STATS & RESET
        // ============================================================

        private void RecordRunAsFinished(bool completed)
        {
            if (runFinishRecorded)
                return;

            runFinishRecorded = true;
            PlayerStatsManager.Instance?.RecordRunFinished(completed);
        }

        private void ResetEndingRuntimeState()
        {
            finalFadeFinished = false;
            runFinishRecorded = false;

            finalFadeTimer = 0f;
            bossDefeatTimer = 0f;
            gameLoseTimer = 0f;

            ResetFinalFadeVisual();

            Time.timeScale = 1f;
            CurrentState = FlowState.Playing;
        }

        private void ResetFinalFadeVisual()
        {
            if (finalFadeCanvasGroup == null)
                return;

            finalFadeCanvasGroup.alpha = 0f;
            finalFadeCanvasGroup.blocksRaycasts = false;
            finalFadeCanvasGroup.interactable = false;
        }

        // ============================================================
        // CANVAS & NAVIGATOR VALIDATION
        // ============================================================

        private void ValidateCanvasHierarchy()
        {
            if (pauseCanvas != null && !pauseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError("GameFlowManager: pauseCanvas ไม่ได้เป็นลูกของ GameFlowManager", this);
            }

            if (gameLoseCanvas != null && !gameLoseCanvas.transform.IsChildOf(transform))
            {
                Debug.LogError("GameFlowManager: gameLoseCanvas ไม่ได้เป็นลูกของ GameFlowManager", this);
            }
        }

        private void ValidateKeyboardNavigators()
        {
            if (pauseMenuNavigator != null && pauseCanvas != null &&
                !pauseMenuNavigator.transform.IsChildOf(pauseCanvas.transform))
            {
                Debug.LogError("GameFlowManager: pauseMenuNavigator ไม่ได้เป็นลูกของ pauseCanvas", this);
            }

            if (gameLoseMenuNavigator != null && gameLoseCanvas != null &&
                !gameLoseMenuNavigator.transform.IsChildOf(gameLoseCanvas.transform))
            {
                Debug.LogError("GameFlowManager: gameLoseMenuNavigator ไม่ได้เป็นลูกของ gameLoseCanvas", this);
            }
        }

        private static void SetCanvasVisible(GameObject canvasObject, bool visible)
        {
            if (canvasObject != null)
                canvasObject.SetActive(visible);
        }
    }

    public interface IPlayerRespawnable
    {
        void Respawn();
    }
}