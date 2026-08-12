using UnityEngine;
using TMPro;
using BossSystem;

namespace UISystem
{
    /// <summary>
    /// เอฟเฟกต์แบนเนอร์ Signature Spell แบบ Touhou:
    /// เริ่มที่จุดล่างจอ (Opacity 0) -> เลื่อนขึ้นไปจุดกลางจอ (Opacity 100, Ease In-Out) -> ค้างไว้ -> เลื่อนต่อขึ้นบนจนหาย (Opacity 0, Ease In-Out)
    ///
    /// เรียกเองได้ตรงๆ ผ่าน Play("ชื่อ Spell") หรือจะให้ผูกอัตโนมัติกับ Boss Spell Card ทุกตัว
    /// ผ่าน static event ของ BossV2 ก็ได้ (ติ๊ก autoPlayOnBossSpellCard ไว้)
    /// </summary>
    public class SignatureSpellEffectUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform textRect;
        [SerializeField] private TMP_Text spellNameText;
        [SerializeField] private SpriteRenderer backgroundSpriteRenderer;

        [Header("Visual (Optional)")]
        [Tooltip(
            "ใส่ได้ไม่บังคับ - เป็น RectTransform ของ UI Image/GameObject ที่อยาก Spawn " +
            "เคลื่อนที่และ Fade ไปพร้อมกับ Text (ใช้ตำแหน่ง Start/Center/End เดียวกันด้านล่าง)\n" +
            "แนะนำให้เป็น UI Image (ไม่ใช่ SpriteRenderer) เพราะ CanvasGroup.alpha จะ Fade ให้อัตโนมัติ " +
            "ถ้าเป็น SpriteRenderer ต้องทำ Fade เอง อยู่นอกเหนือ Component นี้"
        )]
        [SerializeField] private RectTransform visualRect;

        [Tooltip(
            "ใส่ได้ไม่บังคับ - Animator ของ Object ในช่อง Visual Rect ด้านบน " +
            "ถ้ามีและมี Trigger ชื่อตรงกับ Visual Play Trigger จะถูกเรียกตอน Play() " +
            "ถ้าไม่ใส่ หรือไม่มี Trigger ชื่อนี้ จะข้ามไปเฉยๆ ไม่มี Error"
        )]
        [SerializeField] private Animator visualAnimator;

        [SerializeField] private string visualPlayTriggerName = "Play";

        [Header("Positions (Anchored Position ของ textRect)")]
        [SerializeField] private Vector2 startPosition = new Vector2(0f, -600f);
        [SerializeField] private Vector2 centerPosition = Vector2.zero;
        [SerializeField] private Vector2 endPosition = new Vector2(0f, 600f);

        [Header("Timing")]
        [Min(0f)] [SerializeField] private float fadeInDuration = 0.6f;
        [Min(0f)] [SerializeField] private float holdDuration = 1.2f;
        [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

        [Header("Auto Play")]
        [Tooltip("ติ๊กไว้ - Effect นี้จะเล่นอัตโนมัติทุกครั้งที่ Boss ตัวไหนก็ตามเข้า Phase ที่เป็น Spell Card " +
                 "(ฟัง static event ของ BossV2 - ไม่ต้องผูก Boss ตัวใดตัวหนึ่งเอง)")]
        [SerializeField] private bool autoPlayOnBossSpellCard = true;

        private enum Phase { Idle, FadeIn, Hold, FadeOut }

        private Phase currentPhase = Phase.Idle;
        private float phaseTimer;

        private void Awake()
        {
            SetVisualState(startPosition, 0f);
            gameObject.SetActive(true); // ตัว GameObject เปิดค้างไว้เสมอ ใช้ CanvasGroup.alpha คุมการมองเห็นแทน
        }

        /// <summary>เรียก Animator Trigger ของ Visual ถ้ามีทั้ง Animator และ Trigger ชื่อตรงกัน - ไม่มีก็ข้ามเงียบๆ (opt-in)</summary>
        private void TriggerVisualPlayAnimation()
        {
            if (visualAnimator == null)
                return;

            if (!HasTriggerParameter(visualAnimator, visualPlayTriggerName))
                return;

            visualAnimator.SetTrigger(visualPlayTriggerName);
        }

        private static bool HasTriggerParameter(Animator animator, string paramName)
        {
            if (string.IsNullOrEmpty(paramName))
                return false;

            AnimatorControllerParameter[] parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                    parameters[i].name == paramName)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEnable()
        {
            if (autoPlayOnBossSpellCard)
                BossV2.OnAnySpellCardStarted += HandleBossSpellCardStarted;
        }

        private void OnDisable()
        {
            BossV2.OnAnySpellCardStarted -= HandleBossSpellCardStarted;
        }

        private void HandleBossSpellCardStarted(string spellCardName)
        {
            // Spell Card บางเฟสอาจไม่ได้ตั้งชื่อไว้ (spellCardName ว่าง) - เล่นเฉพาะตอนมีชื่อจริงๆ เท่านั้น
            if (string.IsNullOrWhiteSpace(spellCardName)) return;
            Play(spellCardName);
        }

        /// <summary>เริ่มเล่นแบนเนอร์ด้วยข้อความที่กำหนด - เรียกซ้ำระหว่างกำลังเล่นอยู่จะรีสตาร์ทตั้งแต่ต้น</summary>
        public void Play(string text)
        {
            if (spellNameText != null)
                spellNameText.text = text;

            currentPhase = Phase.FadeIn;
            phaseTimer = 0f;
            SetVisualState(startPosition, 0f);

            TriggerVisualPlayAnimation();
        }

        private void Update()
        {
            if (currentPhase == Phase.Idle) return;

            phaseTimer += Time.deltaTime;

            switch (currentPhase)
            {
                case Phase.FadeIn:
                    UpdateFadeIn();
                    break;

                case Phase.Hold:
                    UpdateHold();
                    break;

                case Phase.FadeOut:
                    UpdateFadeOut();
                    break;
            }
        }

        private void UpdateFadeIn()
        {
            float t = Mathf.Clamp01(phaseTimer / Mathf.Max(fadeInDuration, 0.0001f));
            float eased = ApplyEaseInOut(t);

            Vector2 pos = Vector2.LerpUnclamped(startPosition, centerPosition, eased);
            SetVisualState(pos, eased);

            if (t >= 1f)
            {
                currentPhase = Phase.Hold;
                phaseTimer = 0f;
                SetVisualState(centerPosition, 1f);
            }
        }

        private void UpdateHold()
        {
            if (phaseTimer >= holdDuration)
            {
                currentPhase = Phase.FadeOut;
                phaseTimer = 0f;
            }
        }

        private void UpdateFadeOut()
        {
            float t = Mathf.Clamp01(phaseTimer / Mathf.Max(fadeOutDuration, 0.0001f));
            float eased = ApplyEaseInOut(t);

            Vector2 pos = Vector2.LerpUnclamped(centerPosition, endPosition, eased);
            SetVisualState(pos, 1f - eased);

            if (t >= 1f)
            {
                currentPhase = Phase.Idle;
                SetVisualState(startPosition, 0f); // เตรียมพร้อมสำหรับรอบถัดไป
            }
        }

        private void SetVisualState(Vector2 anchoredPosition, float alpha)
        {
            if (textRect != null)
                textRect.anchoredPosition = anchoredPosition;

            // Visual (ถ้ามี) เคลื่อนที่ไปพร้อม Text บนเส้นทางเดียวกัน
            // ไม่ต้อง Fade เองแยก เพราะอยู่ใต้ canvasGroup เดียวกัน (ถ้าเป็น UI Image)
            if (visualRect != null)
                visualRect.anchoredPosition = anchoredPosition;

            if (canvasGroup != null)
                canvasGroup.alpha = alpha;
        }

        /// <summary>สูตร Ease In-Out เดียวกับที่ใช้ใน EnemyV1/BossV2 (ช้า-เร็ว-ช้า) - ก็อปมาเองเพื่อไม่ผูก UI เข้ากับ EnemySystem</summary>
        private static float ApplyEaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }
    }
}