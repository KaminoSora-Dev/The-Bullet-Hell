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
    /// MenuManager ไม่ใช้ DontDestroyOnLoad
    /// เพราะ UI ของ Main Menu เป็นของ Scene โดยตรง
    ///
    /// ทุกครั้งที่ Main Menu ถูกสร้าง/เปิด:
    /// - Reset UI
    /// - Reset Controls Info
    /// - Reset Button state
    /// - Reset KeyboardMenuNavigator
    /// - ตรวจสถานะ TransitionBox
    ///
    /// สำคัญ:
    /// Menu จะไม่พึ่ง TransitionBox Event เพียงอย่างเดียว
    /// แต่จะตรวจสถานะ IsAtEnd เป็น safety fallback ด้วย
    ///
    /// ============================================================
    /// STAGE SELECT (เพิ่มใหม่)
    /// ============================================================
    /// กด "Stage Select" -> ซ่อน Panel ปุ่มหลักของตัวเอง + สั่ง
    /// StageSelectController.Open() แทน ตัว StageSelectController
    /// คุม Panel/Navigator ของหน้า Stage List เองทั้งหมด - พอกด Go
    /// Back จะยิง OnClosed event กลับมาให้ MenuManager เปิด Panel
    /// ปุ่มหลักคืน (Event-driven decoupling ตาม convention เดิม
    /// เหมือน TransitionBox.OnArrivedAtEnd/Start)
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        private enum MenuFlowState
        {
            Idle,
            WaitingForBoxReveal,
            ClosingBoxForStart,
            ControlsFadeIn,
            WaitingForContinue,
            ControlsFadeOut,
            ClosingBoxForExit
        }

        // ============================================================
        // BUTTONS
        // ============================================================

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        // ============================================================
        // STAGE SELECT (เพิ่มใหม่)
        // ============================================================

        [Header("Stage Select")]
        [SerializeField] private Button stageSelectButton;

        [Tooltip("Panel ที่รวมปุ่มหลักของ Main Menu (Start/Stage Select/Exit) - ถูกซ่อนตอนเปิด Stage Select แล้วเปิดกลับตอน Go Back")]
        [SerializeField] private GameObject mainMenuButtonsPanel;

        [SerializeField] private StageSelectController stageSelectController;

        // ============================================================
        // KEYBOARD
        // ============================================================

        [Header("Keyboard Navigation")]
        [SerializeField]
        private KeyboardMenuNavigator keyboardNavigator;

        // ============================================================
        // TRANSITION BOX
        // ============================================================

        [Header("Transition Box")]
        [SerializeField]
        private TransitionBoxController transitionBox;

        // ============================================================
        // CONTROLS INFO
        // ============================================================

        [Header("Controls Info Panel")]

        [SerializeField]
        private GameObject controlsInfoPanel;

        [SerializeField]
        private CanvasGroup controlsInfoCanvasGroup;

        [Min(0f)]
        [SerializeField]
        private float controlsFadeDuration = 0.3f;

        [SerializeField]
        private float maxDeltaTimePerFrame = 0.05f;

        [Tooltip(
            "ปุ่มที่ใช้กดไปต่อจาก Controls Info"
        )]
        [SerializeField]
        private KeyCode continueKey =
            KeyCode.Z;

        // ============================================================
        // SCENE
        // ============================================================

        [Header("Scene")]

        [SerializeField]
        private string stageSceneName = "Stage1";

        // ============================================================
        // RUNTIME
        // ============================================================

        private MenuFlowState flowState =
            MenuFlowState.Idle;

        private float fadeTimer;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            ResetMenuState();

            InitializeMenuEntryState();
        }

        private void OnEnable()
        {
            // --------------------------------------------------------
            // Reset ทุกครั้งที่ Main Menu ถูกเปิด
            // --------------------------------------------------------

            ResetMenuState();

            flowState =
                MenuFlowState.Idle;

            fadeTimer = 0f;

            // --------------------------------------------------------
            // Button Events
            // --------------------------------------------------------

            if (startButton != null)
            {
                startButton.onClick.AddListener(
                    OnStartClicked
                );
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(
                    OnExitClicked
                );
            }

            if (stageSelectButton != null)
            {
                stageSelectButton.onClick.AddListener(
                    OnStageSelectClicked
                );
            }

            if (stageSelectController != null)
            {
                stageSelectController.OnClosed +=
                    HandleStageSelectClosed;
            }

            // --------------------------------------------------------
            // Transition Events
            // --------------------------------------------------------

            if (transitionBox != null)
            {
                transitionBox.OnArrivedAtEnd +=
                    HandleBoxArrivedAtEnd;

                transitionBox.OnArrivedAtStart +=
                    HandleBoxArrivedAtStart;
            }

            // --------------------------------------------------------
            // Initialize Menu Input
            // --------------------------------------------------------

            InitializeMenuEntryState();
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(
                    OnStartClicked
                );
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(
                    OnExitClicked
                );
            }

            if (stageSelectButton != null)
            {
                stageSelectButton.onClick.RemoveListener(
                    OnStageSelectClicked
                );
            }

            if (stageSelectController != null)
            {
                stageSelectController.OnClosed -=
                    HandleStageSelectClosed;
            }

            if (transitionBox != null)
            {
                transitionBox.OnArrivedAtEnd -=
                    HandleBoxArrivedAtEnd;

                transitionBox.OnArrivedAtStart -=
                    HandleBoxArrivedAtStart;
            }
        }

        private void Update()
        {
            // --------------------------------------------------------
            // Safety:
            // ตรวจว่า TransitionBox เปิด Menu เสร็จแล้วหรือยัง
            //
            // ไม่พึ่ง OnArrivedAtEnd อย่างเดียว
            // --------------------------------------------------------

            if (flowState ==
                MenuFlowState.WaitingForBoxReveal)
            {
                UpdateWaitingForBoxReveal();
            }

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
        // MENU INITIALIZATION
        // ============================================================

        /// <summary>
        /// Reset UI ทั้งหมดให้เหมือน Main Menu เพิ่งถูกสร้าง
        /// </summary>
        private void ResetMenuState()
        {
            // --------------------------------------------------------
            // Controls Info
            // --------------------------------------------------------

            if (controlsInfoPanel != null)
                controlsInfoPanel.SetActive(false);

            if (controlsInfoCanvasGroup != null)
            {
                controlsInfoCanvasGroup.alpha = 0f;

                controlsInfoCanvasGroup.blocksRaycasts =
                    false;

                controlsInfoCanvasGroup.interactable =
                    false;
            }

            fadeTimer = 0f;

            // --------------------------------------------------------
            // Stage Select (เพิ่มใหม่) - ให้ Panel ปุ่มหลักโชว์อยู่เสมอตอนเริ่ม
            // --------------------------------------------------------

            if (mainMenuButtonsPanel != null)
                mainMenuButtonsPanel.SetActive(true);

            // --------------------------------------------------------
            // Buttons
            // --------------------------------------------------------

            SetButtonsInteractable(false);

            // --------------------------------------------------------
            // Keyboard
            // --------------------------------------------------------

            if (keyboardNavigator != null)
            {
                keyboardNavigator.SetInputEnabled(
                    false
                );
            }
        }

        /// <summary>
        /// กำหนดสถานะเริ่มต้นของ Menu
        /// หลังจาก UI ถูก reset แล้ว
        /// </summary>
        private void InitializeMenuEntryState()
        {
            if (transitionBox == null)
            {
                UnlockMenuInput();
                return;
            }

            // --------------------------------------------------------
            // ถ้าไม่มี reveal animation
            // Menu พร้อมใช้งานทันที
            // --------------------------------------------------------

            if (!transitionBox.RevealOnStart)
            {
                UnlockMenuInput();
                return;
            }

            // --------------------------------------------------------
            // ถ้ากล่องอยู่ End อยู่แล้ว
            // ไม่ต้องรอ event
            // --------------------------------------------------------

            if (transitionBox.IsAtEnd)
            {
                UnlockMenuInput();
                return;
            }

            // --------------------------------------------------------
            // กล่องกำลังเปิด / กำลังไป End
            // ล็อก input ไว้ก่อน
            // --------------------------------------------------------

            flowState =
                MenuFlowState.WaitingForBoxReveal;

            SetButtonsInteractable(false);

            if (keyboardNavigator != null)
            {
                keyboardNavigator.SetInputEnabled(
                    false
                );
            }
        }

        private void UpdateWaitingForBoxReveal()
        {
            if (transitionBox == null)
            {
                UnlockMenuInput();
                return;
            }

            if (transitionBox.IsAtEnd)
            {
                UnlockMenuInput();
            }
        }

        /// <summary>
        /// ปลดล็อก Menu หลัง TransitionBox พร้อม
        /// </summary>
        private void UnlockMenuInput()
        {
            flowState =
                MenuFlowState.Idle;

            SetButtonsInteractable(true);

            if (keyboardNavigator != null)
            {
                keyboardNavigator.SetInputEnabled(
                    true
                );

                keyboardNavigator.ResetForMenuEntry();
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
                flowState =
                    MenuFlowState.ClosingBoxForStart;

                transitionBox.MoveToStart();
            }
            else
            {
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
                flowState =
                    MenuFlowState.ClosingBoxForExit;

                transitionBox.MoveToStart();
            }
            else
            {
                QuitGame();
            }
        }

        /// <summary>
        /// กด "Stage Select" - ซ่อน Panel ปุ่มหลักแล้วเปิดหน้า Stage List แทน
        /// ไม่ผ่าน TransitionBox (Panel ธรรมดา SetActive สลับตามที่ตกลงกันไว้)
        /// </summary>
        public void OnStageSelectClicked()
        {
            if (!CanAcceptInput())
                return;

            LockAllInput();

            if (stageSelectController == null)
            {
                Debug.LogWarning(
                    "MenuManager: ไม่ได้ผูก stageSelectController ไว้ - เปิดหน้า Stage Select ไม่ได้"
                );

                UnlockMenuInput();
                return;
            }

            if (mainMenuButtonsPanel != null)
                mainMenuButtonsPanel.SetActive(false);

            stageSelectController.Open();
        }

        /// <summary>
        /// เรียกจาก StageSelectController.OnClosed ตอนกด Go Back
        /// </summary>
        private void HandleStageSelectClosed()
        {
            if (mainMenuButtonsPanel != null)
                mainMenuButtonsPanel.SetActive(true);

            UnlockMenuInput();
        }

        private bool CanAcceptInput()
        {
            if (flowState !=
                MenuFlowState.Idle)
            {
                return false;
            }

            if (transitionBox != null &&
                transitionBox.IsMoving)
            {
                return false;
            }

            return true;
        }

        // ============================================================
        // TRANSITION BOX CALLBACKS
        // ============================================================

        private void HandleBoxArrivedAtEnd()
        {
            if (flowState ==
                MenuFlowState.WaitingForBoxReveal)
            {
                UnlockMenuInput();
            }
        }

        private void HandleBoxArrivedAtStart()
        {
            if (flowState ==
                MenuFlowState.ClosingBoxForStart)
            {
                BeginControlsInfo();
            }
            else if (
                flowState ==
                MenuFlowState.ClosingBoxForExit)
            {
                QuitGame();
            }
        }

        // ============================================================
        // CONTROLS INFO
        // ============================================================

        private void BeginControlsInfo()
        {
            if (controlsInfoPanel == null ||
                controlsInfoCanvasGroup == null)
            {
                ProceedToStage();
                return;
            }

            controlsInfoPanel.SetActive(true);

            controlsInfoCanvasGroup.alpha = 0f;

            controlsInfoCanvasGroup.blocksRaycasts =
                true;

            controlsInfoCanvasGroup.interactable =
                true;

            fadeTimer = 0f;

            flowState =
                MenuFlowState.ControlsFadeIn;
        }

        private void UpdateControlsFadeIn()
        {
            if (controlsInfoCanvasGroup == null)
            {
                ProceedToStage();
                return;
            }

            float dt =
                Mathf.Min(
                    Time.unscaledDeltaTime,
                    maxDeltaTimePerFrame
                );

            fadeTimer += dt;

            float t =
                controlsFadeDuration > 0f
                    ? Mathf.Clamp01(
                        fadeTimer /
                        controlsFadeDuration
                    )
                    : 1f;

            controlsInfoCanvasGroup.alpha =
                t;

            if (t >= 1f)
            {
                flowState =
                    MenuFlowState.WaitingForContinue;
            }
        }

        private void UpdateWaitingForContinue()
        {
            if (!Input.GetKeyDown(
                continueKey))
            {
                return;
            }

            fadeTimer = 0f;

            flowState =
                MenuFlowState.ControlsFadeOut;
        }

        private void UpdateControlsFadeOut()
        {
            if (controlsInfoCanvasGroup == null)
            {
                ProceedToStage();
                return;
            }

            float dt =
                Mathf.Min(
                    Time.unscaledDeltaTime,
                    maxDeltaTimePerFrame
                );

            fadeTimer += dt;

            float t =
                controlsFadeDuration > 0f
                    ? Mathf.Clamp01(
                        fadeTimer /
                        controlsFadeDuration
                    )
                    : 1f;

            controlsInfoCanvasGroup.alpha =
                1f - t;

            if (t >= 1f)
            {
                controlsInfoCanvasGroup.blocksRaycasts =
                    false;

                controlsInfoCanvasGroup.interactable =
                    false;

                if (controlsInfoPanel != null)
                {
                    controlsInfoPanel.SetActive(
                        false
                    );
                }

                ProceedToStage();
            }
        }

        // ============================================================
        // PROCEED
        // ============================================================

        private void ProceedToStage()
        {
            // --------------------------------------------------------
            // Menu flow จบแล้ว
            // GameFlowManager จะเป็นคนจัดการ Run reset
            // --------------------------------------------------------

            flowState =
                MenuFlowState.Idle;

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartOver();
                return;
            }

            // --------------------------------------------------------
            // Fallback กรณีไม่มี GameFlowManager
            // --------------------------------------------------------

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetScore();

            if (PlayerStatsManager.Instance != null)
                PlayerStatsManager.Instance.ResetRun();

            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.LoadScene(
                    stageSceneName
                );
            }
            else
            {
                Debug.LogError(
                    "MenuManager: ไม่พบ " +
                    "GameFlowManager หรือ " +
                    "LoadingScreenManager.Instance"
                );
            }
        }

        // ============================================================
        // QUIT
        // ============================================================

        private void QuitGame()
        {
            flowState =
                MenuFlowState.Idle;

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
            {
                keyboardNavigator.SetInputEnabled(
                    false
                );
            }
        }

        private void SetButtonsInteractable(
            bool value)
        {
            if (startButton != null)
                startButton.interactable = value;

            if (exitButton != null)
                exitButton.interactable = value;

            if (stageSelectButton != null)
                stageSelectButton.interactable = value;
        }
    }
}