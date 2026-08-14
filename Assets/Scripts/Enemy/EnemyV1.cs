using System;
using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;
using GameSystem;
using CombatSystem;

namespace EnemySystem
{
    /// <summary>
    /// รูปแบบการไล่ค่า (ease) ระหว่าง 2 จุด
    /// เพิ่มแบบใหม่ได้ในอนาคตโดยเพิ่ม case ใน ApplyEase()
    /// </summary>
    public enum EaseType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// 1 ช่วงของการเคลื่อนที่ - จากตำแหน่งปัจจุบัน ไปยัง target
    /// ภายในเวลา duration ด้วย easeType ที่กำหนด
    /// </summary>
    [Serializable]
    public class MovementStep
    {
        [Tooltip("ลาก Transform ของจุดหมาย (Empty GameObject ที่วางไว้ใน Scene)")]
        public Transform target;

        [Tooltip("เวลาที่ใช้เดินทางไปถึงจุดนี้ (วินาที)")]
        public float duration = 1f;

        public EaseType easeType = EaseType.Linear;

        [Tooltip(
            "ติ๊กถ้าอยากให้ EnemyBulletPatternV2 ยิงกระสุนได้ระหว่างเดินอยู่ที่ step นี้"
        )]
        public bool allowShoot = true;
    }

    /// <summary>
    /// Configuration สำหรับ Item Drop ของ Enemy
    ///
    /// itemPrefab ควรเป็น prefab ที่มี ItemDropPickup และใช้ Tag:
    /// Life / Spell / Fragment
    /// </summary>
    [Serializable]
    public class EnemyItemDropData
    {
        [Tooltip(
            "Prefab ของ ItemDropPickup ที่จะดรอป\n" +
            "Tag ของ prefab ต้องเป็น Life, Spell หรือ Fragment"
        )]
        public GameObject itemPrefab;

        [Tooltip("จำนวน Item ที่ดรอปต่อการตาย 1 ครั้ง")]
        [Min(1)]
        public int count = 1;
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
        [Tooltip(
            "ถ้าติ๊กไว้ ตัว Enemy จะมองไม่เห็นและโดนยิงไม่ได้ตอนเริ่ม " +
            "จนกว่าจะครบเวลา Appear Delay"
        )]
        [SerializeField] private bool startHidden = false;
        [SerializeField] private float appearDelay = 0f;

        [Header("Movement Path")]
        [Tooltip(
            "ลิสต์จุดที่จะเคลื่อนที่ไปตามลำดับ โดยเริ่มนับจากตำแหน่งปัจจุบัน " +
            "ของ Enemy ตอน Start เป็นจุดตั้งต้น"
        )]
        [SerializeField] private List<MovementStep> movementPath =
            new List<MovementStep>();

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;

        [Tooltip(
            "Collider ของตัว Enemy ต้องติ๊ก isTrigger = true " +
            "เพื่อให้โดน PlayerBullet ได้"
        )]
        [SerializeField] private Collider2D hitCollider;

        [Header("SFX (Optional)")]
        [Tooltip("เสียงตอน Enemy ตัวนี้ spawn ออกมา")]
        [SerializeField] private AudioSource spawnSfx;

        [Tooltip("เสียงตอน Enemy ตัวนี้ตาย")]
        [SerializeField] private AudioSource deathSfx;

        [Header("Item Drop")]
        [Tooltip(
            "กำหนด Item ที่จะดรอปเมื่อตาย\n" +
            "ใช้ Life / Spell / Fragment ItemDropPickup ได้\n" +
            "ปล่อย Item Prefab ว่างไว้ = ไม่ดรอป"
        )]
        [SerializeField] private EnemyItemDropData itemDrop;

        private Color originalColor;
        private bool isDead;

        private Rigidbody2D rb;

        // Death blink state
        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        // Appear state
        private float appearTimer;
        private bool hasAppeared;

        // Movement state
        private int currentStepIndex = -1;
        private float stepTimer;
        private Vector3 segmentStartPosition;

        public int CurrentMovementStepIndex => currentStepIndex;

        public bool CanShootNow { get; private set; } = true;

        public event Action OnShootWindowOpened;

        /// <summary>
        /// ยิงตอน Die() เริ่มทำงาน ก่อน blink visual ใด ๆ
        /// </summary>
        public event Action OnDied;

        /// <summary>
        /// ยิงตอนจบ OnSpawnFromPool() ทุกครั้งที่ Enemy ถูกดึงจาก Pool มาใช้
        /// </summary>
        public event Action OnSpawnedFromPool;

        /// <summary>
        /// ถ้า true ระบบ Death Visual ภายในจะหยุดทำงานทันที
        /// ระบบภายนอกต้องรับผิดชอบ Return เข้า Pool เอง
        /// </summary>
        public bool SuppressBuiltInDeathVisual { get; set; }

        public static event Action<AudioClip> OnAnyEnemySpawnSfx;
        public static event Action<AudioClip> OnAnyEnemyDeathSfx;

        /// <summary>
        /// ยิงเฉพาะตอน Enemy ตายเพราะ PlayerBullet
        /// ใช้สำหรับนับสถิติ Enemy ที่ผู้เล่นกำจัดได้
        /// </summary>
        public static event Action OnAnyEnemyDefeatedByPlayer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            if (hitCollider != null)
                hitCollider.isTrigger = true;

            if (bodySprite != null)
                originalColor = bodySprite.color;
        }

        public void OnSpawnFromPool()
        {
            isDead = false;
            currentHealth = maxHealth;
            SuppressBuiltInDeathVisual = false;

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

            CanShootNow = ComputeAllowShootAtCurrentStep();

            var shooters = GetComponents<EnemyBulletPatternV2>();
            for (int i = 0; i < shooters.Length; i++)
                shooters[i].ResetAllGroups();

            OnSpawnedFromPool?.Invoke();

            if (spawnSfx != null && spawnSfx.clip != null)
                OnAnyEnemySpawnSfx?.Invoke(spawnSfx.clip);
        }

        public void OnReturnToPool()
        {
        }

        public void SetMovementTargets(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            for (int i = 0; i < movementPath.Count && i < targets.Count; i++)
            {
                if (targets[i] != null)
                    movementPath[i].target = targets[i];
            }
        }

        private void Update()
        {
            if (isDead)
            {
                UpdateDeathBlink();
                return;
            }

            if (!hasAppeared)
                UpdateAppear();

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
            if (bodySprite != null)
                bodySprite.enabled = value;

            if (hitCollider != null)
                hitCollider.enabled = value;
        }

        private void UpdateMovement()
        {
            if (currentStepIndex < 0 ||
                currentStepIndex >= movementPath.Count)
                return;

            MovementStep step = movementPath[currentStepIndex];

            if (step.target == null)
            {
                AdvanceToNextStep();
                return;
            }

            stepTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                stepTimer / Mathf.Max(step.duration, 0.0001f)
            );

            float easedT = ApplyEase(t, step.easeType);

            transform.position = Vector3.LerpUnclamped(
                segmentStartPosition,
                step.target.position,
                easedT
            );

            if (t >= 1f)
                AdvanceToNextStep();
        }

        private void AdvanceToNextStep()
        {
            segmentStartPosition = transform.position;
            currentStepIndex++;
            stepTimer = 0f;

            if (currentStepIndex >= movementPath.Count)
                currentStepIndex = -1;

            UpdateShootGate();
        }

        private void UpdateShootGate()
        {
            bool newState = ComputeAllowShootAtCurrentStep();

            if (newState && !CanShootNow)
                OnShootWindowOpened?.Invoke();

            CanShootNow = newState;
        }

        private bool ComputeAllowShootAtCurrentStep()
        {
            if (movementPath.Count == 0)
                return true;

            if (currentStepIndex >= 0 &&
                currentStepIndex < movementPath.Count)
            {
                return movementPath[currentStepIndex].allowShoot;
            }

            return CanShootNow;
        }

        private float ApplyEase(float t, EaseType easeType)
        {
            switch (easeType)
            {
                case EaseType.EaseIn:
                    return t * t;

                case EaseType.EaseOut:
                    return 1f - (1f - t) * (1f - t);

                case EaseType.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default:
                    return t;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead)
                return;

            if (other.CompareTag("EnemyBorder"))
            {
                if (PoolManager.Instance != null)
                    PoolManager.Instance.Return(gameObject);

                return;
            }

            if (!other.CompareTag("Player"))
                return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
                player.Die();
        }

        public void TakeDamage(int amount, GameObject attacker = null)
        {
            if (isDead)
                return;

            currentHealth -= amount;

            if (currentHealth <= 0)
            {
                bool killedByPlayer =
                    attacker != null &&
                    attacker.CompareTag("PlayerBullet");

                Die(killedByPlayer);
            }
        }

        private void Die(bool awardsScore)
        {
            isDead = true;

            OnDied?.Invoke();

            if (deathSfx != null && deathSfx.clip != null)
                OnAnyEnemyDeathSfx?.Invoke(deathSfx.clip);

            if (hitCollider != null)
                hitCollider.enabled = false;

            if (awardsScore && ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(scoreValue);

            if (awardsScore)
                OnAnyEnemyDefeatedByPlayer?.Invoke();

            SpawnItemDrop();

            blinkTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;
        }

        /// <summary>
        /// Spawn ItemDropPickup ผ่าน Pool
        ///
        /// Enemy ไม่จำเป็นต้องรู้ว่า prefab เป็น Life / Spell / Fragment
        /// เพราะ ItemDropPickup จะอ่าน Tag ของ prefab เองตอนถูกเก็บ
        /// </summary>
        private void SpawnItemDrop()
        {
            if (itemDrop == null ||
                itemDrop.itemPrefab == null ||
                itemDrop.count <= 0)
            {
                return;
            }

            if (PoolManager.Instance == null)
                return;

            for (int i = 0; i < itemDrop.count; i++)
            {
                PoolManager.Instance.Get(
                    itemDrop.itemPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }
        }

        private void UpdateDeathBlink()
        {
            if (SuppressBuiltInDeathVisual)
                return;

            blinkTimer += Time.deltaTime;

            if (blinkTimer < blinkInterval)
                return;

            blinkTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (bodySprite != null)
            {
                bodySprite.color =
                    blinkIsColored ? blinkColor : originalColor;
            }

            if (!blinkIsColored)
            {
                blinksDone++;

                if (blinksDone >= blinkCount)
                {
                    if (PoolManager.Instance != null)
                        PoolManager.Instance.Return(gameObject);
                }
            }
        }
    }
}