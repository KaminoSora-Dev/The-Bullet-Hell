using UnityEngine;
using UnityEngine.UI;
using GameFlowSystem;
using GameSystem;
using PlayerSystem;
using UISystem;

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
    ///
    /// Flow ตอนเข้า Scene:
    /// - TransitionBox (ถ้ามี) จะเคลื่อนจากตำแหน่งตัวเองไป endPoint อัตโนมัติ (เผยหน้าเมนู)
    /// - ระหว่างนั้นปุ่ม/คีย์บอร์ดจะถูกล็อกไว้ก่อน จนกว่า Box จะถึงปลายทาง
    ///
    /// Flow ตอนกด Start:
    /// 1. ล็อก Input ทั้งหมด (ปุ่ม + คีย์บอร์ด)
    /// 2. สั่ง TransitionBox เคลื่อนกลับไปตำแหน่งเริ่มต้น (ปิดจอ)
    /// 3. พอ Box ถึงตำแหน่งเริ่มต้นแล้ว -> Fade เข้าภาพบอกปุ่มบังคับ (Controls Info)
    /// 4. รอผู้เล่นกด Z -> Fade ภาพออก
    /// 5. Reset Score/Stats แล้วเริ่มเปลี่ยน Scene ผ่าน GameFlowManager (ขึ้นหน้าโหลดตามปกติ)
    ///
    /// Flow ตอนกด Exit:
    /// 1. ล็อก Input ทั้งหมด
    /// 2. สั่ง TransitionBox เคลื่อนกลับไปตำแหน่งเริ่มต้น
    /// 3. พอถึงแล้ว -> Quit เกม (ไม่ผ่าน Controls Info / ไม่ผ่านหน้าโหลด)
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        private enum MenuFlowState
        {
            Idle,
            ClosingBoxForStart,
            ControlsFadeIn,
            WaitingForContinue,
            ControlsFadeOut,
            ClosingBoxForExit
        }

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Keyboard Navigation")]
        [Tooltip(
            "KeyboardMenuNavigator ที่แปะอยู่บน Panel ของเมนูนี้ (ไม่ใส่ก็ได้ ถ้าไม่ต้องการเลือกด้วยคีย์บอร์ด)"
        )]
        [SerializeField] private KeyboardMenuNavigator keyboardNavigator;

        [Header("Transition Box (World Space Sprite)")]
        [Tooltip("กล่องดำที่เคลื่อนที่ตอนเข้า/ออกเมนู - ไม่ใส่ก็ได้ ถ้าไม่ต้องการ Transition นี้")]
        [SerializeField] private TransitionBoxController transitionBox;

        [Header("Controls Info Panel")]
        [Tooltip(
            "Panel ที่มีภาพบอกปุ่มบังคับ (ผู้ใช้เตรียม Image ที่มีข้อความ \"กด Z เพื่อไปต่อ\" อยู่ในภาพเอง) " +
            "ต้องมี CanvasGroup ติดอยู่บน Panel นี้เพื่อใช้ Fade"
        )]
        [SerializeField] private GameObject controlsInfoPanel;
        [SerializeField] private CanvasGroup controlsInfoCanvasGroup;

        [Min(0f)]
        [SerializeField] private float controlsFadeDuration = 0.3f;

        [Tooltip(
            "จำกัดเวลาสูงสุดที่นับต่อ 1 เฟรม (วินาที) - กันปัญหาเฟรมแรกหลัง Panel เปิด " +
            "ที่ deltaTime มักกระโดดสูงผิดปกติ ไม่งั้น Fade จะเหมือนกระโดด Instant แทนที่จะค่อยๆ จางเข้า/ออก"
        )]
        [SerializeField] private float maxDeltaTimePerFrame = 0.05f;

        [Tooltip("ปุ่มที่ใช้กดไปต่อจากหน้า Controls Info")]
        [SerializeField] private KeyCode continueKey = KeyCode.Z;

        [Header("Scene")]
        [Tooltip(
            "ชื่อ Scene ที่จะโหลดตอนกด Start (ต้องอยู่ใน Build Settings ด้วย) " +
            "- ใช้เป็น Fallback เฉพาะตอนไม่มี GameFlowManager ใน Scene เท่านั้น"
        )]
        [SerializeField] private string stageSceneName = "Stage1";

        private MenuFlowState flowState = MenuFlowState.Idle;
        private float fadeTimer;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            if (controlsInfoPanel != null)
                controlsInfoPanel.SetActive(false);

            if (controlsInfoCanvasGroup != null)
            {
                controlsInfoCanvasGroup.alpha = 0f;
                controlsInfoCanvasGroup.blocksRaycasts = false;
                controlsInfoCanvasGroup.interactable = false;
            }

            // ถ้า TransitionBox จะ Reveal ตอนเริ่ม Scene (revealOnStart)
            // ต้องล็อกปุ่ม/คีย์บอร์ดไว้ก่อน จนกว่า Box จะเคลื่อนที่มาถึงปลายทาง
            // เช็คจาก Property ตรงๆ กันปัญหา Script Execution Order ระหว่าง Component
            bool startLocked = transitionBox != null && transitionBox.RevealOnStart;

            SetButtonsInteractable(!startLocked);

            if (keyboardNavigator != null)
                keyboardNavigator.SetInputEnabled(!startLocked);
        }

        private void OnEnable()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            if (exitButton != null)
                exitButton.onClick.AddListener(OnExitClicked);

            if (transitionBox != null)
            {
                transitionBox.OnArrivedAtEnd += HandleBoxArrivedAtEnd;
                transitionBox.OnArrivedAtStart += HandleBoxArrivedAtStart;
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnStartClicked);

            if (exitButton != null)
                exitButton.onClick.RemoveListener(OnExitClicked);

            if (transitionBox != null)
            {
                transitionBox.OnArrivedAtEnd -= HandleBoxArrivedAtEnd;
                transitionBox.OnArrivedAtStart -= HandleBoxArrivedAtStart;
            }
        }

        private void Update()
        {
            switch (flowState)
            {
                case MenuFlowState.ControlsFadeIn:
                    UpdateControlsFadeIn();
                    break;

                case MenuFlowState.WaitingForContinue:
                    UpdateWaitingForContinue();
                    break;

                case MenuFlowState.ControlsFadeOut:
                    UpdateControlsFadeOut();
                    break;
            }
        }

        // ============================================================
        // BUTTON HANDLERS
        // ============================================================

        public void OnStartClicked()
        {
            if (!CanAcceptInput())
                return;

            LockAllInput();

            if (transitionBox != null)
            {
                flowState = MenuFlowState.ClosingBoxForStart;
                transitionBox.MoveToStart();
            }
            else
            {
                // ไม่มี Transition Box ให้ใช้ - ข้ามไปโชว์ Controls Info เลย
                BeginControlsInfo();
            }
        }

        public void OnExitClicked()
        {
            if (!CanAcceptInput())
                return;

            LockAllInput();

            if (transitionBox != null)
            {
                flowState = MenuFlowState.ClosingBoxForExit;
                transitionBox.MoveToStart();
            }
            else
            {
                QuitGame();
            }
        }

        private bool CanAcceptInput()
        {
            if (flowState != MenuFlowState.Idle)
                return false;

            if (transitionBox != null && transitionBox.IsMoving)
                return false;

            return true;
        }

        // ============================================================
        // TRANSITION BOX CALLBACKS
        // ============================================================

        private void HandleBoxArrivedAtEnd()
        {
            // Box เผยหน้าเมนูเสร็จตอนเพิ่งเข้า Scene -> ปลดล็อกปุ่ม/คีย์บอร์ดให้กดได้
            SetButtonsInteractable(true);

            if (keyboardNavigator != null)
                keyboardNavigator.SetInputEnabled(true);
        }

        private void HandleBoxArrivedAtStart()
        {
            if (flowState == MenuFlowState.ClosingBoxForStart)
            {
                BeginControlsInfo();
            }
            else if (flowState == MenuFlowState.ClosingBoxForExit)
            {
                QuitGame();
            }
        }

        // ============================================================
        // CONTROLS INFO PANEL (Fade In -> รอกด Z -> Fade Out)
        // ============================================================

        private void BeginControlsInfo()
        {
            if (controlsInfoPanel == null || controlsInfoCanvasGroup == null)
            {
                // ไม่ได้ตั้งค่า Panel ไว้ - ข้ามไปโหลด Scene เลย
                ProceedToStage();
                return;
            }

            controlsInfoPanel.SetActive(true);
            controlsInfoCanvasGroup.alpha = 0f;
            controlsInfoCanvasGroup.blocksRaycasts = true;
            controlsInfoCanvasGroup.interactable = true;

            fadeTimer = 0f;
            flowState = MenuFlowState.ControlsFadeIn;
        }

        private void UpdateControlsFadeIn()
        {
            // Clamp deltaTime ต่อเฟรม กันเฟรมแรก Spike สูงผิดปกติ (Panel เพิ่งเปิด/Scene เพิ่งโหลด)
            // ทำให้ Fade เหมือนกระโดด Instant แทนที่จะค่อยๆ จางเข้าตามเวลาปกติ
            float dt = Mathf.Min(Time.unscaledDeltaTime, maxDeltaTimePerFrame);
            fadeTimer += dt;

            float t = controlsFadeDuration > 0f
                ? Mathf.Clamp01(fadeTimer / controlsFadeDuration)
                : 1f;

            controlsInfoCanvasGroup.alpha = t;

            if (t >= 1f)
                flowState = MenuFlowState.WaitingForContinue;
        }

        private void UpdateWaitingForContinue()
        {
            // ค้างอยู่หน้านี้จนกว่าจะกด continueKey (ตามที่ต้องการ - ไม่กดก็ไม่ไปต่อ)
            if (!Input.GetKeyDown(continueKey))
                return;

            fadeTimer = 0f;
            flowState = MenuFlowState.ControlsFadeOut;
        }

        private void UpdateControlsFadeOut()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, maxDeltaTimePerFrame);
            fadeTimer += dt;

            float t = controlsFadeDuration > 0f
                ? Mathf.Clamp01(fadeTimer / controlsFadeDuration)
                : 1f;

            controlsInfoCanvasGroup.alpha = 1f - t;

            if (t >= 1f)
            {
                controlsInfoCanvasGroup.blocksRaycasts = false;
                controlsInfoCanvasGroup.interactable = false;
                controlsInfoPanel.SetActive(false);

                ProceedToStage();
            }
        }

        // ============================================================
        // PROCEED / QUIT
        // ============================================================

        private void ProceedToStage()
        {
            flowState = MenuFlowState.Idle;

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

        private void QuitGame()
        {
            flowState = MenuFlowState.Idle;

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ============================================================
        // INPUT LOCK
        // ============================================================

        private void LockAllInput()
        {
            SetButtonsInteractable(false);

            if (keyboardNavigator != null)
                keyboardNavigator.SetInputEnabled(false);
        }

        private void SetButtonsInteractable(bool value)
        {
            if (startButton != null) startButton.interactable = value;
            if (exitButton != null) exitButton.interactable = value;
        }
    }
}