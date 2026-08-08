using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;
using PlayerSystem;
using GameSystem;
using CombatSystem;
using EnemySystem; // reuse ShootGroup/ShootBurst/EaseType/EnemyBulletV1 ที่มีอยู่แล้ว ไม่ต้อง duplicate โครงสร้างซ้ำ

namespace BossSystem
{
    /// <summary>
    /// เงื่อนไขจบ Phase - เลือกได้ต่อ Phase อิสระจากกัน (บาง phase ใช้ HP บาง phase ใช้เวลา บาง phase ใช้ทั้งคู่)
    /// </summary>
    public enum PhaseEndCondition
    {
        HpOnly,         // จบเมื่อ HP ของ phase นี้หมดเท่านั้น (ไม่มี timeout)
        TimerOnly,      // จบเมื่อครบเวลาเท่านั้น (ตี HP เท่าไหร่ก็ไม่ตาย ต้องรอหมดเวลา - ใช้กับ survive-pattern)
        WhicheverFirst  // จบทันทีที่เงื่อนไขไหนถึงก่อน (HP หมด หรือ หมดเวลา)
    }

    /// <summary>
    /// สถานะหลักของบอสทั้งตัว (แยกจาก Phase index) คุมว่าตอนนี้ทำอะไรอยู่
    /// </summary>
    public enum BossState
    {
        Inactive,       // ยังไม่เริ่ม (ก่อน Start() ทำงาน)
        Entering,       // กำลังเล่น movement (ทั้งเข้าฉากครั้งแรก และ reposition ระหว่าง phase)
        PhaseActive,    // กำลังยิง/รับดาเมจได้ตาม pattern ของ phase ปัจจุบัน
        PhaseTransition,// ช่วงพักระหว่าง phase - อมตะ + เคลียร์กระสุนบนจอ
        Defeated        // ตายแล้ว กำลังเล่น blink ก่อน Destroy
    }

    /// <summary>
    /// ข้อมูล 1 Phase ของบอส - ตั้งค่าทุกอย่างของ phase นี้ไว้ในนี้ทั้งก้อน (HP, เวลา, pattern, reposition, คะแนน)
    /// เพื่อให้ดีไซน์บอสใหม่ทั้งตัวทำได้จบใน Inspector เดียว ไม่ต้องกระโดดไปแก้หลายที่
    /// </summary>
    [System.Serializable]
    public class BossPhaseData
    {
        [Header("ข้อมูลทั่วไป")]
        public string phaseName = "Phase 1";
        [Tooltip("ติ๊กถ้า phase นี้เป็น Spell Card (โชว์ชื่อ spell ทาง UI + track การ capture) " +
                 "ปล่อยว่างไว้ถ้าเป็นแค่ phase โจมตีปกติ (non-spell)")]
        public bool isSpellCard = false;

        [Header("HP / เวลา")]
        [Tooltip("HP เฉพาะของ phase นี้เท่านั้น (รีเซ็ตใหม่ทุกครั้งที่เข้า phase นี้ - ไม่ใช่ HP รวมทั้งบอส)")]
        public int phaseHP = 500;
        [Tooltip("เงื่อนไขจบ phase นี้ - ดูรายละเอียดที่ enum PhaseEndCondition")]
        public PhaseEndCondition endCondition = PhaseEndCondition.HpOnly;
        [Tooltip("เวลาจำกัดของ phase นี้ (วินาที) - มีผลเฉพาะตอน endCondition ไม่ใช่ HpOnly")]
        public float timeLimit = 30f;

        [Header("Pattern การยิง")]
        [Tooltip("รายการกลุ่มยิงของ phase นี้ - reuse โครงสร้าง ShootGroup/ShootBurst เดียวกับ EnemyShooterV1 " +
                 "แต่ละ phase มี pattern list เป็นของตัวเอง สลับกันทั้งชุดตอนเปลี่ยน phase")]
        public List<ShootGroup> patterns = new List<ShootGroup>();

        [Header("Reposition ก่อนเริ่ม Phase นี้ (ไม่บังคับ)")]
        [Tooltip("ถ้าลากไว้ บอสจะเดินไปจุดนี้ก่อน (ระหว่าง Transition ของ phase ก่อนหน้า) แล้วค่อยเริ่ม phase นี้ " +
                 "ปล่อยว่างไว้ถ้าอยากให้บอสอยู่ตำแหน่งเดิม ไม่ต้องขยับ")]
        public Transform repositionTarget;
        public float repositionDuration = 1f;
        public EaseType repositionEase = EaseType.EaseInOut;

        [Header("คะแนน")]
        [Tooltip("คะแนนที่ได้ตอนจบ phase นี้แบบปกติ (ไม่ capture หรือไม่ใช่ spell card)")]
        public int bonusScoreOnClear = 1000;
        [Tooltip("คะแนนพิเศษที่ได้ถ้า capture สำเร็จ - มีผลเฉพาะ phase ที่ isSpellCard = true เท่านั้น")]
        public int bonusScoreOnCapture = 3000;

        // ---- runtime state เฉพาะของ phase นี้ - ไม่โชว์ Inspector เพราะเป็นค่าที่เปลี่ยนตอนเล่นจริง ----
        [System.NonSerialized] public int currentHP;
        [System.NonSerialized] public float timer;
    }

    /// <summary>
    /// บอส - แยกจาก EnemyV1 โดยสิ้นเชิงตามที่ตกลงไว้ (ไม่ผ่าน pool, ใช้ Instantiate/Destroy ตรงๆ)
    ///
    /// เรื่องสำคัญที่สุดของไฟล์นี้: การยิงกระสุนของบอส "ต้องผ่าน PoolManager.Instance.Get() เท่านั้น"
    /// (เหมือนที่ EnemyShooterV1.FireBurst() ทำ) ห้ามใช้ Instantiate() ตรงๆ เด็ดขาด - เวอร์ชันก่อนหน้าของบอส
    /// เขียน shooting logic แยกเองแล้วไม่ได้ผ่าน pool ทำให้ GC spike รัวๆ จนเกมแลคหนักตอนบอสยิงถี่ๆ
    /// ไฟล์นี้ก็อป logic การยิง (UpdateGroup/FireBurst/RotateVector) มาจาก EnemyShooterV1 ที่พิสูจน์แล้วว่าเร็ว/เสถียร
    /// แต่ปรับให้ยิงจาก "pattern list ของ phase ปัจจุบัน" แทนที่จะเป็น list เดียวคงที่ทั้งตัว
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BossV1 : MonoBehaviour, IDamageable
    {
        [Header("Phases")]
        [Tooltip("เรียงตามลำดับที่จะเล่น - phase สุดท้ายจบแล้ว = บอสตาย")]
        [SerializeField] private List<BossPhaseData> phases = new List<BossPhaseData>();

        [Header("Fire Point")]
        [Tooltip("จุดที่กระสุนจะ spawn ออกมา - ถ้าไม่ใส่จะใช้ตำแหน่งตัวบอสเอง")]
        [SerializeField] private Transform firePoint;

        [Header("Entrance (เข้าฉากครั้งแรก)")]
        [Tooltip("จุดที่บอสจะเดินไปตอนเริ่มฉาก ก่อนเข้า Phase แรก - ปล่อยว่างถ้าอยากให้เริ่ม phase แรกทันทีไม่ต้องเดิน")]
        [SerializeField] private Transform entranceTarget;
        [SerializeField] private float entranceDuration = 2f;
        [SerializeField] private EaseType entranceEase = EaseType.EaseOut;

        [Header("Transition ระหว่าง Phase")]
        [Tooltip("เวลาหน่วง (วินาที) ระหว่าง phase ก่อนเริ่ม phase ถัดไป - ช่วงนี้บอสอมตะและกระสุนบนจอจะถูกเคลียร์ทิ้งหมด")]
        [SerializeField] private float transitionDuration = 1f;

        [Header("Death Visual")]
        [SerializeField] private int blinkCount = 5;
        [SerializeField] private float blinkInterval = 0.08f;
        [SerializeField] private Color blinkColor = Color.red;

        [Header("Fragment Drop")]
        [Tooltip("Prefab ของ FragmentPickupV1 - บอสควรดรอปเยอะกว่า enemy ทั่วไปตามธรรมเนียมเกมแนวนี้")]
        [SerializeField] private GameObject fragmentPrefab;
        [SerializeField] private int fragmentDropCount = 10;

        [Header("References")]
        [Tooltip("สำคัญ: ต้องตั้ง Tag ของ GameObject นี้เป็น \"Boss\" ด้วย เพราะ PlayerBulletV1 เช็ค tag นี้ก่อนจะเช็ค IDamageable")]
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Collider ของตัวบอส ต้องติ๊ก isTrigger = true เพื่อให้โดน PlayerBullet ได้")]
        [SerializeField] private Collider2D hitCollider;

        // ---------------- Events สำหรับ UI/ระบบอื่น (เช่น BossHealthUI, StageResultManager) ----------------
        public event System.Action OnBossAppeared;
        public event System.Action<BossPhaseData, int> OnPhaseStarted;      // (phase data, phase index)
        public event System.Action<int, int> OnPhaseHPChanged;              // (current, max)
        public event System.Action<float, float> OnPhaseTimerChanged;       // (remaining, total)
        public event System.Action<BossPhaseData> OnSpellCaptured;          // ยิงตอน capture สำเร็จเท่านั้น
        public event System.Action OnBossDefeated;                          // TODO: ผูกกับ StageResultManager (Stage Clear)

        private BossState state = BossState.Inactive;
        private int currentPhaseIndex = -1;
        private Transform playerTransform;
        private Rigidbody2D rb;
        private Color originalColor;
        private bool isDead;

        // ---- movement state (ใช้ร่วมกันทั้ง entrance และ reposition ระหว่าง phase - โครงเดียวกับ EnemyV1.UpdateMovement) ----
        private Vector3 moveStartPosition;
        private Transform moveTarget;
        private float moveDuration;
        private EaseType moveEase;
        private float moveTimer;
        private System.Action onMoveComplete;

        // ---- transition state ----
        private float transitionTimer;

        // ---- capture tracking - เก็บ CurrentLives ตอนเริ่ม spell card ไว้เทียบตอนจบ phase ----
        private int captureCheckLives;

        // ---- death blink state ----
        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        /// <summary>true ระหว่าง Entering/PhaseTransition/Defeated - ใช้เป็น gate ไม่ให้ TakeDamage ทำงานตอนอมตะ</summary>
        public bool IsInvincible => state != BossState.PhaseActive;

        public int CurrentPhaseIndex => currentPhaseIndex;
        public int TotalPhases => phases.Count;
        public BossPhaseData CurrentPhase =>
            (currentPhaseIndex >= 0 && currentPhaseIndex < phases.Count) ? phases[currentPhaseIndex] : null;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // Kinematic เหตุผลเดียวกับ EnemyV1: ต้องการแค่ Rigidbody2D ไว้ให้ trigger event ทำงานตอนชน Player
            rb.bodyType = RigidbodyType2D.Kinematic;

            if (hitCollider != null) hitCollider.isTrigger = true;
            if (bodySprite != null) originalColor = bodySprite.color;
            if (firePoint == null) firePoint = transform;
        }

        void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;

            BeginEntrance();
        }

        void Update()
        {
            switch (state)
            {
                case BossState.Entering:
                    UpdateMovement();
                    break;

                case BossState.PhaseActive:
                    UpdatePhaseActive();
                    break;

                case BossState.PhaseTransition:
                    UpdateTransition();
                    break;

                case BossState.Defeated:
                    UpdateDeathBlink();
                    break;
            }
        }

        // ==================== Entrance ====================

        private void BeginEntrance()
        {
            state = BossState.Entering;

            if (entranceTarget != null)
            {
                StartMove(entranceTarget, entranceDuration, entranceEase, BeginFirstPhase);
            }
            else
            {
                BeginFirstPhase();
            }
        }

        private void BeginFirstPhase()
        {
            OnBossAppeared?.Invoke();
            StartPhase(0);
        }

        // ==================== Movement (ใช้ทั้ง entrance และ reposition) ====================

        /// <summary>
        /// เริ่มเคลื่อนที่ไปยัง target ภายในเวลา duration แล้วเรียก onComplete ตอนถึง
        /// ต้องตั้ง state = Entering เองก่อนเรียกเมธอดนี้เสมอ (Update() จะ dispatch ไป UpdateMovement() ตาม state)
        /// </summary>
        private void StartMove(Transform target, float duration, EaseType ease, System.Action onComplete)
        {
            moveStartPosition = transform.position;
            moveTarget = target;
            moveDuration = duration;
            moveEase = ease;
            moveTimer = 0f;
            onMoveComplete = onComplete;
        }

        private void UpdateMovement()
        {
            // กันลืมลาก target ไว้ - ข้ามไปเรียก onComplete ทันทีแทนที่จะค้าง
            if (moveTarget == null)
            {
                InvokeMoveComplete();
                return;
            }

            moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveTimer / Mathf.Max(moveDuration, 0.0001f));
            float easedT = ApplyEase(t, moveEase);

            transform.position = Vector3.LerpUnclamped(moveStartPosition, moveTarget.position, easedT);

            if (t >= 1f)
            {
                InvokeMoveComplete();
            }
        }

        private void InvokeMoveComplete()
        {
            // เคลียร์ callback ก่อนเรียก กัน callback เก่าถูกเรียกซ้ำถ้า onComplete ไปตั้ง move ใหม่ต่อทันที
            System.Action callback = onMoveComplete;
            onMoveComplete = null;
            callback?.Invoke();
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
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default: // Linear
                    return t;
            }
        }

        // ==================== Phase lifecycle ====================

        private void StartPhase(int index)
        {
            // ไม่มี phase เหลือแล้ว - จบครบทุก phase = บอสตายจริง
            if (index >= phases.Count)
            {
                BeginDefeatSequence();
                return;
            }

            currentPhaseIndex = index;
            BossPhaseData phase = phases[index];

            phase.currentHP = phase.phaseHP;
            phase.timer = 0f;

            ResetAllPatternGroups(phase);

            // เริ่ม track capture เฉพาะ spell card - เก็บ lives ตอนนี้ไว้เทียบตอนจบ phase
            if (phase.isSpellCard && PlayerStatsManager.Instance != null)
            {
                captureCheckLives = PlayerStatsManager.Instance.CurrentLives;
            }

            state = BossState.PhaseActive;

            OnPhaseStarted?.Invoke(phase, index);
            OnPhaseHPChanged?.Invoke(phase.currentHP, phase.phaseHP);
            OnPhaseTimerChanged?.Invoke(phase.timeLimit - phase.timer, phase.timeLimit);
        }

        private void UpdatePhaseActive()
        {
            BossPhaseData phase = phases[currentPhaseIndex];

            UpdateShootPatterns(phase);

            phase.timer += Time.deltaTime;

            // อัปเดต timer UI เฉพาะตอนที่ phase นี้แคร์เรื่องเวลาจริงๆ (ไม่ต้องยิง event ทุกเฟรมถ้า HpOnly ไม่มีใครใช้)
            if (phase.endCondition != PhaseEndCondition.HpOnly)
            {
                OnPhaseTimerChanged?.Invoke(Mathf.Max(phase.timeLimit - phase.timer, 0f), phase.timeLimit);
            }

            bool hpDone = phase.currentHP <= 0;
            bool timeDone = phase.endCondition != PhaseEndCondition.HpOnly && phase.timer >= phase.timeLimit;

            bool phaseCleared;
            switch (phase.endCondition)
            {
                case PhaseEndCondition.TimerOnly:
                    phaseCleared = timeDone;
                    break;
                case PhaseEndCondition.WhicheverFirst:
                    phaseCleared = hpDone || timeDone;
                    break;
                default: // HpOnly
                    phaseCleared = hpDone;
                    break;
            }

            if (phaseCleared)
            {
                EndPhase(phase);
            }
        }

        private void EndPhase(BossPhaseData phase)
        {
            // capture สำเร็จ = เป็น spell card + lives ไม่ลดเลยตลอด phase นี้ (เทียบกับตอนเริ่ม)
            bool captured = phase.isSpellCard
                && PlayerStatsManager.Instance != null
                && PlayerStatsManager.Instance.CurrentLives == captureCheckLives;

            if (captured)
            {
                if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(phase.bonusScoreOnCapture);
                OnSpellCaptured?.Invoke(phase);
            }
            else if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(phase.bonusScoreOnClear);
            }

            BeginTransition();
        }

        private void BeginTransition()
        {
            state = BossState.PhaseTransition;
            transitionTimer = 0f;

            // เคลียร์กระสุนบนจอทันทีตอนเริ่ม transition กันกระสุนจาก phase ก่อนหน้าค้างมาโดนผู้เล่นทั้งที่ phase จบแล้ว
            ClearAllEnemyBullets();
        }

        private void UpdateTransition()
        {
            transitionTimer += Time.deltaTime;
            if (transitionTimer < transitionDuration) return;

            int nextIndex = currentPhaseIndex + 1;

            if (nextIndex >= phases.Count)
            {
                BeginDefeatSequence();
                return;
            }

            BossPhaseData nextPhase = phases[nextIndex];

            if (nextPhase.repositionTarget != null)
            {
                // reuse state Entering เพื่อเดินไปจุดใหม่ก่อน แล้วค่อย StartPhase ตอนถึง
                state = BossState.Entering;
                StartMove(nextPhase.repositionTarget, nextPhase.repositionDuration, nextPhase.repositionEase,
                    () => StartPhase(nextIndex));
            }
            else
            {
                StartPhase(nextIndex);
            }
        }

        /// <summary>
        /// เก็บกระสุนศัตรูทุกนัดที่ยังลอยอยู่บนจอกลับเข้า pool ทั้งหมด (ใช้ tag "EnemyBullet" ตัวเดียวกับที่
        /// GrazeDetectorV1/PlayerShieldV1 เช็คอยู่แล้ว - กระสุนของบอสต้องตั้ง tag นี้เหมือนกระสุน enemy ทั่วไปด้วย)
        /// </summary>
        private void ClearAllEnemyBullets()
        {
            GameObject[] bullets = GameObject.FindGameObjectsWithTag("EnemyBullet");
            for (int i = 0; i < bullets.Length; i++)
            {
                PoolManager.Instance.Return(bullets[i]);
            }
        }

        // ==================== Shooting ====================
        // ก็อป logic มาจาก EnemyShooterV1 (UpdateGroup/FireBurst/RotateVector/CalculateAngleOffset) ตรงๆ
        // เพราะพิสูจน์แล้วว่าทำงานถูกต้อง/เร็ว ต่างกันแค่ยิงจาก "pattern list ของ phase ปัจจุบัน" แทน list คงที่ตัวเดียว

        private void UpdateShootPatterns(BossPhaseData phase)
        {
            List<ShootGroup> groups = phase.patterns;
            for (int g = 0; g < groups.Count; g++)
            {
                UpdateGroup(groups[g]);
            }
        }

        private void UpdateGroup(ShootGroup group)
        {
            if (group.pattern.Count == 0 || group.patternFinished) return;

            ShootBurst burst = group.pattern[group.currentBurstIndex];
            int totalRepeats = Mathf.Max(burst.repeatCount, 1);

            if (!group.hasFiredCurrentBurst)
            {
                FireBurst(burst, 0);
                group.hasFiredCurrentBurst = true;
                group.currentRepeatIndex = 0;
                group.repeatTimer = 0f;
                group.delayTimer = 0f;
                return;
            }

            bool stillRepeating = group.currentRepeatIndex < totalRepeats - 1;

            if (stillRepeating)
            {
                group.repeatTimer += Time.deltaTime;
                while (group.repeatTimer >= burst.repeatInterval)
                {
                    group.repeatTimer -= burst.repeatInterval;
                    group.currentRepeatIndex++;
                    FireBurst(burst, group.currentRepeatIndex);

                    if (group.currentRepeatIndex >= totalRepeats - 1)
                        break;
                }
                return;
            }

            group.delayTimer += Time.deltaTime;
            if (group.delayTimer >= burst.delayAfter)
            {
                AdvanceToNextBurst(group);
            }
        }

        private void AdvanceToNextBurst(ShootGroup group)
        {
            group.currentBurstIndex++;
            group.hasFiredCurrentBurst = false;
            group.currentRepeatIndex = 0;
            group.repeatTimer = 0f;

            if (group.currentBurstIndex >= group.pattern.Count)
            {
                if (group.loopPattern)
                    group.currentBurstIndex = 0;
                else
                    group.patternFinished = true;
            }
        }

        private void ResetAllPatternGroups(BossPhaseData phase)
        {
            for (int i = 0; i < phase.patterns.Count; i++)
            {
                phase.patterns[i].ResetState();
            }
        }

        private void FireBurst(ShootBurst burst, int repeatIndex)
        {
            if (burst.bulletPrefab == null)
            {
                Debug.LogWarning($"BossV1 ({name}): มี Burst ที่ไม่ได้ตั้งค่า bulletPrefab ไว้ - ข้ามการยิงชุดนี้");
                return;
            }

            Vector2 baseCenterDirection = burst.aimAtPlayer
                ? GetDirectionToPlayer()
                : burst.baseDirection.normalized;

            Vector2 centerDirection = RotateVector(baseCenterDirection, burst.angleOffsetPerRepeat * repeatIndex);

            int count = Mathf.Max(burst.bulletCount, 1);

            for (int i = 0; i < count; i++)
            {
                float angleOffset = CalculateAngleOffset(i, count, burst.spreadAngle);
                Vector2 direction = RotateVector(centerDirection, angleOffset);

                // สำคัญ: ต้องผ่าน PoolManager.Instance.Get() เท่านั้น ห้าม Instantiate() ตรงๆ (ดูคอมเมนต์บนคลาส)
                GameObject bulletObj = PoolManager.Instance.Get(burst.bulletPrefab, firePoint.position, Quaternion.identity);
                if (bulletObj.TryGetComponent<EnemyBulletV1>(out var bullet))
                {
                    bullet.SetDirection(direction);
                }
            }
        }

        private Vector2 GetDirectionToPlayer()
        {
            if (playerTransform == null) return Vector2.down;
            return ((Vector2)playerTransform.position - (Vector2)firePoint.position).normalized;
        }

        private float CalculateAngleOffset(int index, int count, float spreadAngle)
        {
            if (count == 1) return 0f;
            float step = spreadAngle / (count - 1);
            return -spreadAngle / 2f + step * index;
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        // ==================== Damage ====================

        /// <summary>
        /// รับดาเมจจาก PlayerBulletV1 ผ่าน IDamageable - อมตะทุกช่วงที่ไม่ใช่ PhaseActive
        /// (Entering/PhaseTransition/Defeated ทั้งหมดต้องเพิกเฉยต่อดาเมจ)
        /// </summary>
        public void TakeDamage(int amount, GameObject attacker = null)
        {
            if (isDead) return;
            if (state != BossState.PhaseActive) return;

            BossPhaseData phase = phases[currentPhaseIndex];
            phase.currentHP -= amount;
            if (phase.currentHP < 0) phase.currentHP = 0;

            OnPhaseHPChanged?.Invoke(phase.currentHP, phase.phaseHP);
        }

        // ==================== Defeat ====================

        private void BeginDefeatSequence()
        {
            state = BossState.Defeated;
            isDead = true;

            if (hitCollider != null) hitCollider.enabled = false;

            if (fragmentPrefab != null)
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
                    // TODO: ยิง event นี้ก่อน Destroy - StageResultManager ควรฟัง OnBossDefeated ตัวนี้
                    // เพื่อ trigger Stage Clear (แทน BossV1.OnBossDefeated ตัวเก่าที่ signature อาจไม่ตรงกับ TODO เดิม
                    // ต้องไปเช็ค/แก้จุด subscribe ใน StageResultManager ให้ตรงกับ event ใหม่นี้ด้วย)
                    OnBossDefeated?.Invoke();

                    // บอสไม่ผ่าน pool ตาม convention ของโปรเจกต์ - Destroy() ตรงๆ ได้เลย
                    Destroy(gameObject);
                }
            }
        }

        // ==================== Collision (ชนตัวบอสโดยตรง) ====================

        void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead) return;
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
            {
                player.Die();
            }
        }
    }
}