using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace GameFlowSystem
{
    /// <summary>
    /// รูปแบบตัวบอกสถานะโหลด
    /// </summary>
    public enum LoadingIndicatorMode
    {
        PlainText,
        AnimatedSprite,
        StaticSprite
    }

    /// <summary>
    /// Singleton ถาวร (DontDestroyOnLoad)
    ///
    /// รับผิดชอบ Loading Screen ระหว่าง Scene Transition
    ///
    /// สำคัญ:
    /// - LoadingScreenManager เป็น Persistent Manager
    /// - UI ของ Loading Screen จึงอยู่ข้าม Scene ด้วย
    /// - ต้องมี ResetForMainMenu() เพื่อคืน UI/state
    ///   หลังจบ Run และก่อน Main Menu ถูกใช้งานอีกครั้ง
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        public static LoadingScreenManager Instance { get; private set; }

        [Header("Panel")]
        [Tooltip(
            "Panel หลักที่มีพื้นดำ + Loading Text/Sprite อยู่ข้างใน"
        )]
        [SerializeField] private GameObject loadingPanel;

        [Tooltip(
            "CanvasGroup บน loadingPanel - ใช้คุม alpha ตอน Fade"
        )]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Indicator Mode")]
        [SerializeField]
        private LoadingIndicatorMode indicatorMode =
            LoadingIndicatorMode.PlainText;

        [Header("Plain Text")]
        [SerializeField] private TMP_Text loadingText;

        [SerializeField]
        private string loadingMessage = "Loading...";

        [Header("Static Sprite")]
        [SerializeField] private Image staticImage;
        [SerializeField] private Sprite staticSprite;

        [Header("Animated Sprite")]
        [SerializeField] private Image animatedImage;

        [SerializeField]
        private List<Sprite> animationFrames =
            new List<Sprite>();

        [SerializeField]
        private float frameRate = 8f;

        [Header("Fade")]
        [SerializeField] private bool useFade = true;

        [Min(0f)]
        [SerializeField] private float fadeDuration = 0.3f;

        // ============================================================
        // STATE
        // ============================================================

        private enum LoadingState
        {
            Idle,
            FadingIn,
            WaitingForScene,
            FadingOut
        }

        private LoadingState state =
            LoadingState.Idle;

        private string pendingSceneName;

        private AsyncOperation asyncOp;

        private float fadeTimer;
        private float animFrameTimer;

        private int animFrameIndex;

        public bool IsLoading =>
            state != LoadingState.Idle;

        // ============================================================
        // EVENTS
        // ============================================================

        /// <summary>
        /// ยิงทันทีที่เริ่ม Scene Transition
        /// </summary>
        public event Action OnSceneEnter;

        /// <summary>
        /// ยิงเมื่อ Scene Transition เสร็จสมบูรณ์
        /// </summary>
        public event Action OnSceneExit;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);

            // --------------------------------------------------------
            // สำคัญ:
            // Persistent Manager ต้องเริ่มจาก Loading UI ที่ปิดอยู่
            // --------------------------------------------------------

            ResetVisualStateOnly();
        }

        private void Update()
        {
            switch (state)
            {
                case LoadingState.FadingIn:
                    UpdateFadeIn();
                    break;

                case LoadingState.WaitingForScene:
                    UpdateWaitingForScene();
                    break;

                case LoadingState.FadingOut:
                    UpdateFadeOut();
                    break;
            }

            if (state != LoadingState.Idle &&
                indicatorMode ==
                LoadingIndicatorMode.AnimatedSprite)
            {
                UpdateAnimatedSprite();
            }
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        private static LoadingScreenManager instance;
        /// <summary>
        /// โหลด Scene
        /// </summary>
        public void LoadScene(Scene scene)
        {
            LoadScene(scene.name);
        }

        /// <summary>
        /// โหลด Scene จาก Build Index
        /// </summary>
        public void LoadScene(int buildIndex)
        {
            if (buildIndex < 0 ||
                buildIndex >=
                SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError(
                    "LoadingScreenManager.LoadScene: " +
                    $"Build Index {buildIndex} ไม่ถูกต้อง"
                );

                return;
            }

            string path =
                SceneUtility.GetScenePathByBuildIndex(
                    buildIndex
                );

            string sceneName =
                System.IO.Path.GetFileNameWithoutExtension(
                    path
                );

            LoadScene(sceneName);
        }

        /// <summary>
        /// โหลด Scene จากชื่อ
        /// </summary>
        public void LoadScene(string sceneName)
        {
            // --------------------------------------------------------
            // ป้องกัน transition ซ้อน
            // --------------------------------------------------------

            if (state != LoadingState.Idle)
            {
                Debug.LogWarning(
                    "LoadingScreenManager: " +
                    "กำลังโหลดอยู่แล้ว - ข้ามคำขอซ้ำ"
                );

                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError(
                    "LoadingScreenManager.LoadScene: " +
                    "sceneName ว่างเปล่า"
                );

                return;
            }

            pendingSceneName = sceneName;

            // --------------------------------------------------------
            // Freeze เกมทันที
            // --------------------------------------------------------

            Time.timeScale = 0f;

            SetupIndicatorForShow();
            ShowPanel();

            OnSceneEnter?.Invoke();

            BeginAsyncLoad();

            // --------------------------------------------------------
            // Fade In
            // --------------------------------------------------------

            if (useFade &&
                canvasGroup != null)
            {
                fadeTimer = 0f;

                canvasGroup.alpha = 0f;

                state =
                    LoadingState.FadingIn;
            }
            else
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                state =
                    LoadingState.WaitingForScene;
            }
        }

        /// <summary>
        /// ============================================================
        /// RESET FOR MAIN MENU
        /// ============================================================
        ///
        /// ใช้เฉพาะเมื่อระบบกำลังจะกลับ Main Menu
        /// และไม่มี Scene Transition กำลังทำงานอยู่
        ///
        /// หน้าที่:
        /// - ล้าง Loading State
        /// - ล้าง pending Scene
        /// - ซ่อน Loading Panel
        /// - Reset CanvasGroup
        /// - Reset Animation
        /// - คืน Time.timeScale
        ///
        /// ไม่ทำลาย GameObject ที่เป็น DontDestroyOnLoad
        /// </summary>
        public void ResetForMainMenu()
        {
            // --------------------------------------------------------
            // ไม่ควรถูกเรียกขณะกำลังโหลดจริง
            // --------------------------------------------------------

            if (IsLoading)
            {
                Debug.LogWarning(
                    "LoadingScreenManager.ResetForMainMenu: " +
                    "ยังมี Scene Transition ทำงานอยู่ " +
                    "จึงไม่ Reset เพื่อป้องกัน AsyncOperation เสีย state"
                );

                return;
            }

            state =
                LoadingState.Idle;

            pendingSceneName = null;
            asyncOp = null;

            fadeTimer = 0f;
            animFrameTimer = 0f;
            animFrameIndex = 0;

            ResetVisualStateOnly();

            Time.timeScale = 1f;
        }

        // ============================================================
        // FADE
        // ============================================================

        private void UpdateFadeIn()
        {
            if (canvasGroup == null)
            {
                state =
                    LoadingState.WaitingForScene;

                return;
            }

            fadeTimer +=
                Time.unscaledDeltaTime;

            float t =
                fadeDuration > 0f
                    ? Mathf.Clamp01(
                        fadeTimer /
                        fadeDuration
                    )
                    : 1f;

            canvasGroup.alpha = t;

            if (t >= 1f)
            {
                state =
                    LoadingState.WaitingForScene;
            }
        }

        private void UpdateFadeOut()
        {
            if (canvasGroup == null)
            {
                FinishTransition();
                return;
            }

            fadeTimer +=
                Time.unscaledDeltaTime;

            float t =
                fadeDuration > 0f
                    ? Mathf.Clamp01(
                        fadeTimer /
                        fadeDuration
                    )
                    : 1f;

            canvasGroup.alpha =
                1f - t;

            if (t >= 1f)
                FinishTransition();
        }

        // ============================================================
        // ASYNC LOAD
        // ============================================================

        private void BeginAsyncLoad()
        {
            asyncOp =
                SceneManager.LoadSceneAsync(
                    pendingSceneName
                );

            if (asyncOp == null)
            {
                Debug.LogError(
                    "LoadingScreenManager: " +
                    "ไม่สามารถสร้าง AsyncOperation ได้"
                );

                FinishTransition();
                return;
            }

            asyncOp.allowSceneActivation = false;
        }

        private void UpdateWaitingForScene()
        {
            if (asyncOp == null)
                return;

            // --------------------------------------------------------
            // Unity จะหยุด progress ที่ประมาณ 0.9
            // เมื่อ allowSceneActivation = false
            // --------------------------------------------------------

            if (!asyncOp.allowSceneActivation &&
                asyncOp.progress >= 0.9f)
            {
                asyncOp.allowSceneActivation = true;
            }

            if (!asyncOp.isDone)
                return;

            asyncOp = null;

            if (useFade &&
                canvasGroup != null)
            {
                fadeTimer = 0f;

                state =
                    LoadingState.FadingOut;
            }
            else
            {
                FinishTransition();
            }
        }

        private void FinishTransition()
        {
            Time.timeScale = 1f;

            HidePanel();

            state =
                LoadingState.Idle;

            pendingSceneName = null;

            OnSceneExit?.Invoke();
        }

        // ============================================================
        // PANEL / INDICATOR
        // ============================================================

        private void ShowPanel()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(true);
        }

        private void HidePanel()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);
        }

        /// <summary>
        /// Reset เฉพาะ Visual/UI
        /// ไม่แตะ Loading State
        /// </summary>
        private void ResetVisualStateOnly()
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(false);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            fadeTimer = 0f;
            animFrameTimer = 0f;
            animFrameIndex = 0;
        }

        private void SetupIndicatorForShow()
        {
            switch (indicatorMode)
            {
                case LoadingIndicatorMode.PlainText:

                    if (loadingText != null)
                        loadingText.text =
                            loadingMessage;

                    break;

                case LoadingIndicatorMode.StaticSprite:

                    if (staticImage != null)
                        staticImage.sprite =
                            staticSprite;

                    break;

                case LoadingIndicatorMode.AnimatedSprite:

                    animFrameTimer = 0f;
                    animFrameIndex = 0;

                    if (animatedImage != null &&
                        animationFrames != null &&
                        animationFrames.Count > 0)
                    {
                        animatedImage.sprite =
                            animationFrames[0];
                    }

                    break;
            }
        }

        private void UpdateAnimatedSprite()
        {
            if (animatedImage == null ||
                animationFrames == null ||
                animationFrames.Count == 0 ||
                frameRate <= 0f)
            {
                return;
            }

            animFrameTimer +=
                Time.unscaledDeltaTime;

            float frameDuration =
                1f / frameRate;

            if (animFrameTimer >= frameDuration)
            {
                animFrameTimer -=
                    frameDuration;

                animFrameIndex =
                    (animFrameIndex + 1) %
                    animationFrames.Count;

                animatedImage.sprite =
                    animationFrames[
                        animFrameIndex
                    ];
            }
        }
    }
}