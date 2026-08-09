using System;
using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;
using GameSystem;
using CombatSystem;
using EnemySystem; // ใช้ EaseType, MovementStep ร่วมกับระบบ Enemy

namespace BossSystem
{
    // ============================================================
    // PHASE DATA
    // ============================================================

    [Serializable]
    public class BossPhaseData
    {
        [Tooltip("ชื่อ Phase - ใช้แค่ให้อ่านง่ายใน Inspector เท่านั้น")]
        public string phaseName = "Phase 1";

        [Header("Health")]
        [Min(1)]
        [Tooltip("HP ของ Phase นี้ (รีเซ็ตใหม่ทุกครั้งที่ Phase นี้เริ่ม)")]
        public int phaseHealth = 100;

        [Header("Spell Card")]
        [Tooltip("ติ๊กถ้า Phase นี้เป็น Spell Card (มี time limit, capture ได้ถ้าผู้เล่นอยู่รอดจนหมดเวลาโดยไม่ทำ HP หมดก่อน)")]
        public bool isSpellCard = false;

        [Tooltip("ใช้แสดงผลตอน isSpellCard = true เท่านั้น")]
        public string spellCardName = "";

        [Tooltip("เวลาจำกัดของ Spell Card (วินาที) - 0 = ไม่จำกัดเวลา (ต้องตี HP ให้หมดอย่างเดียว) " +
                 "ถ้าผู้เล่นอยู่รอดจนครบเวลาโดย HP ยังไม่หมด = Capture สำเร็จ")]
        [Min(0f)]
        public float spellCardTimeLimit = 0f;

        [Header("Transition")]
        [Tooltip("จุดที่ Boss จะเดินไปเตรียมตัวในช่วง Phase Transition ก่อนเริ่ม Phase นี้ (เว้นว่างไว้ถ้าให้อยู่กับที่)")]
        public Transform transitionTargetPoint;
        [Tooltip("เวลาที่ใช้พักตอนเปลี่ยนเฟส (และเวลาที่ใช้เดินไปยังจุดเป้าหมายด้านบน) - ถ้าเป็น 0 จะใช้ค่า Default จาก BossV2")]
        [Min(0f)]
        public float transitionDuration = 0f;
        public EaseType transitionEase = EaseType.EaseInOut;

        [Header("Movement")]
        [Tooltip("ลิสต์จุดที่จะเคลื่อนที่ไปตามลำดับในระหว่างที่อยู่ในเฟสนี้ (เหมือน EnemyV1)")]
        public List<MovementStep> movementPath = new List<MovementStep>();
        
        [Tooltip("ติ๊กเพื่อให้บอสเดินวนลูปตาม Path ไปเรื่อยๆ จนกว่าเฟสจะจบ")]
        public bool loopMovement = true;

        [Header("Bullet Pattern")]
        [Tooltip("Pattern ที่จะยิงระหว่าง Phase นี้ Active อยู่ - โครงสร้างเดียวกับ EnemyBulletPatternV2 ทุกประการ")]
        public List<BulletPatternGroup> patterns = new List<BulletPatternGroup>();

        // ---- runtime state (ไม่โชว์ใน Inspector) ----
        [NonSerialized] public int currentHealth;
        [NonSerialized] public float phaseTimer;
        [NonSerialized] public BulletPatternV2Executor executor;

        public void ResetRuntime()
        {
            currentHealth = phaseHealth;
            phaseTimer = 0f;
            executor?.ResetAllGroups();
        }
    }

    // ============================================================
    // BOSS V2
    // ============================================================

    [RequireComponent(typeof(Rigidbody2D))]
    public class BossV2 : MonoBehaviour, IPoolable, IDamageable
    {
        public enum BossState
        {
            Inactive,
            Entering,
            PhaseActive,
            PhaseTransition,
            Defeated
        }

        [Header("Entry")]
        [Tooltip("จุดที่ Boss จะเดินเข้ามาหยุดก่อนเริ่ม Phase แรก - เว้นว่างไว้ถ้าอยากให้เริ่ม Phase 1 ทันทีที่ Activate() โดยไม่มี Entry")]
        [SerializeField] private Transform entryTargetPoint;
        [SerializeField] private float entryDuration = 2f;
        [SerializeField] private EaseType entryEase = EaseType.EaseOut;

        [Header("Phases")]
        [Tooltip("เรียงตามลำดับที่จะเล่น - Phase สุดท้ายที่ HP หมดจะถือว่า Boss ตาย")]
        [SerializeField] private List<BossPhaseData> phases = new List<BossPhaseData>();

        [Header("Phase Transition (Default)")]
        [Tooltip("ช่วงพักระหว่างเปลี่ยน Phase (วินาที) - ค่านี้จะเป็นค่าเริ่มต้น ถ้า Phase นั้นๆ ไม่ได้ระบุเวลา Transition แยกไว้")]
        [SerializeField] private float phaseTransitionDuration = 1.2f;

        [Tooltip("เคลียร์กระสุนศัตรูทั้งหมดบนจอตอนเปลี่ยน Phase (มาตรฐานของเกมแนว Touhou)")]
        [SerializeField] private bool clearBulletsOnPhaseTransition = true;

        [Header("Death Visual")]
        [SerializeField] private int blinkCount = 6;
        [SerializeField] private float blinkInterval = 0.08f;
        [SerializeField] private Color blinkColor = Color.white;

        [Header("Score")]
        [SerializeField] private int scoreValueOnDefeat = 1000;

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Collider ของตัว Boss ต้องติ๊ก isTrigger = true")]
        [SerializeField] private Collider2D hitCollider;

        private Rigidbody2D rb;
        private Transform playerTransform;
        private Color originalColor;

        // entry state
        private Vector3 entryStartPosition;
        private float entryTimer;

        // phase transition state
        private float phaseTransitionTimer;
        private float currentTransitionDuration;
        private Vector3 transitionStartPosition;
        private Transform currentTransitionTarget;
        private EaseType currentTransitionEase;

        // phase movement state
        private int currentMovementStepIndex = -1;
        private float movementStepTimer;
        private Vector3 movementSegmentStartPosition;
        private bool canShootNow = true;

        // death blink state
        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        public BossState CurrentState { get; private set; } = BossState.Inactive;
        public int CurrentPhaseIndex { get; private set; } = -1;
        public int PhaseCount => phases != null ? phases.Count : 0;

        public bool IsInvulnerable => CurrentState != BossState.PhaseActive;

        public BossPhaseData CurrentPhase =>
            (CurrentPhaseIndex >= 0 && CurrentPhaseIndex < phases.Count)
                ? phases[CurrentPhaseIndex]
                : null;

        public event Action<int> OnPhaseStarted;
        public event Action<int, bool> OnPhaseCleared;
        public event Action OnBossDefeated;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; 

            if (hitCollider != null)
                hitCollider.isTrigger = true;

            if (bodySprite != null)
                originalColor = bodySprite.color;
        }

        private void Start()
        {
            CachePlayer();
            Activate(); 
        }

        public void OnSpawnFromPool()
        {
            CachePlayer();

            CurrentState = BossState.Inactive;
            CurrentPhaseIndex = -1;

            entryTimer = 0f;
            phaseTransitionTimer = 0f;
            currentMovementStepIndex = -1;
            canShootNow = true;

            blinkTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;

            if (bodySprite != null)
                bodySprite.color = originalColor;

            if (hitCollider != null)
                hitCollider.enabled = true;

            for (int i = 0; i < phases.Count; i++)
                phases[i]?.ResetRuntime();

            Activate(); 
        }

        public void OnReturnToPool()
        {
        }

        private void CachePlayer()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            playerTransform = playerObject != null ? playerObject.transform : null;
        }

        public void Activate()
        {
            if (CurrentState != BossState.Inactive)
                return;

            entryStartPosition = transform.position;
            entryTimer = 0f;
            CurrentState = BossState.Entering;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            switch (CurrentState)
            {
                case BossState.Entering:
                    UpdateEntering(dt);
                    break;
                case BossState.PhaseActive:
                    UpdatePhaseActive(dt);
                    break;
                case BossState.PhaseTransition:
                    UpdatePhaseTransition(dt);
                    break;
                case BossState.Defeated:
                    UpdateDefeated(dt);
                    break;
            }
        }

        private void UpdateEntering(float dt)
        {
            if (entryTargetPoint == null)
            {
                StartPhase(0);
                return;
            }

            entryTimer += dt;
            float t = Mathf.Clamp01(entryTimer / Mathf.Max(entryDuration, 0.0001f));
            float eased = ApplyEase(t, entryEase);

            transform.position = Vector3.LerpUnclamped(entryStartPosition, entryTargetPoint.position, eased);

            if (t >= 1f)
                StartPhase(0);
        }

        private void UpdatePhaseActive(float dt)
        {
            BossPhaseData phase = CurrentPhase;
            if (phase == null)
                return;

            UpdatePhaseMovement(dt, phase);

            bool currentAllowShoot = true;
            if (currentMovementStepIndex >= 0 && currentMovementStepIndex < phase.movementPath.Count)
            {
                currentAllowShoot = phase.movementPath[currentMovementStepIndex].allowShoot;
            }

            // ถ้าเพิ่งถูกอนุญาตให้ยิง (จากที่โดนบล็อกไว้ใน step ก่อนหน้า) ให้เริ่ม Pattern ใหม่
            if (currentAllowShoot && !canShootNow)
            {
                phase.executor?.ResetAllGroups();
            }
            canShootNow = currentAllowShoot;

            if (canShootNow)
            {
                phase.executor?.Tick(dt);
            }

            if (phase.isSpellCard && phase.spellCardTimeLimit > 0f)
            {
                phase.phaseTimer += dt;
                if (phase.phaseTimer >= phase.spellCardTimeLimit)
                    EndCurrentPhase(captured: true);
            }
        }

        private void UpdatePhaseMovement(float dt, BossPhaseData phase)
        {
            if (currentMovementStepIndex < 0 || currentMovementStepIndex >= phase.movementPath.Count)
                return;

            MovementStep step = phase.movementPath[currentMovementStepIndex];
            if (step.target == null)
            {
                AdvanceMovementStep(phase);
                return;
            }

            movementStepTimer += dt;
            float t = Mathf.Clamp01(movementStepTimer / Mathf.Max(step.duration, 0.0001f));
            float eased = ApplyEase(t, step.easeType);

            transform.position = Vector3.LerpUnclamped(movementSegmentStartPosition, step.target.position, eased);

            if (t >= 1f)
            {
                AdvanceMovementStep(phase);
            }
        }

        private void AdvanceMovementStep(BossPhaseData phase)
        {
            movementSegmentStartPosition = transform.position;
            currentMovementStepIndex++;
            movementStepTimer = 0f;

            if (currentMovementStepIndex >= phase.movementPath.Count)
            {
                if (phase.loopMovement)
                {
                    currentMovementStepIndex = 0;
                }
                else
                {
                    currentMovementStepIndex = -1; // เดินสุดทางแล้วหยุด
                }
            }
        }

        private void UpdatePhaseTransition(float dt)
        {
            phaseTransitionTimer += dt;

            // เดินไปยังจุดเริ่มต้นของ Phase ถัดไป (ถ้ามีการตั้งเป้าหมายไว้)
            if (currentTransitionTarget != null)
            {
                float t = Mathf.Clamp01(phaseTransitionTimer / Mathf.Max(currentTransitionDuration, 0.0001f));
                float eased = ApplyEase(t, currentTransitionEase);
                transform.position = Vector3.LerpUnclamped(transitionStartPosition, currentTransitionTarget.position, eased);
            }

            if (phaseTransitionTimer >= currentTransitionDuration)
                StartPhase(CurrentPhaseIndex + 1);
        }

        private void UpdateDefeated(float dt)
        {
            blinkTimer += dt;
            if (blinkTimer < blinkInterval)
                return;

            blinkTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (bodySprite != null)
                bodySprite.color = blinkIsColored ? blinkColor : originalColor;

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

        private void StartPhase(int index)
        {
            if (index < 0 || index >= phases.Count)
            {
                BeginDefeated();
                return;
            }

            CurrentPhaseIndex = index;
            BossPhaseData phase = phases[CurrentPhaseIndex];

            phase.currentHealth = phase.phaseHealth;
            phase.phaseTimer = 0f;

            if (phase.executor == null)
                phase.executor = new BulletPatternV2Executor(phase.patterns, transform, () => playerTransform);
            else
                phase.executor.ResetAllGroups();

            // Set up movement for this phase
            canShootNow = true;
            if (phase.movementPath != null && phase.movementPath.Count > 0)
            {
                currentMovementStepIndex = 0;
                movementStepTimer = 0f;
                movementSegmentStartPosition = transform.position;
                canShootNow = phase.movementPath[0].allowShoot;
            }
            else
            {
                currentMovementStepIndex = -1;
            }

            CurrentState = BossState.PhaseActive;
            OnPhaseStarted?.Invoke(CurrentPhaseIndex);
        }

        private void EndCurrentPhase(bool captured)
        {
            BossPhaseData phase = CurrentPhase;
            if (phase == null)
                return;

            OnPhaseCleared?.Invoke(CurrentPhaseIndex, phase.isSpellCard && captured);

            bool isLastPhase = CurrentPhaseIndex >= phases.Count - 1;

            if (isLastPhase)
            {
                BeginDefeated();
            }
            else
            {
                CurrentState = BossState.PhaseTransition;
                phaseTransitionTimer = 0f;
                transitionStartPosition = transform.position;

                BossPhaseData nextPhase = phases[CurrentPhaseIndex + 1];
                currentTransitionDuration = nextPhase.transitionDuration > 0f ? nextPhase.transitionDuration : phaseTransitionDuration;
                currentTransitionTarget = nextPhase.transitionTargetPoint;
                currentTransitionEase = nextPhase.transitionEase;

                if (clearBulletsOnPhaseTransition)
                    ClearAllEnemyBulletsOnScreen();
            }
        }

        private void BeginDefeated()
        {
            CurrentState = BossState.Defeated;

            if (hitCollider != null)
                hitCollider.enabled = false;

            if (clearBulletsOnPhaseTransition)
                ClearAllEnemyBulletsOnScreen();

            blinkTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(scoreValueOnDefeat);

            OnBossDefeated?.Invoke();
        }

        private void ClearAllEnemyBulletsOnScreen()
        {
            if (PoolManager.Instance == null)
                return;

            EnemyBulletV2[] bullets = FindObjectsOfType<EnemyBulletV2>();
            for (int i = 0; i < bullets.Length; i++)
                PoolManager.Instance.Return(bullets[i].gameObject);
        }

        public void TakeDamage(int amount, GameObject attacker = null)
        {
            if (IsInvulnerable)
                return;

            BossPhaseData phase = CurrentPhase;
            if (phase == null)
                return;

            phase.currentHealth -= amount;

            if (phase.currentHealth <= 0)
            {
                phase.currentHealth = 0;
                EndCurrentPhase(captured: false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (CurrentState == BossState.Defeated)
                return;

            if (!other.CompareTag("Player"))
                return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
                player.Die();
        }

        private static float ApplyEase(float t, EaseType easeType)
        {
            t = Mathf.Clamp01(t);
            switch (easeType)
            {
                case EaseType.EaseIn: return t * t;
                case EaseType.EaseOut: return 1f - (1f - t) * (1f - t);
                case EaseType.EaseInOut: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default: return t;
            }
        }
    }
}