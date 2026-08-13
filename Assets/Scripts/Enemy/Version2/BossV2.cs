using System;
using System.Collections.Generic;
using UnityEngine;
using GameFlowSystem;
using PlayerSystem;
using PoolingSystem;
using GameSystem;
using CombatSystem;
using EnemySystem;

namespace BossSystem
{
    [Serializable]
    public class BossPhaseData
    {
        [Tooltip("ชื่อ Phase - ใช้แค่ให้อ่านง่ายใน Inspector เท่านั้น")]
        public string phaseName = "Phase 1";

        [Header("Health")]
        [Min(1)]
        public int phaseHealth = 100;

        [Header("Spell Card")]
        [Tooltip(
            "ติ๊กถ้าอยากให้ Phase นี้นับเป็นสถิติ Spell Card Capture " +
            "(ยิง OnAnySpellCardResult และธง spell card ใน OnPhaseCleared)\n" +
            "ไม่ติ๊กก็ยังตั้งชื่อ Banner และ Time Limit ได้ปกติ แค่ไม่ถูกนับสถิติ Capture"
        )]
        public bool isSpellCard = false;

        [Tooltip(
            "ชื่อที่โชว์เป็น Banner ตอนเข้า Phase นี้ (ผ่าน OnAnySpellCardStarted)\n" +
            "เว้นว่างไว้ = ไม่โชว์ Banner ไม่ว่าจะติ๊ก Is Spell Card หรือไม่"
        )]
        public string spellCardName = "";

        [Tooltip(
            "เวลาจำกัดของ Phase นี้ (วินาที) - หมดเวลาแล้วยังไม่ตายจะถือว่าจบ Phase แบบ Timeout " +
            "ทำงานไม่ว่าจะติ๊ก Is Spell Card หรือไม่\n0 = ไม่จำกัดเวลา (ต้องตี HP ให้หมดอย่างเดียว)"
        )]
        [Min(0f)]
        public float spellCardTimeLimit = 0f;

        [Header("Transition")]
        public Transform transitionTargetPoint;

        [Min(0f)]
        public float transitionDuration = 0f;

        public EaseType transitionEase = EaseType.EaseInOut;

        [Header("Movement")]
        public List<MovementStep> movementPath =
            new List<MovementStep>();

        public bool loopMovement = true;

        [Header("Bullet Pattern")]
        public List<BulletPatternGroup> patterns =
            new List<BulletPatternGroup>();

        [NonSerialized]
        public int currentHealth;

        [NonSerialized]
        public float phaseTimer;

        [NonSerialized]
        public BulletPatternV2Executor executor;

        public void ResetRuntime()
        {
            currentHealth = phaseHealth;
            phaseTimer = 0f;
            executor?.ResetAllGroups();
        }
    }

    /// <summary>
    /// Configuration สำหรับ Item Drop ของ Boss
    ///
    /// itemPrefab ควรเป็น prefab ที่มี ItemDropPickup และใช้ Tag:
    /// Life / Spell / Fragment
    /// </summary>
    [Serializable]
    public class BossItemDropData
    {
        [Tooltip(
            "Prefab ของ ItemDropPickup ที่จะดรอป\n" +
            "Tag ของ prefab ต้องเป็น Life, Spell หรือ Fragment"
        )]
        public GameObject itemPrefab;

        [Tooltip("จำนวน Item ที่ดรอปเมื่อ Boss ถูกกำจัดครบทุก Phase")]
        [Min(1)]
        public int count = 1;
    }

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

        [Header("Identity")]
        [Tooltip(
            "ชื่อบอสที่จะโชว์บน UI ตอนเจอบอส " +
            "(BossNameDisplayUI ฟัง OnAnyBossNameReveal)"
        )]
        [SerializeField] private string bossName = "Boss";

        public string BossName => bossName;

        [Header("Entry")]
        [SerializeField] private Transform entryTargetPoint;
        [SerializeField] private float entryDuration = 2f;
        [SerializeField] private EaseType entryEase = EaseType.EaseOut;

        [Header("Phases")]
        [SerializeField] private List<BossPhaseData> phases =
            new List<BossPhaseData>();

        [Header("Phase Transition (Default)")]
        [SerializeField] private float phaseTransitionDuration = 1.2f;
        [SerializeField] private bool clearBulletsOnPhaseTransition = true;

        [Header("Death Visual")]
        [SerializeField] private int blinkCount = 6;
        [SerializeField] private float blinkInterval = 0.08f;
        [SerializeField] private Color blinkColor = Color.white;

        [Header("Score")]
        [SerializeField] private int scoreValueOnDefeat = 1000;

        [Header("Item Drop")]
        [Tooltip(
            "กำหนด Item ที่ Boss จะดรอปหลังจบทุก Phase\n" +
            "ใช้ Life / Spell / Fragment ItemDropPickup ได้\n" +
            "ปล่อย Item Prefab ว่างไว้ = ไม่ดรอป"
        )]
        [SerializeField] private BossItemDropData itemDrop;

        [Header("Scene Flow")]
        [Tooltip("ติ๊กเพื่อให้ Boss ตัวนี้ เมื่อตายครบทุก Phase แล้ว ไป Scene ถัดไปอัตโนมัติ")]
        [SerializeField] private bool loadNextSceneOnDefeat = false;

        [Tooltip("ใช้กับ Boss ใน Scene สุดท้าย ถ้าติ๊กจะ Fade จอดำเพื่อจบเกมแทนการไป Scene ถัดไป")]
        [SerializeField] private bool fadeOnFinalScene = false;

        public bool LoadNextSceneOnDefeat => loadNextSceneOnDefeat;
        public bool FadeOnFinalScene => fadeOnFinalScene;

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;
        [SerializeField] private Collider2D hitCollider;

        private Rigidbody2D rb;
        private Transform playerTransform;
        private Color originalColor;

        private Vector3 entryStartPosition;
        private float entryTimer;

        private float phaseTransitionTimer;
        private float currentTransitionDuration;
        private Vector3 transitionStartPosition;
        private Transform currentTransitionTarget;
        private EaseType currentTransitionEase;

        private int currentMovementStepIndex = -1;
        private float movementStepTimer;
        private Vector3 movementSegmentStartPosition;
        private bool canShootNow = true;

        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        public BossState CurrentState { get; private set; } =
            BossState.Inactive;

        public int CurrentPhaseIndex { get; private set; } = -1;

        public int PhaseCount =>
            phases != null ? phases.Count : 0;

        public bool IsInvulnerable =>
            CurrentState != BossState.PhaseActive;

        public BossPhaseData CurrentPhase =>
            (CurrentPhaseIndex >= 0 &&
             CurrentPhaseIndex < phases.Count)
                ? phases[CurrentPhaseIndex]
                : null;

        public event Action<int> OnPhaseStarted;
        public event Action<int, bool> OnPhaseCleared;
        public event Action OnBossDefeated;

        public static event Action<string> OnAnyBossNameReveal;
        public static event Action OnAnyBossNameHide;
        public static event Action<string> OnAnySpellCardStarted;
        public static event Action OnAnyBossActivated;
        public static event Action OnAnyBossDefeatedAny;

        /// <summary>
        /// ยิงทุกครั้งที่ Phase ที่เป็น Spell Card จบลง
        /// true = Capture สำเร็จ
        /// false = ไม่สำเร็จ
        /// </summary>
        public static event Action<bool> OnAnySpellCardResult;

        /// <summary>
        /// ให้ BossAnimationController รับช่วง Death Visual แทนได้
        /// </summary>
        public bool SuppressBuiltInDeathVisual { get; set; }

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
            SuppressBuiltInDeathVisual = false;

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
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            playerTransform =
                playerObject != null
                    ? playerObject.transform
                    : null;
        }

        public void Activate()
        {
            if (CurrentState != BossState.Inactive)
                return;

            entryStartPosition = transform.position;
            entryTimer = 0f;
            CurrentState = BossState.Entering;

            OnAnyBossActivated?.Invoke();
            OnAnyBossNameReveal?.Invoke(bossName);
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

            float t = Mathf.Clamp01(
                entryTimer / Mathf.Max(entryDuration, 0.0001f)
            );

            float eased = ApplyEase(t, entryEase);

            transform.position = Vector3.LerpUnclamped(
                entryStartPosition,
                entryTargetPoint.position,
                eased
            );

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

            if (currentMovementStepIndex >= 0 &&
                currentMovementStepIndex < phase.movementPath.Count)
            {
                currentAllowShoot =
                    phase.movementPath[currentMovementStepIndex].allowShoot;
            }

            if (currentAllowShoot && !canShootNow)
                phase.executor?.ResetAllGroups();

            canShootNow = currentAllowShoot;

            if (canShootNow)
                phase.executor?.Tick(dt);

            // Timer ทำงานตาม spellCardTimeLimit ล้วนๆ ไม่ต้องพึ่ง isSpellCard แล้ว
            // (กันเคส Phase ธรรมดาที่ Pattern ค้าง ไม่จบ Phase สักที ก็ยังตั้งเวลาบังคับจบได้)
            if (phase.spellCardTimeLimit > 0f)
            {
                phase.phaseTimer += dt;

                if (phase.phaseTimer >= phase.spellCardTimeLimit)
                    EndCurrentPhase(captured: true);
            }
        }

        private void UpdatePhaseMovement(
            float dt,
            BossPhaseData phase)
        {
            if (currentMovementStepIndex < 0 ||
                currentMovementStepIndex >= phase.movementPath.Count)
            {
                return;
            }

            MovementStep step =
                phase.movementPath[currentMovementStepIndex];

            if (step.target == null)
            {
                AdvanceMovementStep(phase);
                return;
            }

            movementStepTimer += dt;

            float t = Mathf.Clamp01(
                movementStepTimer / Mathf.Max(step.duration, 0.0001f)
            );

            float eased = ApplyEase(t, step.easeType);

            transform.position = Vector3.LerpUnclamped(
                movementSegmentStartPosition,
                step.target.position,
                eased
            );

            if (t >= 1f)
                AdvanceMovementStep(phase);
        }

        private void AdvanceMovementStep(BossPhaseData phase)
        {
            movementSegmentStartPosition = transform.position;
            currentMovementStepIndex++;
            movementStepTimer = 0f;

            if (currentMovementStepIndex >= phase.movementPath.Count)
            {
                currentMovementStepIndex =
                    phase.loopMovement ? 0 : -1;
            }
        }

        private void UpdatePhaseTransition(float dt)
        {
            phaseTransitionTimer += dt;

            if (currentTransitionTarget != null)
            {
                float t = Mathf.Clamp01(
                    phaseTransitionTimer /
                    Mathf.Max(currentTransitionDuration, 0.0001f)
                );

                float eased =
                    ApplyEase(t, currentTransitionEase);

                transform.position = Vector3.LerpUnclamped(
                    transitionStartPosition,
                    currentTransitionTarget.position,
                    eased
                );
            }

            if (phaseTransitionTimer >= currentTransitionDuration)
                StartPhase(CurrentPhaseIndex + 1);
        }

        private void UpdateDefeated(float dt)
        {
            if (SuppressBuiltInDeathVisual)
                return;

            blinkTimer += dt;

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

        private void StartPhase(int index)
        {
            if (index < 0 || index >= phases.Count)
            {
                BeginDefeated();
                return;
            }

            CurrentPhaseIndex = index;

            BossPhaseData phase =
                phases[CurrentPhaseIndex];

            phase.currentHealth = phase.phaseHealth;
            phase.phaseTimer = 0f;

            if (phase.executor == null)
            {
                phase.executor =
                    new BulletPatternV2Executor(
                        phase.patterns,
                        transform,
                        () => playerTransform
                    );
            }
            else
            {
                phase.executor.ResetAllGroups();
            }

            canShootNow = true;

            if (phase.movementPath != null &&
                phase.movementPath.Count > 0)
            {
                currentMovementStepIndex = 0;
                movementStepTimer = 0f;
                movementSegmentStartPosition =
                    transform.position;

                canShootNow =
                    phase.movementPath[0].allowShoot;
            }
            else
            {
                currentMovementStepIndex = -1;
            }

            CurrentState = BossState.PhaseActive;

            OnPhaseStarted?.Invoke(CurrentPhaseIndex);

            // Banner ทำงานตามชื่อ ไม่ใช่ตาม isSpellCard แล้ว
            // isSpellCard เก็บไว้คุมแค่สถิติ Capture (ดู EndCurrentPhase)
            if (!string.IsNullOrWhiteSpace(phase.spellCardName))
                OnAnySpellCardStarted?.Invoke(
                    phase.spellCardName
                );
        }

        private void EndCurrentPhase(bool captured)
        {
            BossPhaseData phase = CurrentPhase;

            if (phase == null)
                return;

            OnPhaseCleared?.Invoke(
                CurrentPhaseIndex,
                phase.isSpellCard && captured
            );

            if (phase.isSpellCard)
                OnAnySpellCardResult?.Invoke(captured);

            bool isLastPhase =
                CurrentPhaseIndex >= phases.Count - 1;

            if (isLastPhase)
            {
                BeginDefeated();
            }
            else
            {
                CurrentState = BossState.PhaseTransition;
                phaseTransitionTimer = 0f;
                transitionStartPosition = transform.position;

                BossPhaseData nextPhase =
                    phases[CurrentPhaseIndex + 1];

                currentTransitionDuration =
                    nextPhase.transitionDuration > 0f
                        ? nextPhase.transitionDuration
                        : phaseTransitionDuration;

                currentTransitionTarget =
                    nextPhase.transitionTargetPoint;

                currentTransitionEase =
                    nextPhase.transitionEase;

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
                ScoreManager.Instance.AddScore(
                    scoreValueOnDefeat
                );

            // Boss ดรอป Item ครั้งเดียวหลังจบทุก Phase
            SpawnItemDrop();

            OnBossDefeated?.Invoke();

            OnAnyBossDefeatedAny?.Invoke();
            OnAnyBossNameHide?.Invoke();

            // Boss ไม่โหลด Scene เองโดยตรง แต่แจ้ง GameFlowManager
            // เพื่อให้ Loading / Final Fade ถูกจัดการจากจุดกลางเดียว
            if (loadNextSceneOnDefeat && GameFlowManager.Instance != null)
                GameFlowManager.Instance.HandleBossDefeated(fadeOnFinalScene);
        }

        /// <summary>
        /// Spawn ItemDropPickup ผ่าน Pool
        /// Boss จะไม่สนใจว่า Item เป็น Life / Spell / Fragment
        /// เพราะ ItemDropPickup อ่าน Tag ของ prefab เอง
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

        private void ClearAllEnemyBulletsOnScreen()
        {
            if (PoolManager.Instance == null)
                return;

            EnemyBulletV2[] bullets =
                FindObjectsOfType<EnemyBulletV2>();

            for (int i = 0; i < bullets.Length; i++)
                bullets[i].RequestDestroy();
        }

        public void TakeDamage(
            int amount,
            GameObject attacker = null)
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

            if (other.TryGetComponent<PlayerControllerV1>(
                out var player))
            {
                player.Die();
            }
        }

        private static float ApplyEase(
            float t,
            EaseType easeType)
        {
            t = Mathf.Clamp01(t);

            switch (easeType)
            {
                case EaseType.EaseIn:
                    return t * t;

                case EaseType.EaseOut:
                    return 1f - (1f - t) * (1f - t);

                case EaseType.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f -
                          Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default:
                    return t;
            }
        }
    }
}