using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using BossSystem;

namespace UISystem
{
    /// <summary>
    /// Signature Spell Banner:
    /// - Spell BG เปิดเฉพาะช่วงที่ Boss อยู่ใน Spell Card
    /// - Text และ Diamond เคลื่อนแยกกันได้
    /// - เลือก Linear / EaseIn / EaseOut / EaseInOut แยก Enter/Exit
    /// - Fade ไปพร้อม movement
    /// - รองรับ Child ที่ SetActive(false) ไว้ใน Inspector
    /// - ส่ง Spell SFX ผ่าน BossV2 event เพื่อให้ AudioSystem รับต่อได้
    /// </summary>
    public class SignatureSpellEffectUI : MonoBehaviour
    {
        public enum MotionEase
        {
            Linear,
            EaseIn,
            EaseOut,
            EaseInOut
        }

        private enum EffectPhase
        {
            Idle,
            Enter,
            Hold,
            Exit
        }

        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform textRect;
        [SerializeField] private TMP_Text spellNameText;

        [Tooltip("SpriteRenderer ของ Diamond Banner")]
        [SerializeField] private SpriteRenderer backgroundSpriteRenderer;

        [Header("Spell Background")]
        [Tooltip(
            "BG พิเศษของ Spell Card เท่านั้น\n" +
            "แนะนำให้ SetActive(false) ไว้ใน Inspector"
        )]
        [SerializeField] private GameObject spellBackgroundObject;

        [Tooltip("Optional - CanvasGroup ของ Spell BG สำหรับ Fade")]
        [SerializeField] private CanvasGroup spellBackgroundCanvasGroup;

        [SerializeField] private bool controlSpellBackgroundActiveState = true;

        [Header("Text Motion")]
        [SerializeField] private Vector2 textStartPosition = new Vector2(-700f, 0f);
        [SerializeField] private Vector2 textCenterPosition = Vector2.zero;
        [SerializeField] private Vector2 textEndPosition = new Vector2(700f, 0f);

        [Header("Diamond Motion")]
        [Tooltip("RectTransform ที่ครอบ SpriteRenderer ของ Diamond")]
        [SerializeField] private RectTransform diamondRect;

        [SerializeField] private Vector2 diamondStartPosition = new Vector2(700f, 0f);
        [SerializeField] private Vector2 diamondCenterPosition = Vector2.zero;
        [SerializeField] private Vector2 diamondEndPosition = new Vector2(-700f, 0f);

        [Header("Timing")]
        [Min(0f)]
        [SerializeField] private float enterDuration = 0.6f;

        [Min(0f)]
        [SerializeField] private float holdDuration = 1.2f;

        [Min(0f)]
        [SerializeField] private float exitDuration = 0.6f;

        [Header("Movement Ease")]
        [SerializeField] private MotionEase textEnterEase = MotionEase.EaseInOut;
        [SerializeField] private MotionEase diamondEnterEase = MotionEase.EaseInOut;
        [SerializeField] private MotionEase textExitEase = MotionEase.EaseInOut;
        [SerializeField] private MotionEase diamondExitEase = MotionEase.EaseInOut;

        [Header("Fade")]
        [Range(0f, 1f)]
        [SerializeField] private float bannerMaxAlpha = 1f;

        [Tooltip("ปกติ Fade ใช้ progress เดียวกับ Text Movement")]
        [SerializeField] private bool useSeparateFadeEase = false;

        [SerializeField] private MotionEase fadeEnterEase = MotionEase.EaseInOut;
        [SerializeField] private MotionEase fadeExitEase = MotionEase.EaseInOut;

        [Header("Audio")]
        [Tooltip(
            "เสียง Signature Spell Card เดียวของเกม\n" +
            "จะส่งผ่าน BossV2.OnAnySpellCardSfxRequested ให้ AudioSystem เล่น"
        )]
        [SerializeField] private AudioClip spellCardSfx;

        [SerializeField] private bool requestSpellCardSfx = true;

        [Header("Auto Play")]
        [SerializeField] private bool autoPlayOnBossSpellCard = true;

        private EffectPhase currentPhase = EffectPhase.Idle;
        private float phaseTimer;

        private void Awake()
        {
            SetBannerVisual(
                textStartPosition,
                diamondStartPosition,
                0f
            );

            if (spellBackgroundCanvasGroup != null)
                spellBackgroundCanvasGroup.alpha = 0f;

            // Root ต้องเปิดอยู่เพื่อรับ Boss static event
            gameObject.SetActive(true);

            // Child visual สามารถ inactive จาก Inspector ได้
            SetObjectActive(
                textRect != null ? textRect.gameObject : null,
                false
            );

            SetObjectActive(
                diamondRect != null ? diamondRect.gameObject : null,
                false
            );

            HideSpellBackground();
        }

        private void OnEnable()
        {
            if (!autoPlayOnBossSpellCard)
                return;

            BossV2.OnAnySpellCardPhaseStarted += HandleSpellCardStarted;
            BossV2.OnAnySpellCardPhaseEnded += HandleSpellCardEnded;
        }

        private void OnDisable()
        {
            BossV2.OnAnySpellCardPhaseStarted -= HandleSpellCardStarted;
            BossV2.OnAnySpellCardPhaseEnded -= HandleSpellCardEnded;
        }

        private void HandleSpellCardStarted(string spellCardName)
        {
            // BG ต้องเปิดทุก Spell Card
            ShowSpellBackground();

            // Banner จะเล่น Text/Movement เฉพาะเมื่อมีชื่อ
            if (!string.IsNullOrWhiteSpace(spellCardName))
                Play(spellCardName);

            if (requestSpellCardSfx && spellCardSfx != null)
                BossV2.RequestSpellCardSfx(spellCardSfx);
        }

        private void HandleSpellCardEnded(bool captured)
        {
            // StopImmediate() ครอบคลุม HideSpellBackground() อยู่แล้ว
            // และตัดจบ Banner (Enter/Hold/Exit) ทันที ป้องกัน Text/Diamond
            // เล่นค้างต่อลอยๆ หลัง BG หายไปแล้ว เช่นตอนบอสตายกลาง Spell Card
            StopImmediate();
        }

        /// <summary>
        /// เล่น Banner โดยตรงจากภายนอก
        /// </summary>
        public void Play(string text)
        {
            if (spellNameText != null)
                spellNameText.text = text;

            ActivateBannerObjects();

            currentPhase = EffectPhase.Enter;
            phaseTimer = 0f;

            SetBannerVisual(
                textStartPosition,
                diamondStartPosition,
                0f
            );
        }

        private void Update()
        {
            switch (currentPhase)
            {
                case EffectPhase.Enter:
                    UpdateEnter();
                    break;

                case EffectPhase.Hold:
                    UpdateHold();
                    break;

                case EffectPhase.Exit:
                    UpdateExit();
                    break;
            }
        }

        private void UpdateEnter()
        {
            phaseTimer += Time.deltaTime;

            float t = NormalizeTime(
                phaseTimer,
                enterDuration
            );

            float textT = EvaluateEase(t, textEnterEase);
            float diamondT = EvaluateEase(t, diamondEnterEase);

            float fadeT = useSeparateFadeEase
                ? EvaluateEase(t, fadeEnterEase)
                : textT;

            SetBannerVisual(
                Vector2.LerpUnclamped(
                    textStartPosition,
                    textCenterPosition,
                    textT
                ),
                Vector2.LerpUnclamped(
                    diamondStartPosition,
                    diamondCenterPosition,
                    diamondT
                ),
                fadeT * bannerMaxAlpha
            );

            if (t >= 1f)
            {
                currentPhase = EffectPhase.Hold;
                phaseTimer = 0f;

                SetBannerVisual(
                    textCenterPosition,
                    diamondCenterPosition,
                    bannerMaxAlpha
                );
            }
        }

        private void UpdateHold()
        {
            phaseTimer += Time.deltaTime;

            if (phaseTimer >= holdDuration)
            {
                currentPhase = EffectPhase.Exit;
                phaseTimer = 0f;
            }
        }

        private void UpdateExit()
        {
            phaseTimer += Time.deltaTime;

            float t = NormalizeTime(
                phaseTimer,
                exitDuration
            );

            float textT = EvaluateEase(t, textExitEase);
            float diamondT = EvaluateEase(t, diamondExitEase);

            float fadeT = useSeparateFadeEase
                ? EvaluateEase(t, fadeExitEase)
                : textT;

            SetBannerVisual(
                Vector2.LerpUnclamped(
                    textCenterPosition,
                    textEndPosition,
                    textT
                ),
                Vector2.LerpUnclamped(
                    diamondCenterPosition,
                    diamondEndPosition,
                    diamondT
                ),
                (1f - fadeT) * bannerMaxAlpha
            );

            if (t >= 1f)
            {
                currentPhase = EffectPhase.Idle;
                phaseTimer = 0f;

                SetBannerVisual(
                    textStartPosition,
                    diamondStartPosition,
                    0f
                );

                DeactivateBannerObjects();
            }
        }

        private void SetBannerVisual(
            Vector2 textPosition,
            Vector2 diamondPosition,
            float alpha)
        {
            if (textRect != null)
                textRect.anchoredPosition = textPosition;

            if (diamondRect != null)
                diamondRect.anchoredPosition = diamondPosition;

            alpha = Mathf.Clamp01(alpha);

            if (canvasGroup != null)
                canvasGroup.alpha = alpha;

            // CanvasGroup ไม่คุม SpriteRenderer
            // จึงต้องปรับ alpha ของ Diamond เอง
            if (backgroundSpriteRenderer != null)
            {
                Color color =
                    backgroundSpriteRenderer.color;

                color.a = alpha;
                backgroundSpriteRenderer.color = color;
            }
        }

        private void ActivateBannerObjects()
        {
            SetObjectActive(
                textRect != null ? textRect.gameObject : null,
                true
            );

            SetObjectActive(
                diamondRect != null ? diamondRect.gameObject : null,
                true
            );
        }

        private void DeactivateBannerObjects()
        {
            SetObjectActive(
                textRect != null ? textRect.gameObject : null,
                false
            );

            SetObjectActive(
                diamondRect != null ? diamondRect.gameObject : null,
                false
            );
        }

        private void ShowSpellBackground()
        {
            if (spellBackgroundObject != null &&
                controlSpellBackgroundActiveState)
            {
                spellBackgroundObject.SetActive(true);
            }

            if (spellBackgroundCanvasGroup != null)
                spellBackgroundCanvasGroup.alpha = 1f;
        }

        private void HideSpellBackground()
        {
            if (spellBackgroundCanvasGroup != null)
                spellBackgroundCanvasGroup.alpha = 0f;

            if (spellBackgroundObject != null &&
                controlSpellBackgroundActiveState)
            {
                spellBackgroundObject.SetActive(false);
            }
        }

        private static void SetObjectActive(
            GameObject target,
            bool value)
        {
            if (target == null)
                return;

            if (target.activeSelf != value)
                target.SetActive(value);
        }

        private static float NormalizeTime(
            float timer,
            float duration)
        {
            if (duration <= 0f)
                return 1f;

            return Mathf.Clamp01(
                timer / duration
            );
        }

        private static float EvaluateEase(
            float t,
            MotionEase ease)
        {
            t = Mathf.Clamp01(t);

            switch (ease)
            {
                case MotionEase.EaseIn:
                    return t * t;

                case MotionEase.EaseOut:
                    return 1f -
                           (1f - t) *
                           (1f - t);

                case MotionEase.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f -
                          Mathf.Pow(
                              -2f * t + 2f,
                              2f
                          ) / 2f;

                default:
                    return t;
            }
        }

        /// <summary>
        /// หยุด Effect ทันที เช่นตอน Scene กำลังเปลี่ยน
        /// </summary>
        public void StopImmediate()
        {
            currentPhase = EffectPhase.Idle;
            phaseTimer = 0f;

            SetBannerVisual(
                textStartPosition,
                diamondStartPosition,
                0f
            );

            DeactivateBannerObjects();
            HideSpellBackground();
        }

        public AudioClip SpellCardSfx => spellCardSfx;
    }
}