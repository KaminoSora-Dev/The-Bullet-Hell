using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace GameFlowSystem
{
    /// <summary>
    /// รูปแบบตัวบอกสถานะโหลด - เลือกได้ว่าจะใช้ Text ธรรมดา, Sprite Animation หรือ Sprite นิ่งที่เลือกเอง
    /// </summary>
    public enum LoadingIndicatorMode
    {
        PlainText,
        AnimatedSprite,
        StaticSprite
    }

    /// <summary>
    /// Singleton ถาวร (DontDestroyOnLoad) - จัดการหน้าโหลดระหว่างเปลี่ยน Scene ทั้งหมด
    ///
    /// พฤติกรรมหลัก:
    /// - Freeze เกมทั้งหมด (Time.timeScale = 0) ทันทีที่เริ่มเปลี่ยน Scene
    /// - โหลด Scene ใหม่แบบ Async อยู่เบื้องหลัง โดยห้าม Activate จนกว่า Fade In จะจบก่อน
    /// - Time.timeScale จะกลับมาเป็น 1 ก็ต่อเมื่อ Scene ใหม่โหลดเสร็จ (และ Fade Out จบแล้วถ้าเปิดใช้ Fade) เท่านั้น
    /// - ไม่ใช้ Coroutine ตาม convention ของโปรเจกต์ - ทำงานผ่าน state machine (enum) ใน Update() ทั้งหมด
    ///
    /// Setup:
    /// 1. สร้าง GameObject ว่างชื่อ "LoadingScreenManager" ใน Scene แรกของเกม (เช่น MainMenu) แล้วแปะสคริปต์นี้
    /// 2. ใต้ GameObject เดียวกัน (หรือลูก) สร้าง Canvas + Panel (พื้นดำ + Text/Image) เอง แล้วลาก Panel มาใส่ loadingPanel
    /// 3. ถ้าจะใช้ Fade ต้องมี CanvasGroup ติดอยู่บน Panel นั้นด้วย แล้วลากมาใส่ canvasGroup
    /// 4. ตั้ง Indicator Mode แล้วลาก Reference ของโหมดนั้นๆ ให้ครบ
    /// </summary>
    public class LoadingScreenManager : MonoBehaviour
    {
        public static LoadingScreenManager Instance { get; private set; }

        [Header("Panel (ผู้ใช้จัดวางตำแหน่งเอง)")]
        [Tooltip("Panel หลักที่มีพื้นดำ + Loading Text/Sprite อยู่ข้างใน - สคริปต์นี้แค่ SetActive เปิด/ปิดให้")]
        [SerializeField] private GameObject loadingPanel;

        [Tooltip("CanvasGroup บน loadingPanel - ใช้คุม alpha ตอน Fade เท่านั้น ถ้าไม่ใช้ Fade ปล่อยว่างไว้ได้")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Indicator Mode")]
        [SerializeField] private LoadingIndicatorMode indicatorMode = LoadingIndicatorMode.PlainText;

        [Header("Plain Text (TextMeshPro)")]
        [SerializeField] private TMP_Text loadingText;
        [Tooltip("ข้อความที่จะโชว์ตอนโหลด")]
        [SerializeField] private string loadingMessage = "Loading...";

        [Header("Static Sprite")]
        [Tooltip("ใช้ตอน Indicator Mode = Static Sprite")]
        [SerializeField] private Image staticImage;
        [SerializeField] private Sprite staticSprite;

        [Header("Animated Sprite")]
        [Tooltip("ใช้ตอน Indicator Mode = Animated Sprite - เผื่อไว้ในอนาคต ปกติแนะนำใช้ Plain Text")]
        [SerializeField] private Image animatedImage;
        [SerializeField] private List<Sprite> animationFrames = new List<Sprite>();
        [Tooltip("จำนวนเฟรมต่อวินาที")]
        [SerializeField] private float frameRate = 8f;

        [Header("Fade")]
        [Tooltip("ติ๊กถ้าอยากให้ Panel ค่อยๆ Fade เข้า/ออก แทนการโชว์/ซ่อนทันที")]
        [SerializeField] private bool useFade = true;
        [Min(0f)]
        [Tooltip("เวลา Fade เข้า/ออก (วินาที) - ใช้ Time.unscaledDeltaTime เพราะเกม Freeze อยู่ระหว่างนี้")]
        [SerializeField] private float fadeDuration = 0.3f;

        private enum LoadingState
        {
            Idle,
            FadingIn,
            WaitingForScene,
            FadingOut
        }

        private LoadingState state = LoadingState.Idle;

        private string pendingSceneName;
        private AsyncOperation asyncOp;

        private float fadeTimer;
        private float animFrameTimer;
        private int animFrameIndex;

        public bool IsLoading => state != LoadingState.Idle;

        /// <summary>โหลด Scene โดยใช้ Loading Screen กลาง</summary>
        public void LoadScene(Scene scene)
        {
            LoadScene(scene.name);
        }

        /// <summary>โหลด Scene จาก Build Index โดยใช้ Loading Screen กลาง</summary>
        public void LoadScene(int buildIndex)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"LoadingScreenManager.LoadScene: Build Index {buildIndex} ไม่ถูกต้อง");
                return;
            }

            string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
            LoadScene(sceneName);
        }

        /// <summary>ยิงทันทีที่เริ่มกระบวนการเปลี่ยน Scene (ก่อน Fade In / ก่อนเริ่มโหลดจริง) - ใช้เปลี่ยนเพลง/หยุดเสียงได้</summary>
        public event Action OnSceneEnter;

        /// <summary>ยิงตอนจบกระบวนการทั้งหมด (Fade Out จบแล้ว, Time.timeScale กลับเป็น 1 แล้ว)</summary>
        public event Action OnSceneExit;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadingPanel != null)
                loadingPanel.SetActive(false);
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

            if (state != LoadingState.Idle && indicatorMode == LoadingIndicatorMode.AnimatedSprite)
                UpdateAnimatedSprite();
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// เริ่มเปลี่ยนไปยัง Scene ที่ระบุ - Freeze เกมทันที, โชว์ Loading Panel, โหลด Scene ใหม่เบื้องหลัง
        /// แล้วค่อยคืน Time.timeScale ตอนทุกอย่างพร้อมจริงๆ เท่านั้น
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (state != LoadingState.Idle)
            {
                Debug.LogWarning("LoadingScreenManager: กำลังโหลดอยู่แล้ว - ข้ามคำขอซ้ำ");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("LoadingScreenManager.LoadScene: sceneName ว่างเปล่า");
                return;
            }

            pendingSceneName = sceneName;

            Time.timeScale = 0f; // Freeze ทั้งเกมทันที ตามที่ต้องการ

            SetupIndicatorForShow();
            ShowPanel();

            OnSceneEnter?.Invoke();

            BeginAsyncLoad();

            if (useFade && canvasGroup != null)
            {
                fadeTimer = 0f;
                canvasGroup.alpha = 0f;
                state = LoadingState.FadingIn;
            }
            else
            {
                if (canvasGroup != null) canvasGroup.alpha = 1f;
                state = LoadingState.WaitingForScene;
            }
        }

        // ============================================================
        // INTERNAL - FADE
        // ============================================================

        private void UpdateFadeIn()
        {
            fadeTimer += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeTimer / fadeDuration) : 1f;
            canvasGroup.alpha = t;

            if (t >= 1f)
            {
                // Fade In เสร็จแล้วเท่านั้นถึงยอมให้ Scene ใหม่ Activate ได้ ตามที่ต้องการ
                state = LoadingState.WaitingForScene;
            }
        }

        private void UpdateFadeOut()
        {
            fadeTimer += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeTimer / fadeDuration) : 1f;
            canvasGroup.alpha = 1f - t;

            if (t >= 1f)
                FinishTransition();
        }

        // ============================================================
        // INTERNAL - ASYNC SCENE LOAD
        // ============================================================

        private void BeginAsyncLoad()
        {
            asyncOp = SceneManager.LoadSceneAsync(pendingSceneName);
            asyncOp.allowSceneActivation = false; // ห้าม Scene ใหม่เริ่มจนกว่าเราจะสั่งเอง (รอ Fade In จบก่อน)
        }

        private void UpdateWaitingForScene()
        {
            if (asyncOp == null) return;

            // Unity ค้าง progress ไว้ที่ 0.9 เสมอตอน allowSceneActivation = false
            if (!asyncOp.allowSceneActivation && asyncOp.progress >= 0.9f)
                asyncOp.allowSceneActivation = true;

            if (asyncOp.isDone)
            {
                asyncOp = null;

                if (useFade && canvasGroup != null)
                {
                    fadeTimer = 0f;
                    state = LoadingState.FadingOut;
                }
                else
                {
                    FinishTransition();
                }
            }
        }

        private void FinishTransition()
        {
            Time.timeScale = 1f; // คืนเวลาปกติ - เกมใหม่ "เริ่ม" ตรงจุดนี้เท่านั้น
            HidePanel();

            state = LoadingState.Idle;
            OnSceneExit?.Invoke();
        }

        // ============================================================
        // INTERNAL - PANEL / INDICATOR
        // ============================================================

        private void ShowPanel()
        {
            if (loadingPanel != null) loadingPanel.SetActive(true);
        }

        private void HidePanel()
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        private void SetupIndicatorForShow()
        {
            switch (indicatorMode)
            {
                case LoadingIndicatorMode.PlainText:
                    if (loadingText != null)
                        loadingText.text = loadingMessage;
                    break;

                case LoadingIndicatorMode.StaticSprite:
                    if (staticImage != null)
                        staticImage.sprite = staticSprite;
                    break;

                case LoadingIndicatorMode.AnimatedSprite:
                    animFrameTimer = 0f;
                    animFrameIndex = 0;
                    if (animatedImage != null && animationFrames.Count > 0)
                        animatedImage.sprite = animationFrames[0];
                    break;
            }
        }

        private void UpdateAnimatedSprite()
        {
            if (animatedImage == null || animationFrames == null ||
                animationFrames.Count == 0 || frameRate <= 0f)
                return;

            animFrameTimer += Time.unscaledDeltaTime;
            float frameDuration = 1f / frameRate;

            if (animFrameTimer >= frameDuration)
            {
                animFrameTimer -= frameDuration;
                animFrameIndex = (animFrameIndex + 1) % animationFrames.Count;
                animatedImage.sprite = animationFrames[animFrameIndex];
            }
        }
    }
}