using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;

namespace EnemySystem
{
    /// <summary>
    /// รูปแบบการไล่ค่า (ease) ระหว่าง 2 จุด - เพิ่มแบบใหม่ได้ในอนาคตโดยเพิ่ม case ใน ApplyEase()
    /// </summary>
    public enum EaseType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// 1 ช่วงของการเคลื่อนที่ - จากตำแหน่งปัจจุบัน ไปยัง target ภายในเวลา duration ด้วย easeType ที่กำหนด
    /// ทำเป็น [System.Serializable] เพื่อให้ Unity แสดงเป็นลิสต์แก้ไขได้ตรงๆ ใน Inspector
    /// </summary>
    [System.Serializable]
    public class MovementStep
    {
        [Tooltip("ลาก Transform ของจุดหมาย (Empty GameObject ที่วางไว้ใน Scene)")]
        public Transform target;
        [Tooltip("เวลาที่ใช้เดินทางไปถึงจุดนี้ (วินาที)")]
        public float duration = 1f;
        public EaseType easeType = EaseType.Linear;
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyV1 : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 10;
        private int currentHealth;

        [Header("Death Visual (เหมือนของ Player แต่ไม่ Respawn)")]
        [SerializeField] private int blinkCount = 3;
        [SerializeField] private float blinkInterval = 0.1f;
        [SerializeField] private Color blinkColor = Color.red;

        [Header("Appear (ตัวเลือก: เริ่มซ่อนตัวก่อนค่อยปรากฏ)")]
        [Tooltip("ถ้าติ๊กไว้ ตัว Enemy จะมองไม่เห็นและโดนยิงไม่ได้ตอนเริ่ม จนกว่าจะครบเวลา Appear Delay")]
        [SerializeField] private bool startHidden = false;
        [SerializeField] private float appearDelay = 0f;

        [Header("Movement Path")]
        [Tooltip("ลิสต์จุดที่จะเคลื่อนที่ไปตามลำดับ โดยเริ่มนับจากตำแหน่งปัจจุบันของ Enemy ตอน Start เป็นจุดตั้งต้น " +
                 "เช่น ใส่ 2 step: step แรก = จุด 2 (Ease In, 2 วิ), step สอง = จุด 3 (Linear, 1 วิ)")]
        [SerializeField] private List<MovementStep> movementPath = new List<MovementStep>();

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Collider ของตัว Enemy ต้องติ๊ก isTrigger = true เพื่อให้โดน PlayerBullet ได้")]
        [SerializeField] private Collider2D hitCollider;

        private Color originalColor;
        private bool isDead;

        // Rigidbody2D จำเป็นแค่เพื่อให้เกิด OnTriggerEnter2D ตอนชน Player เท่านั้น
        // (Player เอา Rigidbody2D ออกไปแล้ว - กฎ 2D Trigger ต้องมีฝั่งใดฝั่งหนึ่งมี Rigidbody2D)
        private Rigidbody2D rb;

        // death blink state
        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        // appear state
        private float appearTimer;
        private bool hasAppeared;

        // movement state
        private int currentStepIndex = -1; // -1 = ไม่มี step ให้เดิน หรือเดินจบแล้ว
        private float stepTimer;
        private Vector3 segmentStartPosition;

        void Awake()
        {
            currentHealth = maxHealth;

            rb = GetComponent<Rigidbody2D>();
            // Kinematic เหตุผลเดียวกับ EnemyBullet/PlayerBullet: ไม่ต้องการแรงจากภายนอกใดๆ
            // Enemy เคลื่อนที่เองผ่าน transform.position ใน UpdateMovement() อยู่แล้ว
            // ที่ต้องมี rb ตัวนี้คือเพื่อให้ trigger event เกิดขึ้นได้ตอนชน Player เท่านั้น
            rb.bodyType = RigidbodyType2D.Kinematic;

            // กันลืมติ๊ก isTrigger ใน Inspector - ถ้าไม่ใช่ trigger กระสุน/Player จะโดนดันแทนที่จะทะลุผ่าน
            if (hitCollider != null) hitCollider.isTrigger = true;

            if (bodySprite != null)
                originalColor = bodySprite.color;

            // ถ้าตั้งให้เริ่มซ่อนตัว ปิดทั้งภาพและ collider ตั้งแต่ Awake
            // สำคัญ: ต้องปิด collider ด้วย ไม่ใช่แค่ sprite เพราะไม่งั้นกระสุนผู้เล่นจะยิงโดนตัวที่ "มองไม่เห็น" ได้ ซึ่งไม่ตรงกับที่ต้องการ
            if (startHidden)
            {
                SetVisibleAndHittable(false);
                hasAppeared = false;
            }
            else
            {
                hasAppeared = true;
            }
        }

        void Start()
        {
            // จุดเริ่มต้น "จุดที่ 1" ของ path คือตำแหน่งที่ Enemy ถูกวางไว้ใน Scene ตอนเริ่มเกมนี่เอง
            // ไม่ต้องระบุใน movementPath list ซ้ำ - list มีแค่จุดที่ 2 เป็นต้นไป
            segmentStartPosition = transform.position;

            if (movementPath.Count > 0)
            {
                currentStepIndex = 0;
                stepTimer = 0f;
            }
        }

        void Update()
        {
            if (isDead)
            {
                UpdateDeathBlink();
                return; // ตายแล้วไม่ต้องอัปเดตเรื่อง appear/movement อีกต่อไป
            }

            if (!hasAppeared)
            {
                UpdateAppear();
            }

            UpdateMovement();
        }

        private void UpdateAppear()
        {
            appearTimer += Time.deltaTime;
            if (appearTimer >= appearDelay)
            {
                SetVisibleAndHittable(true);
                hasAppeared = true;
            }
        }

        private void SetVisibleAndHittable(bool value)
        {
            if (bodySprite != null) bodySprite.enabled = value;
            if (hitCollider != null) hitCollider.enabled = value;
        }

        private void UpdateMovement()
        {
            // currentStepIndex == -1 หมายถึงไม่มี step เหลือให้เดิน (จบ path แล้ว หรือไม่ได้กำหนด path เลย)
            if (currentStepIndex < 0 || currentStepIndex >= movementPath.Count) return;

            MovementStep step = movementPath[currentStepIndex];

            // กัน error ถ้าลืมลาก target ใส่ step ไหนสัก step - ข้ามไป step ถัดไปแทนที่จะค้าง
            if (step.target == null)
            {
                AdvanceToNextStep();
                return;
            }

            stepTimer += Time.deltaTime;
            // Mathf.Max กัน duration = 0 ที่จะทำให้หารด้วยศูนย์
            float t = Mathf.Clamp01(stepTimer / Mathf.Max(step.duration, 0.0001f));
            float easedT = ApplyEase(t, step.easeType);

            // LerpUnclamped เผื่อ ease function บางแบบให้ค่าเกิน 0-1 ชั่วคราว (เช่น overshoot/bounce ในอนาคต)
            transform.position = Vector3.LerpUnclamped(segmentStartPosition, step.target.position, easedT);

            if (t >= 1f)
            {
                AdvanceToNextStep();
            }
        }

        private void AdvanceToNextStep()
        {
            // จุดเริ่มของ segment ถัดไป คือตำแหน่งปัจจุบัน (ซึ่งควรจะตรงกับ target ของ step ที่เพิ่งจบพอดี)
            segmentStartPosition = transform.position;
            currentStepIndex++;
            stepTimer = 0f;

            if (currentStepIndex >= movementPath.Count)
            {
                currentStepIndex = -1; // จบ path ทั้งหมดแล้ว หยุดอยู่กับที่ตำแหน่งสุดท้าย
            }
        }

        private float ApplyEase(float t, EaseType easeType)
        {
            switch (easeType)
            {
                case EaseType.EaseIn:
                    // เริ่มช้า จบเร็ว (t^2)
                    return t * t;

                case EaseType.EaseOut:
                    // เริ่มเร็ว จบช้า
                    return 1f - (1f - t) * (1f - t);

                case EaseType.EaseInOut:
                    // ช้า-เร็ว-ช้า
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default: // Linear
                    return t;
            }
        }

        /// <summary>
        /// ชนตัว Enemy โดยตรง (ไม่ใช่กระสุน) ก็ต้องทำให้ Player ตายด้วยเช่นกัน
        /// ใช้ tag check ก่อนเหมือน EnemyBullet เพื่อไม่ต้องเสีย cost GetComponent กับทุกอย่างที่มาชน
        /// </summary>
        void OnTriggerEnter2D(Collider2D other)
        {
            // Enemy ตายไปแล้วแต่ collider ยังไม่ทัน disable ในเฟรมเดียวกัน - กันไว้ไม่ให้ยังฆ่า Player ซ้ำ
            if (isDead) return;

            if (other.CompareTag("EnemyBorder"))
            {
                // ใช้ tag แยกจาก HiddenBorder ของกระสุนโดยเฉพาะ เพราะ Enemy มักเกิด (spawn) จากนอกจอ
                // แล้วค่อยเดินเข้ามา ถ้าใช้ tag เดียวกับกระสุน (ซึ่งมักครอบพื้นที่ใกล้จอ) จะโดน Destroy
                // ทันทีตั้งแต่เพิ่ง spawn ก่อนจะทันได้เดินเข้าฉากเลยด้วยซ้ำ
                // EnemyBorder จะถูกวางให้ไกลออกไปกว่านั้น (เลยจุด spawn) เพื่อเปิดพื้นที่ให้เดินเข้ามาได้ก่อน
                Destroy(gameObject);
                return;
            }

            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
            {
                player.Die();
            }
        }

        /// <summary>
        /// เรียกจากภายนอก เช่น PlayerBullet.cs ตอน OnTriggerEnter2D เจอ Enemy
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (isDead) return; // กันโดนซ้ำหลังตายแล้วระหว่างเล่น blink

            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            isDead = true;

            // ปิด collider ทันที กันโดนกระสุนซ้ำระหว่าง blink (เหมือน pattern ของ Player)
            if (hitCollider != null) hitCollider.enabled = false;

            blinkTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;
        }

        private void UpdateDeathBlink()
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer < blinkInterval) return;

            blinkTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (bodySprite != null)
                bodySprite.color = blinkIsColored ? blinkColor : originalColor;

            if (!blinkIsColored)
            {
                blinksDone++;
                if (blinksDone >= blinkCount)
                {
                    // ไม่ Respawn ตามที่ต้องการ - จบเห่ไปเลย
                    // TODO: ทีหลังอยากทำ Object Pool สำหรับ Enemy ด้วย ให้เปลี่ยนตรงนี้เป็นคืน pool แทน Destroy
                    Destroy(gameObject);
                }
            }
        }
    }
}