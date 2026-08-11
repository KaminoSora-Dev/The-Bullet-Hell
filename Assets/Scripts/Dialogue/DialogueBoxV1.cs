using System;
using UnityEngine;
using TMPro;

namespace DialogueSystem
{
    /// <summary>
    /// กล่องข้อความของตัวละคร 1 ตัว - วางเป็น Child Object ของตัวละครนั้นๆ (Player/Enemy/Boss)
    /// เพื่อให้ตำแหน่งขยับตามตัวละครอัตโนมัติโดยไม่ต้องเขียนโค้ด follow เอง
    ///
    /// ลงทะเบียนตัวเองกับ DialogueManager ตอน OnEnable และถอนตัวตอน OnDisable
    /// (ใช้ OnEnable/OnDisable แทน Awake/OnDestroy เพราะ Enemy/Boss มาจาก Pool -
    /// SetActive(true/false) เป็นจังหวะที่ถูกต้องกว่า Awake ที่รันครั้งเดียวตอนสร้าง object จริงๆ เท่านั้น)
    ///
    /// ไม่ปิด/เปิด GameObject เพื่อซ่อนกล่อง (จะทำให้ Update ของสคริปต์นี้หยุดทำงานไปด้วย) -
    /// ใช้วิธีลดค่า Alpha ของ Text/Sprite ลงเหลือ 0 แทน กล่อง GameObject จึงยังทำงานอยู่ตลอดที่ตัวละครแอคทีฟ
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueBoxV1 : MonoBehaviour
    {
        private enum BoxState
        {
            Idle,
            Typing,
            Holding,
            Ending
        }

        [Header("Identity")]
        [Tooltip("ต้องตรงกับ Speaker Id ที่ใช้ใน DialogueLine (เช่น \"Player\", \"BossA\", \"EnemyX\")")]
        [SerializeField] private string speakerId = "Player";

        [Header("References")]
        [Tooltip("ใช้ TextMeshPro (world space) หรือ TextMeshProUGUI ก็ได้ - ใช้แสดงข้อความหลัก")]
        [SerializeField] private TMP_Text dialogueText;

        [Tooltip("Sprite ของกล่องข้อความ - ไม่ใส่ก็ได้ถ้าอยากให้มีแค่ตัวหนังสือลอยๆ")]
        [SerializeField] private SpriteRenderer boxSprite;

        [Header("Typing")]
        [Tooltip("จำนวนตัวอักษรที่พิมพ์ต่อวินาที")]
        [SerializeField, Min(1f)] private float charsPerSecond = 30f;

        [Header("Typing SFX (Optional)")]
        [Tooltip("ปล่อยว่างไว้ถ้าไม่ต้องการเสียงตอนพิมพ์")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip typeSfxClip;
        [Tooltip("เล่นเสียงทุกกี่ตัวอักษร - 1 = เล่นทุกตัว, 2 = เล่นสลับตัวเว้นตัว (กันเสียงถี่/หนวกหูตอนพิมพ์เร็ว)")]
        [SerializeField, Min(1)] private int sfxEveryNChars = 1;
        [Tooltip("สุ่ม pitch เล็กน้อยกันเสียงซ้ำจำเจ")]
        [SerializeField] private Vector2 sfxPitchRange = new Vector2(0.95f, 1.05f);

        [Header("Fade")]
        [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;
        [Tooltip("ใช้กับ Text End Style = TypewriterErase เท่านั้น - จำนวนตัวอักษรที่ลบต่อวินาที")]
        [SerializeField, Min(1f)] private float eraseCharsPerSecond = 40f;

        public string SpeakerId => speakerId;
        public bool IsBusy => state != BoxState.Idle;

        private BoxState state = BoxState.Idle;

        private string fullText = "";
        private int visibleCharCount;
        private float typeTimer;

        private float holdTimer;
        private float targetLiveTime;

        private float fadeTimer;
        private TextEndStyle activeEndStyle;

        private bool eraseTextFullyRemoved;
        private float eraseBoxFadeTimer;

        private Action onFinishedCallback;

        private Color textOriginalColor = Color.white;
        private Color boxOriginalColor = Color.white;

        private void Awake()
        {
            if (dialogueText != null)
                textOriginalColor = dialogueText.color;

            if (boxSprite != null)
                boxOriginalColor = boxSprite.color;
        }

        private void OnEnable()
        {
            state = BoxState.Idle;
            SetAlpha(0f);

            if (dialogueText != null)
                dialogueText.text = "";

            TryRegister();
        }

        private void Start()
        {
            // ป้องกันกรณี DialogueManager ยังไม่พร้อมตอน OnEnable()
            TryRegister();
        }

        private void TryRegister()
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.RegisterBox(this);
        }

        private void OnDisable()
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.UnregisterBox(this);
        }

        /// <summary>
        /// เริ่มเล่นบทพูด 1 บรรทัด - เรียกจาก DialogueManager เท่านั้น
        /// onFinished จะถูกเรียกครั้งเดียวตอนกล่องนี้เล่นจบสมบูรณ์ (พิมพ์ + Live Time + Fade)
        /// </summary>
        public void PlayLine(string text, float liveTime, TextEndStyle endStyle, Action onFinished)
        {
            fullText = text ?? "";
            visibleCharCount = 0;
            typeTimer = 0f;

            targetLiveTime = Mathf.Max(0f, liveTime);
            activeEndStyle = endStyle;
            onFinishedCallback = onFinished;

            if (dialogueText != null)
                dialogueText.text = "";

            SetAlpha(1f);

            state = fullText.Length == 0 ? BoxState.Holding : BoxState.Typing;
            holdTimer = 0f;
        }

        private void Update()
        {
            switch (state)
            {
                case BoxState.Typing:
                    UpdateTyping();
                    break;
                case BoxState.Holding:
                    UpdateHolding();
                    break;
                case BoxState.Ending:
                    UpdateEnding();
                    break;
            }
        }

        private void UpdateTyping()
        {
            typeTimer += Time.deltaTime;

            int targetCharCount = Mathf.FloorToInt(typeTimer * charsPerSecond);
            targetCharCount = Mathf.Min(targetCharCount, fullText.Length);

            while (visibleCharCount < targetCharCount)
            {
                visibleCharCount++;
                PlayTypeSfxIfNeeded();
            }

            if (dialogueText != null)
                dialogueText.text = fullText.Substring(0, visibleCharCount);

            if (visibleCharCount >= fullText.Length)
            {
                state = BoxState.Holding;
                holdTimer = 0f;
            }
        }

        private void PlayTypeSfxIfNeeded()
        {
            if (audioSource == null || typeSfxClip == null) return;
            if (visibleCharCount % Mathf.Max(1, sfxEveryNChars) != 0) return;

            // ข้ามช่องว่าง/บรรทัดใหม่ - ไม่ต้องมีเสียง ฟังดูเป็นธรรมชาติกว่า
            char c = fullText[visibleCharCount - 1];
            if (char.IsWhiteSpace(c)) return;

            audioSource.pitch = UnityEngine.Random.Range(sfxPitchRange.x, sfxPitchRange.y);
            audioSource.PlayOneShot(typeSfxClip);
        }

        private void UpdateHolding()
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= targetLiveTime)
                EnterEndingState();
        }

        private void EnterEndingState()
        {
            state = BoxState.Ending;
            fadeTimer = 0f;
            eraseTextFullyRemoved = false;
            eraseBoxFadeTimer = 0f;
        }

        private void UpdateEnding()
        {
            switch (activeEndStyle)
            {
                case TextEndStyle.Instant:
                    FinishLine();
                    return;

                case TextEndStyle.FadeOut:
                    UpdateFadeOutEnding();
                    return;

                case TextEndStyle.TypewriterErase:
                    UpdateEraseEnding();
                    return;
            }
        }

        private void UpdateFadeOutEnding()
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeOutDuration);
            SetAlpha(1f - t);

            if (t >= 1f)
                FinishLine();
        }

        private void UpdateEraseEnding()
        {
            if (!eraseTextFullyRemoved)
            {
                fadeTimer += Time.deltaTime;

                int eraseCount = Mathf.FloorToInt(fadeTimer * eraseCharsPerSecond);
                int remaining = Mathf.Max(0, fullText.Length - eraseCount);

                if (dialogueText != null)
                    dialogueText.text = fullText.Substring(0, remaining);

                if (remaining <= 0)
                    eraseTextFullyRemoved = true;

                return;
            }

            // ลบตัวอักษรหมดแล้ว - เฟดกล่องที่เหลือ (ถ้ามี) หายไปทีหลัง
            eraseBoxFadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(eraseBoxFadeTimer / fadeOutDuration);
            SetAlpha(1f - t);

            if (t >= 1f)
                FinishLine();
        }

        private void FinishLine()
        {
            state = BoxState.Idle;
            SetAlpha(0f);

            if (dialogueText != null)
                dialogueText.text = "";

            Action callback = onFinishedCallback;
            onFinishedCallback = null;
            callback?.Invoke();
        }

        private void SetAlpha(float alpha)
        {
            if (dialogueText != null)
            {
                Color c = textOriginalColor;
                c.a = textOriginalColor.a * alpha;
                dialogueText.color = c;
            }

            if (boxSprite != null)
            {
                Color c = boxOriginalColor;
                c.a = boxOriginalColor.a * alpha;
                boxSprite.color = c;
            }
        }
    }
}
