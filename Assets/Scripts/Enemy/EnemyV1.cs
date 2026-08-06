using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;
using GameSystem;
using CombatSystem;

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

        [Tooltip("ติ๊กถ้าอยากให้ EnemyShooterV1 (ทุกตัวที่ติดอยู่บน GameObject เดียวกัน) ยิงกระสุนได้ระหว่างเดินอยู่ที่ step นี้ " +
                 "ปล่อยว่างไว้ถ้าอยากให้ enemy แค่เดินเฉยๆ ไม่ยิงช่วงนี้")]
        public bool allowShoot = true;
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyV1 : MonoBehaviour, IPoolable, IDamageable
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 10;
        private int currentHealth;

        [Header("Score")]
        [Tooltip("คะแนนที่ผู้เล่นได้ตอนฆ่า Enemy ตัวนี้")]
        [SerializeField] private int scoreValue = 100;

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

        [Header("Fragment Drop")]
        [Tooltip("ติ๊กถ้าอยากให้ Enemy ตัวนี้ดรอป Fragment ตอนตาย - ปกติใช้เฉพาะ Boss prefab เท่านั้น " +
                 "(Fragment ตอนนี้เพิ่ม Power ทันที 1 ระดับต่อชิ้น ไม่ใช่ของเก็บสะสมทั่วไปที่ Enemy ตัวเล็กควรดรอปถี่ๆ)")]
        [SerializeField] private bool dropsFragment = false;
        [Tooltip("Prefab ของ FragmentPickupV1")]
        [SerializeField] private GameObject fragmentPrefab;
        [Tooltip("จำนวน Fragment ที่ดรอปต่อการตาย 1 ครั้ง")]
        [SerializeField] private int fragmentDropCount = 1;

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

        /// <summary>
        /// เผยค่า step ปัจจุบันออกไปให้ EnemyShooterV1 อ่านได้ (read-only จากภายนอก)
        /// ใช้เป็นตัวตัดสินว่า "ตอนนี้เดินอยู่ step ไหน" เพื่อเทียบกับ allowShootPerStep ของแต่ละ Shooter
        /// -1 หมายถึงยังไม่มี path หรือเดินจบ path แล้ว
        /// </summary>
        public int CurrentMovementStepIndex => currentStepIndex;

        /// <summary>
        /// true = ตอนนี้อนุญาตให้ EnemyShooterV1 (ทุกตัวบน GameObject นี้) ยิงกระสุนได้
        /// อ่านค่าจาก allowShoot ของ MovementStep ปัจจุบัน - ถ้าไม่มี movementPath เลย จะเป็น true เสมอ (ไม่ gate อะไร
        /// เพื่อให้ enemy เก่าที่ไม่เคยตั้ง movement path ยังยิงได้ปกติเหมือนก่อนมีระบบนี้)
        /// </summary>
        public bool CanShootNow { get; private set; } = true;

        /// <summary>
        /// ยิงเฉพาะตอน CanShootNow เปลี่ยนจาก false -> true (rising edge) เท่านั้น เช่นเดินผ่าน step ที่ปิดยิงไว้
        /// แล้วกลับมาเจอ step ที่เปิดยิงอีกครั้ง - EnemyShooterV1 ที่ subscribe ไว้จะรีเซ็ต pattern ให้เริ่มเล่นใหม่จากนัดแรก
        /// ไม่ใช่ยิงทุกครั้งที่ค่าเปลี่ยน (ไม่งั้นตอนเปลี่ยนจาก true -> false ก็จะ trigger ด้วยซึ่งไม่มีประโยชน์)
        /// </summary>
        public event System.Action OnShootWindowOpened;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // Kinematic เหตุผลเดียวกับ EnemyBullet/PlayerBullet: ไม่ต้องการแรงจากภายนอกใดๆ
            // Enemy เคลื่อนที่เองผ่าน transform.position ใน UpdateMovement() อยู่แล้ว
            // ที่ต้องมี rb ตัวนี้คือเพื่อให้ trigger event เกิดขึ้นได้ตอนชน Player เท่านั้น
            rb.bodyType = RigidbodyType2D.Kinematic;

            // กันลืมติ๊ก isTrigger ใน Inspector - ถ้าไม่ใช่ trigger กระสุน/Player จะโดนดันแทนที่จะทะลุผ่าน
            if (hitCollider != null) hitCollider.isTrigger = true;

            if (bodySprite != null)
                originalColor = bodySprite.color;

            // หมายเหตุสำคัญ: Awake() รันแค่ "ครั้งเดียว" ตอน object ถูกสร้างจริงๆ (ตอน PoolManager prewarm)
            // ต่อให้ถูกดึงมาใช้ใหม่จาก pool อีกกี่ครั้งก็ตาม Awake() จะไม่รันซ้ำอีกเลย
            // ดังนั้นตรงนี้เก็บไว้แค่ "การตั้งค่า component ที่ไม่เปลี่ยนแปลงระหว่างการใช้งานแต่ละรอบ" เท่านั้น
            // ส่วน "state ของการเล่นรอบนี้" (HP, ตำแหน่งเริ่ม movement, appear ฯลฯ) ย้ายไปไว้ใน OnSpawnFromPool()
            // ซึ่ง PoolManager จะเรียกให้ทุกครั้งที่หยิบตัวนี้มาใช้ ไม่ว่าจะเป็นครั้งแรกหรือครั้งที่ 100
        }

        /// <summary>
        /// เรียกโดย PoolManager ทุกครั้งที่หยิบ Enemy ตัวนี้จาก pool มาใช้งาน (รวมถึงครั้งแรกด้วย)
        /// รวม logic ที่เดิมเคยอยู่ใน Start() ทั้งหมดมาไว้ตรงนี้ เพราะ Start() รันครั้งเดียวไม่พอสำหรับ object ที่ใช้ซ้ำ
        /// </summary>
        public void OnSpawnFromPool()
        {
            isDead = false;
            currentHealth = maxHealth;

            // reset สีกลับเป็นสีปกติ เผื่อตัวก่อนหน้าตายไปตอนกำลัง blink สีแดงค้างอยู่พอดี
            if (bodySprite != null)
                bodySprite.color = originalColor;

            blinkTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;

            appearTimer = 0f;
            if (startHidden)
            {
                SetVisibleAndHittable(false);
                hasAppeared = false;
            }
            else
            {
                SetVisibleAndHittable(true);
                hasAppeared = true;
            }

            // จุดเริ่มต้น "จุดที่ 1" ของ path คือตำแหน่งที่ PoolManager.Get() เพิ่งวางให้ตอนนี้เอง
            // (แทนที่เดิมที่ใช้ transform.position ใน Start() เพราะตอนนั้นมันคือตำแหน่งที่วางไว้ตอน design-time ใน Scene
            // แต่ตอนนี้ object ถูกใช้ซ้ำ ตำแหน่งเริ่มต้นของแต่ละรอบต้องอิงจากตำแหน่ง spawn ที่ StageSpawnerV1 กำหนดแทน)
            segmentStartPosition = transform.position;

            if (movementPath.Count > 0)
            {
                currentStepIndex = 0;
                stepTimer = 0f;
            }
            else
            {
                currentStepIndex = -1;
            }

            // ตั้งสถานะยิงเริ่มต้นตรงๆ โดยไม่ผ่าน UpdateShootGate() (ไม่อยากยิง event OnShootWindowOpened ตอนนี้
            // เพราะการ spawn ใหม่ไม่ใช่ "การเปิดหน้าต่างยิงซ้ำ" - จัดการ reset ตรงนี้แยกให้ชัดเจนแทน)
            CanShootNow = ComputeAllowShootAtCurrentStep();

            // Reset EnemyShooterV1 ทุกตัวที่ติดอยู่บน GameObject นี้ (รองรับกรณีมีหลายตัวพร้อมกันเพื่อยิงหลาย pattern ทีเดียว)
            // ให้เริ่มเล่น pattern จากนัดแรกเสมอทุกครั้งที่ Enemy ถูกดึงจาก pool มาใช้ใหม่
            var shooters = GetComponents<EnemyShooterV1>();
            for (int i = 0; i < shooters.Length; i++)
            {
                shooters[i].ResetAllGroups();
            }
        }

        public void OnReturnToPool()
        {
            // ไม่มี state ค้างที่ต้องเคลียร์เป็นพิเศษตอนคืน pool - OnSpawnFromPool() จะ reset ทุกอย่างใหม่หมดตอนหยิบไปใช้รอบหน้าอยู่แล้ว
        }

        /// <summary>
        /// เรียกจาก Spawner (เช่น StageSpawnerV1/StageWaveGroup) หลัง PoolManager.Get() ทันที
        /// เพื่อ "ยัด" waypoint จริงจาก Scene เข้ามาแทนที่ target เดิมที่ตั้งไว้ตอน design-time บน Prefab
        ///
        /// ทำไมต้องมี method นี้แยกต่างหาก แทนที่จะลาก Transform ใส่ movementPath ใน Prefab ตรงๆ:
        /// Prefab Asset เก็บ reference ไปยัง object ที่อยู่ใน Scene ไม่ได้ (Unity บล็อกไว้ เพราะ prefab ต้องใช้ซ้ำได้ทุก Scene)
        /// ดังนั้นถ้า Enemy ตัวเดียวกันถูกใช้ในหลาย wave/group ที่ต้องเดินคนละเส้นทาง ต้องผูก waypoint ตอน runtime แทน
        /// </summary>
        public void SetMovementTargets(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0) return;

            for (int i = 0; i < movementPath.Count && i < targets.Count; i++)
            {
                if (targets[i] != null) movementPath[i].target = targets[i];
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

            UpdateShootGate();
        }

        /// <summary>
        /// เช็คว่า step ปัจจุบัน (ตาม currentStepIndex) อนุญาตให้ยิงไหม แล้วอัปเดต CanShootNow
        /// ถ้าเพิ่งเปลี่ยนจาก false -> true (rising edge) จะยิง OnShootWindowOpened ให้ EnemyShooterV1 ที่ subscribe ไว้รีเซ็ต pattern
        /// </summary>
        private void UpdateShootGate()
        {
            bool newState = ComputeAllowShootAtCurrentStep();

            if (newState && !CanShootNow)
            {
                OnShootWindowOpened?.Invoke();
            }

            CanShootNow = newState;
        }

        private bool ComputeAllowShootAtCurrentStep()
        {
            if (movementPath.Count == 0) return true; // ไม่มี path เลย - ไม่ gate อะไรทั้งนั้น ยิงได้ตลอดเหมือนพฤติกรรมเดิม

            if (currentStepIndex >= 0 && currentStepIndex < movementPath.Count)
                return movementPath[currentStepIndex].allowShoot;

            // currentStepIndex == -1 คือ path จบแล้ว - ค้างสถานะตามค่าของ step สุดท้ายที่เพิ่งเดินผ่านไว้ ไม่รีเซ็ตเป็นอย่างอื่น
            return CanShootNow;
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
                PoolManager.Instance.Return(gameObject);
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
        /// รับ "attacker" (GameObject ตัวที่ทำ damage) เพิ่ม เพื่อให้ Die() ตัดสินใจได้เองว่าควรให้คะแนนไหม
        /// แทนที่จะเดาว่า "TakeDamage ถูกเรียกจากที่เดียวเสมอ" ซึ่งเป็นสมมติฐานที่พังง่ายถ้าวันหนึ่งมีระบบอื่น
        /// (เช่น boss self-destruct, environmental hazard) มาเรียก TakeDamage() ด้วยเหมือนกัน
        /// attacker เป็น null ได้ (ค่า default) เผื่อ caller ที่ไม่ต้องการระบุ - กรณีนั้นจะไม่ได้คะแนนเป็นค่าปลอดภัยไว้ก่อน
        /// </summary>
        public void TakeDamage(int amount, GameObject attacker = null)
        {
            if (isDead) return; // กันโดนซ้ำหลังตายแล้วระหว่างเล่น blink

            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                // เช็ค tag ของผู้โจมตีตรงๆ ว่าเป็นกระสุนผู้เล่นจริงไหม (ต้องตั้ง Tag "PlayerBullet" ให้ Prefab กระสุนผู้เล่นด้วย)
                // ชัดเจนกว่าการเดาจาก "ใครมักจะเป็นคนเรียก" เพราะเห็นเงื่อนไขตรงๆ ในโค้ด ไม่ใช่ความเชื่อที่ไม่มีการเช็คจริง
                bool killedByPlayer = attacker != null && attacker.CompareTag("PlayerBullet");
                Die(killedByPlayer);
            }
        }

        private void Die(bool awardsScore)
        {
            isDead = true;

            // ปิด collider ทันที กันโดนกระสุนซ้ำระหว่าง blink (เหมือน pattern ของ Player)
            if (hitCollider != null) hitCollider.enabled = false;

            // ให้คะแนนเฉพาะตอนที่ผู้เล่นเป็นคนคิลจริงๆ เท่านั้น (awardsScore มาจากการเช็ค tag "PlayerBullet" ใน TakeDamage())
            // ตายเพราะสาเหตุอื่น (ถ้ามีในอนาคต) จะไม่ได้คะแนน โดยไม่ต้องพึ่งสมมติฐานลับๆ ว่า "TakeDamage ถูกเรียกจากที่เดียวเสมอ" อีกต่อไป
            if (awardsScore && ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(scoreValue);

            // ดรอป Fragment ตอนตายทันที (ไม่ต้องรอ blink จบ) เพราะผู้เล่นควรเห็น/เก็บได้ทันทีที่ศัตรูตาย
            // ไม่ผูกกับจังหวะ blink visual ซึ่งเป็นเรื่องภาพล้วนๆ ไม่เกี่ยวกับ gameplay
            if (dropsFragment && fragmentPrefab != null)
            {
                for (int i = 0; i < fragmentDropCount; i++)
                {
                    PoolManager.Instance.Get(fragmentPrefab, transform.position, Quaternion.identity);
                }
            }

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
                    // ไม่ Respawn ตามที่ต้องการ - จบเห่ไปเลย คืนกลับ pool แทน Destroy
                    PoolManager.Instance.Return(gameObject);
                }
            }
        }
    }
}