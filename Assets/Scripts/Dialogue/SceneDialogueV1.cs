using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StageSystem;

namespace DialogueSystem
{
    public enum DialogueSpeaker
    {
        Left,
        Right
    }


    [System.Serializable]
    public class SceneDialogueLine
    {
        [Tooltip("ตัวละครฝั่งไหนกำลังพูดบรรทัดนี้ (ฝั่งนี้จะไม่ถูก Dim)")]
        public DialogueSpeaker speaker = DialogueSpeaker.Left;

        [Tooltip("ชื่อตัวละครที่จะแสดงบน Name Text")]
        public string speakerName = "???";

        [TextArea(2, 5)]
        [Tooltip("ข้อความบทพูดของบรรทัดนี้")]
        public string text = "";

        [Header("การแสดงผล Portrait ของบรรทัดนี้")]
        [Tooltip("ให้แสดง Portrait ฝั่งซ้ายในบรรทัดนี้หรือไม่")]
        public bool showLeftPortrait = true;

        [Tooltip("ให้แสดง Portrait ฝั่งขวาในบรรทัดนี้หรือไม่")]
        public bool showRightPortrait = true;

        [Header("รูปภาพ (ใช้เมื่อไม่ได้ติ๊ก Use Animator Portrait)")]
        [Tooltip("Sprite ที่จะใช้กับฝั่งที่กำลังพูด (speaker) ในบรรทัดนี้")]
        public Sprite portraitSprite;

        [Header("Animation (ถ้าตัวละครฝั่งที่พูดมี Animator)")]
        [Tooltip("ถ้าติ๊กไว้ จะสั่ง Animator.SetTrigger(animatorTriggerName) แทนการเซ็ต Sprite ตรงๆ")]
        public bool useAnimatorPortrait = false;
        public string animatorTriggerName = "";

        [Header("เสียงตัวอักษรโผล่ (เว้นว่าง = ใช้ค่า Default ของระบบ)")]
        public AudioClip typeSfxOverride;
    }


    [System.Serializable]
    public class DialogueSequence
    {
        [Tooltip("ชื่อชุดบทสนทนา (ไว้ดูใน Inspector เท่านั้น)")]
        public string sequenceName = "New Dialogue";

        [Tooltip("เวลา (อ้างอิงจาก StageSpawnerV1.StageTimer) ที่จะเริ่มเล่นชุดนี้")]
        public float startTime = 0f;

        public List<SceneDialogueLine> lines = new List<SceneDialogueLine>();

        [System.NonSerialized]
        public bool hasTriggered;
    }


    /// <summary>
    /// ระบบ Dialogue ที่เล่นตามเวลาซึ่งอ้างอิงจาก StageSpawnerV1.StageTimer
    ///
    /// พฤติกรรมหลัก:
    /// - เมื่อถึงเวลาที่ตั้งไว้ จะสั่ง StageSpawnerV1.Instance.PauseTimer()
    ///   (StageTimer และ Spawn Timeline จะหยุดจนกว่า Dialogue จะจบ)
    /// - ยิง Event OnDialogueSequenceStart / OnDialogueSequenceEnd
    ///   ให้ระบบอื่น (เช่น EnemyV1 / EnemyBulletV2) subscribe ไปเคลียร์ตัวเอง
    ///   ออกจากจอเอง (ระบบนี้ไม่รู้จัก EnemyV1/EnemyBulletV2 โดยตรง
    ///   เพื่อคงหลัก Event-driven decoupling ของโปรเจกต์)
    /// - Text ไหลทีละตัวอักษร, กด Z/X/Mouse เพื่อเร่งให้ตัวอักษรโผล่ครบ
    ///   หรือกดไปบรรทัดถัดไปเมื่อตัวอักษรครบแล้ว
    /// - กด Ctrl ค้างเพื่อเร่งความเร็ว Text ระหว่างกำลังไหล (สไตล์ Touhou)
    /// - ไม่มีการใช้ Coroutine ทั้งหมดควบคุมผ่าน State Machine ใน Update()
    /// </summary>
    public class SceneDialogueV1 : MonoBehaviour
    {
        private enum State
        {
            Idle,
            PortraitFadeIn,
            Typing,
            WaitingForInput
        }


        // ============================================================
        // EVENTS
        // ============================================================
        //
        // ให้ EnemyV1 / EnemyBulletV2 (หรือระบบอื่น) subscribe ใน OnEnable()
        // แล้วไป unsubscribe ใน OnDisable() เพื่อเคลียร์ตัวเองออกจากจอ
        // ตอน Dialogue เริ่ม เช่น:
        //
        //     private void OnEnable()  { SceneDialogueV1.OnDialogueSequenceStart += HandleDialogueStart; }
        //     private void OnDisable() { SceneDialogueV1.OnDialogueSequenceStart -= HandleDialogueStart; }
        //     private void HandleDialogueStart() { /* return ตัวเองเข้า Pool ตาม logic ที่มีอยู่ */ }
        //
        public static event Action OnDialogueSequenceStart;
        public static event Action OnDialogueSequenceEnd;


        [Header("Dialogue Timeline")]
        [Tooltip("รายการชุดบทสนทนา เรียงตามเวลาที่ต้องการให้เล่น")]
        [SerializeField]
        private List<DialogueSequence> sequences = new List<DialogueSequence>();


        [Header("UI References - กล่องข้อความ")]
        [Tooltip("Root ของกล่อง Dialogue ทั้งหมด (ปกติปิด Inactive ไว้ใน Scene)")]
        [SerializeField] private GameObject dialoguePanelRoot;
        [Tooltip("ใส่ได้ถ้าต้องการ Fade กล่องข้อความ (ไม่ใส่ก็ได้)")]
        [SerializeField] private CanvasGroup dialoguePanelCanvasGroup;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text bodyText;


        [Header("UI References - Portrait ฝั่งซ้าย")]
        [SerializeField] private GameObject leftPortraitRoot;
        [SerializeField] private CanvasGroup leftPortraitCanvasGroup;
        [SerializeField] private Image leftPortraitImage;
        [Tooltip("ใส่ได้ถ้าตัวละครฝั่งนี้มี Animation (ไม่ใส่ก็ได้ถ้าใช้ Sprite เฉยๆ)")]
        [SerializeField] private Animator leftPortraitAnimator;


        [Header("UI References - Portrait ฝั่งขวา")]
        [SerializeField] private GameObject rightPortraitRoot;
        [SerializeField] private CanvasGroup rightPortraitCanvasGroup;
        [SerializeField] private Image rightPortraitImage;
        [SerializeField] private Animator rightPortraitAnimator;


        [Header("Dim Setting (ฝั่งที่ไม่ได้พูด)")]
        [SerializeField] private Color speakingColor = Color.white;
        [SerializeField] private Color dimColor = new Color(0.55f, 0.55f, 0.55f, 1f);


        [Header("Text Speed")]
        [Tooltip("จำนวนตัวอักษรต่อวินาที (ความเร็วปกติ)")]
        [SerializeField] private float charsPerSecond = 30f;
        [Tooltip("จำนวนตัวอักษรต่อวินาที ตอนกด Ctrl ค้างเพื่อเร่ง")]
        [SerializeField] private float fastForwardCharsPerSecond = 120f;


        [Header("Type SFX")]
        [Tooltip("แนะนำให้ใช้ AudioSource แยกเฉพาะของ Dialogue")]
        [SerializeField] private AudioSource typeSfxSource;
        [SerializeField] private AudioClip defaultTypeSfx;
        [Tooltip("ห่างกันอย่างน้อยกี่วินาทีถึงจะเล่นเสียงตัวอักษรอีกครั้ง (กันเสียงถี่เกินไป)")]
        [SerializeField] private float typeSfxMinInterval = 0.03f;


        [Header("Portrait Fade")]
        [Tooltip("เวลา Fade ตอน Portrait โผล่มาครั้งแรกในแต่ละช่วงสนทนา")]
        [SerializeField] private float portraitFadeDuration = 0.25f;


        [Header("Input")]
        [Tooltip("ปุ่มคีย์บอร์ดที่ใช้ยืนยัน/ไปต่อ")]
        [SerializeField] private KeyCode[] confirmKeys = { KeyCode.Z, KeyCode.X };
        [Tooltip("ปุ่มเมาส์ที่ใช้ยืนยัน/ไปต่อ (0 = ปุ่มซ้าย, 1 = ปุ่มขวา)")]
        [SerializeField] private int confirmMouseButton = 0;
        [Tooltip("ปุ่มที่กดค้างเพื่อเร่งความเร็ว Text")]
        [SerializeField] private KeyCode fastForwardKey = KeyCode.LeftControl;


        // ------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------
        private State state = State.Idle;
        private int activeSequenceIndex = -1;
        private int activeLineIndex = -1;

        private string currentFullText = "";
        private int revealedCharCount = 0;
        private float charRevealTimer = 0f;
        private float lastTypeSfxTime = -999f;

        private float portraitFadeTimer = 0f;
        private CanvasGroup fadingPortraitGroup;

        public bool IsPlayingDialogue => state != State.Idle;


        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (dialoguePanelRoot != null)
                dialoguePanelRoot.SetActive(false);

            if (leftPortraitRoot != null)
                leftPortraitRoot.SetActive(false);

            if (rightPortraitRoot != null)
                rightPortraitRoot.SetActive(false);

            ResetSequenceStates();
        }


        private void Update()
        {
            if (state == State.Idle)
            {
                CheckTimeline();
                return;
            }

            switch (state)
            {
                case State.PortraitFadeIn:
                    UpdatePortraitFadeIn();
                    break;

                case State.Typing:
                    UpdateTyping();
                    break;

                case State.WaitingForInput:
                    UpdateWaitingForInput();
                    break;
            }
        }


        // ============================================================
        // TIMELINE CHECK
        // ============================================================

        private void CheckTimeline()
        {
            if (StageSpawnerV1.Instance == null)
                return;

            float stageTimer = StageSpawnerV1.Instance.StageTimer;

            for (int i = 0; i < sequences.Count; i++)
            {
                DialogueSequence seq = sequences[i];

                if (seq == null || seq.hasTriggered)
                    continue;

                if (stageTimer < seq.startTime)
                    continue;

                seq.hasTriggered = true;
                BeginSequence(i);

                // เล่นได้ทีละชุดต่อเฟรม (ชุดที่เหลือจะถูกเช็คในเฟรมถัดไปตอน Idle อีกครั้ง)
                break;
            }
        }


        private void ResetSequenceStates()
        {
            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i] != null)
                    sequences[i].hasTriggered = false;
            }
        }


        /// <summary>
        /// เรียกจากภายนอกได้ ถ้าต้องการ Reset Dialogue Timeline
        /// (เช่นตอน Restart Stage โดยไม่โหลด Scene ใหม่)
        /// </summary>
        public void ResetDialogueTimeline()
        {
            ResetSequenceStates();
        }


        // ============================================================
        // SEQUENCE FLOW
        // ============================================================

        private void BeginSequence(int sequenceIndex)
        {
            activeSequenceIndex = sequenceIndex;
            activeLineIndex = -1;

            // หยุด StageTimer ของ Stage (ศัตรูใหม่จะไม่ Spawn เพิ่ม)
            if (StageSpawnerV1.Instance != null)
                StageSpawnerV1.Instance.PauseTimer();

            // แจ้งระบบอื่น (Enemy/Bullet) ให้เคลียร์ตัวเองออกจากจอ
            OnDialogueSequenceStart?.Invoke();

            if (dialoguePanelRoot != null)
                dialoguePanelRoot.SetActive(true);

            if (dialoguePanelCanvasGroup != null)
                dialoguePanelCanvasGroup.alpha = 1f;

            BeginNextLine();
        }


        private void BeginNextLine()
        {
            DialogueSequence seq = sequences[activeSequenceIndex];
            activeLineIndex++;

            if (activeLineIndex >= seq.lines.Count)
            {
                EndSequence();
                return;
            }

            SceneDialogueLine line = seq.lines[activeLineIndex];

            // --- Name / Text setup ---
            if (nameText != null)
                nameText.text = line.speakerName;

            currentFullText = line.text ?? "";
            revealedCharCount = 0;
            charRevealTimer = 0f;

            if (bodyText != null)
                bodyText.text = "";

            // --- Portrait visibility ---
            bool leftIsNewlyShown = ApplyPortraitVisibility(DialogueSpeaker.Left, line.showLeftPortrait, line);
            bool rightIsNewlyShown = ApplyPortraitVisibility(DialogueSpeaker.Right, line.showRightPortrait, line);

            // --- Dim ฝั่งที่ไม่ได้พูด ---
            ApplySpeakingDim(line.speaker);

            // --- Fade-in ถ้ามีฝั่งไหน "เพิ่งโผล่มาใหม่" ในบรรทัดนี้ ---
            CanvasGroup groupToFade = null;

            if (leftIsNewlyShown)
                groupToFade = leftPortraitCanvasGroup;
            else if (rightIsNewlyShown)
                groupToFade = rightPortraitCanvasGroup;

            if (groupToFade != null && portraitFadeDuration > 0f)
            {
                fadingPortraitGroup = groupToFade;
                fadingPortraitGroup.alpha = 0f;
                portraitFadeTimer = 0f;
                state = State.PortraitFadeIn;
            }
            else
            {
                state = State.Typing;
            }
        }


        private void EndSequence()
        {
            state = State.Idle;
            activeSequenceIndex = -1;
            activeLineIndex = -1;

            if (dialoguePanelRoot != null)
                dialoguePanelRoot.SetActive(false);

            if (leftPortraitRoot != null)
                leftPortraitRoot.SetActive(false);

            if (rightPortraitRoot != null)
                rightPortraitRoot.SetActive(false);

            if (StageSpawnerV1.Instance != null)
                StageSpawnerV1.Instance.ResumeTimer();

            OnDialogueSequenceEnd?.Invoke();
        }


        // ============================================================
        // PORTRAIT HELPERS
        // ============================================================

        /// <summary>
        /// ตั้งค่าการแสดง Portrait ฝั่งที่ระบุ
        /// คืนค่า true ถ้าฝั่งนี้ "เพิ่งถูกเปิดใช้งานใหม่" ในบรรทัดนี้ (เอาไว้ตัดสินใจ Fade-in)
        /// </summary>
        private bool ApplyPortraitVisibility(DialogueSpeaker side, bool shouldShow, SceneDialogueLine line)
        {
            GameObject root = side == DialogueSpeaker.Left ? leftPortraitRoot : rightPortraitRoot;
            Image image = side == DialogueSpeaker.Left ? leftPortraitImage : rightPortraitImage;
            Animator animator = side == DialogueSpeaker.Left ? leftPortraitAnimator : rightPortraitAnimator;

            if (root == null)
                return false;

            bool wasActive = root.activeSelf;

            if (!shouldShow)
            {
                root.SetActive(false);
                return false;
            }

            root.SetActive(true);

            // อัพเดต Sprite/Animation เฉพาะฝั่งที่กำลังพูดในบรรทัดนี้
            // (ฝั่งที่ Show อยู่แต่ไม่ใช่ speaker จะคง Sprite/Animation เดิมไว้)
            if (side == line.speaker)
            {
                if (line.useAnimatorPortrait && animator != null &&
                    !string.IsNullOrEmpty(line.animatorTriggerName))
                {
                    animator.SetTrigger(line.animatorTriggerName);
                }
                else if (image != null && line.portraitSprite != null)
                {
                    image.sprite = line.portraitSprite;
                }
            }

            // "เพิ่งโผล่มาใหม่" = ก่อนหน้านี้ Inactive อยู่
            return !wasActive;
        }


        private void ApplySpeakingDim(DialogueSpeaker speaker)
        {
            if (leftPortraitImage != null)
                leftPortraitImage.color = (speaker == DialogueSpeaker.Left) ? speakingColor : dimColor;

            if (rightPortraitImage != null)
                rightPortraitImage.color = (speaker == DialogueSpeaker.Right) ? speakingColor : dimColor;
        }


        // ============================================================
        // STATE: PORTRAIT FADE IN
        // ============================================================

        private void UpdatePortraitFadeIn()
        {
            portraitFadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(portraitFadeTimer / portraitFadeDuration);

            if (fadingPortraitGroup != null)
                fadingPortraitGroup.alpha = t;

            if (t >= 1f)
            {
                fadingPortraitGroup = null;
                state = State.Typing;
            }
        }


        // ============================================================
        // STATE: TYPING
        // ============================================================

        private void UpdateTyping()
        {
            if (IsConfirmPressedThisFrame())
            {
                // กดระหว่างกำลังพิมพ์ -> โชว์ตัวอักษรให้ครบทันที
                RevealFullLine();
                state = State.WaitingForInput;
                return;
            }

            float speed = Input.GetKey(fastForwardKey) ? fastForwardCharsPerSecond : charsPerSecond;

            if (speed <= 0f)
                speed = 1f;

            float secondsPerChar = 1f / speed;
            charRevealTimer += Time.deltaTime;

            while (charRevealTimer >= secondsPerChar && revealedCharCount < currentFullText.Length)
            {
                charRevealTimer -= secondsPerChar;
                revealedCharCount++;
                PlayTypeSfx();
            }

            if (bodyText != null)
                bodyText.text = currentFullText.Substring(0, revealedCharCount);

            if (revealedCharCount >= currentFullText.Length)
            {
                state = State.WaitingForInput;
            }
        }


        private void RevealFullLine()
        {
            revealedCharCount = currentFullText.Length;

            if (bodyText != null)
                bodyText.text = currentFullText;
        }


        private void PlayTypeSfx()
        {
            if (typeSfxSource == null)
                return;

            if (Time.time - lastTypeSfxTime < typeSfxMinInterval)
                return;

            SceneDialogueLine line = sequences[activeSequenceIndex].lines[activeLineIndex];
            AudioClip clip = line.typeSfxOverride != null ? line.typeSfxOverride : defaultTypeSfx;

            if (clip == null)
                return;

            typeSfxSource.PlayOneShot(clip);
            lastTypeSfxTime = Time.time;
        }


        // ============================================================
        // STATE: WAITING FOR INPUT (บรรทัดโชว์ครบแล้ว รอกดไปต่อ)
        // ============================================================

        private void UpdateWaitingForInput()
        {
            if (IsConfirmPressedThisFrame())
            {
                BeginNextLine();
            }
        }


        // ============================================================
        // INPUT HELPER
        // ============================================================

        private bool IsConfirmPressedThisFrame()
        {
            if (Input.GetMouseButtonDown(confirmMouseButton))
                return true;

            for (int i = 0; i < confirmKeys.Length; i++)
            {
                if (Input.GetKeyDown(confirmKeys[i]))
                    return true;
            }

            return false;
        }
    }
}