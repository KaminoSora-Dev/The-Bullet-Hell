using System;
using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;
using GameSystem;
using EnemySystem;   // ใช้ MovementStep / EaseType / EnemyShooterV1 ของเดิม ไม่ต้องสร้างซ้ำ
using CombatSystem;

namespace BossSystem
{
    /// <summary>
    /// ประเภทของเฟส:
    /// NonSpell    = เฟสธรรมดา ไม่มีชื่อ ไม่มี timer ไม่มีโบนัส ตายเมื่อ HP หมดอย่างเดียว
    /// SignatureSpellCard = เฟสลายเซ็นเนเจอร์ มีชื่อ, มี timer นับถอยหลัง, ได้โบนัสคะแนนถ้าฆ่า HP หมดก่อน timer หมด
    /// </summary>
    public enum BossPhaseType
    {
        NonSpell,
        SignatureSpellCard
    }

    /// <summary>
    /// สถานะการทำงานหลักของ Boss ทั้งตัว - คุมว่าตอนนี้ Update() ควรทำอะไร และควรโดนดาเมจ/ชนได้ไหม
    /// </summary>
    public enum BossState
    {
        Entering,          // กำลังเดินเข้าฉาก (entrancePath) - อมตะ, ยังไม่ยิง
        RepositionMoving,  // กำลังเดินไปจุดใหม่ก่อนเริ่มเฟส (repositionPath ของเฟสนั้น) - อมตะ, ยังไม่ยิง
        Fighting,          // กำลังต่อสู้จริงในเฟสปัจจุบัน - โดนดาเมจได้, ยิงตาม activeShooters ของเฟส, นับ timer ถ้าเป็น SignatureSpellCard
        Dying,             // กำลังเล่น death blink หลังเฟสสุดท้ายจบ
        Defeated           // ตายสมบูรณ์แล้ว (เผื่อระบบอื่นเช็คสถานะได้)
    }

    /// <summary>
    /// 1 เฟสของ Boss - ทุกอย่างที่ต้องตั้งค่าต่อเฟสอยู่ในนี้ทั้งหมด เพื่อให้แก้/เพิ่มเฟสใน Inspector ได้อิสระทีละเฟส
    /// แนวคิดเดียวกับ SpawnGroup/ShootGroup ที่มีอยู่แล้วในโปรเจกต์ (แต่ละรายการมี timeline/ค่าของตัวเอง)
    /// </summary>
    [System.Serializable]
    public class BossPhase
    {
        [Header("ข้อมูลทั่วไป")]
        [Tooltip("ชื่อเฟส/ชื่อลาย - ใช้โชว์บน SignatureSpellCardUI ตอนเป็นเฟส SignatureSpellCard")]
        public string phaseName = "New Phase";
        public BossPhaseType phaseType = BossPhaseType.SignatureSpellCard;
        [Tooltip("HP ของเฟสนี้โดยเฉพาะ - รีเซ็ตเต็มใหม่ทุกครั้งที่ขึ้นเฟสนี้ (ไม่ใช่ HP รวมทั้งไฟต์)")]
        public int healthAmount = 500;

        [Header("Signature Spell Card Timer (ใช้เฉพาะ Phase Type = SignatureSpellCard)")]
        [Tooltip("เวลาจำกัดของเฟสนี้ (วินาที) - ถ้าฆ่าไม่ทัน จะบังคับข้ามไปเฟสถัดไปเองโดยไม่ได้โบนัส")]
        public float timeLimit = 30f;
        [Tooltip("คะแนนโบนัสที่ได้ ถ้าฆ่า HP ของเฟสนี้หมดได้ก่อน timer จะหมด (ตั้งเป็น 0 ได้ถ้าไม่อยากให้โบนัส)")]
        public int clearBonusScore = 1000;

        [Header("ชุดยิงเฉพาะเฟสนี้")]
        [Tooltip("ลาก component EnemyShooterV1 (ที่ติดอยู่บน GameObject Boss ตัวนี้เอง) ที่อยากให้ทำงาน \"เฉพาะตอนเฟสนี้\" มาใส่ " +
                 "component ตัวอื่นที่ไม่ได้ใส่ในนี้จะถูกปิดอัตโนมัติระหว่างเฟสนี้ - วิธีนี้ทำให้ Boss ยิงคนละแพทเทิร์นได้ทุกเฟส " +
                 "โดยไม่ต้องแก้ EnemyShooterV1.cs เลย (แค่ enable/disable component ทั้งตัว)")]
        public List<EnemyShooterV1> activeShooters = new List<EnemyShooterV1>();

        [Header("Reposition Movement ก่อนเริ่มเฟส (ไม่บังคับ)")]
        [Tooltip("ถ้าอยากให้ Boss เดินไปจุดใหม่ก่อนเริ่มยิงเฟสนี้ (เช่น ขยับไปกลางจอก่อนเริ่ม spell card) ใส่ลิสต์ตรงนี้ " +
                 "ปล่อยว่างไว้ได้ถ้าอยากให้เริ่มยิงเฟสนี้จากตำแหน่งปัจจุบันทันที ใช้ MovementStep แบบเดียวกับ EnemyV1")]
        public List<MovementStep> repositionPath = new List<MovementStep>();

        // true เฉพาะตอนเฟสนี้ "หมดเวลา" จริงๆ (ไม่ใช่โดนฆ่าทัน) - ใช้ตัดสินว่าจะให้โบนัสไหม
        [System.NonSerialized] public bool timeoutTriggered;
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class BossV1 : MonoBehaviour, IDamageable
    {
        [Header("ข้อมูลทั่วไป (เผื่อระบบ Dialogue/Cutscene ในอนาคตอ่านไปโชว์ชื่อ)")]
        public string bossName = "Boss";

        [Header("Phases")]
        [Tooltip("เรียงตามลำดับที่จะเล่น - จบเฟสสุดท้ายในลิสต์นี้ = Boss ตายจริง")]
        [SerializeField] private List<BossPhase> phases = new List<BossPhase>();

        [Header("Entrance Movement (ไม่บังคับ)")]
        [Tooltip("เส้นทางเดินเข้าฉากก่อนเริ่มเฟสแรก - อมตะและยังไม่ยิงระหว่างนี้ ปล่อยว่างได้ถ้าอยากให้เริ่มสู้ทันทีจากตำแหน่งที่วางไว้")]
        [SerializeField] private List<MovementStep> entrancePath = new List<MovementStep>();

        [Header("Death Visual")]
        [SerializeField] private int blinkCount = 5;
        [SerializeField] private float blinkInterval = 0.08f;
        [SerializeField] private Color blinkColor = Color.white;
        [Tooltip("คะแนนก้อนสุดท้ายตอน Boss ตายจริง (แยกจาก clearBonusScore ของแต่ละเฟส) - ตั้งเป็น 0 ได้ถ้าไม่อยากให้เพิ่ม")]
        [SerializeField] private int finalDefeatScore = 5000;

        [Header("Fragment Drop ตอนตายจริง")]
        [SerializeField] private bool dropsFragment = true;
        [SerializeField] private GameObject fragmentPrefab;
        [SerializeField] private int fragmentDropCount = 5;

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Collider ของตัว Boss ต้องติ๊ก isTrigger = true เพื่อให้โดน PlayerBullet ได้")]
        [SerializeField] private Collider2D hitCollider;

        private Color originalColor;
        private Rigidbody2D rb;
        private EnemyShooterV1[] allShooters;

        private BossState state = BossState.Entering;
        private int currentPhaseIndex = -1;
        private int currentPhaseHealth;
        private float phaseTimer;

        // --- movement state ใช้ร่วมกันทั้ง entrance และ reposition (ทีละเส้นทางเท่านั้น ไม่เล่นพร้อมกัน) ---
        private List<MovementStep> activeMovementPath;
        private int moveStepIndex;
        private float moveStepTimer;
        private Vector3 moveSegmentStart;
        private Action onMovementPathComplete;

        // --- death blink state ---
        private float blinkTimer;
        private int blinksDone;
        private bool blinkIsColored;

        public BossState State => state;
        public BossPhase CurrentPhase => (currentPhaseIndex >= 0 && currentPhaseIndex < phases.Count) ? phases[currentPhaseIndex] : null;
        public int CurrentPhaseIndex => currentPhaseIndex;
        public int PhaseCount => phases.Count;

        /// <summary>เข้าฉากเสร็จแล้ว (entrance path จบ) - ใช้ hook ระบบ dialogue/cutscene ในอนาคตได้ตรงนี้</summary>
        public event Action OnBossAppeared;
        /// <summary>เริ่มเฟสใหม่ - ส่ง BossPhase มาให้ UI/ระบบอื่นอ่านชื่อ/timeLimit/phaseType เอง</summary>
        public event Action<BossPhase> OnPhaseStarted;
        /// <summary>จบเฟส (ไม่ว่าจะฆ่าทันหรือหมดเวลา) - เช็ค phase.timeoutTriggered เพื่อรู้ว่าจบแบบไหน</summary>
        public event Action<BossPhase> OnPhaseEnded;
        /// <summary>HP เฟสปัจจุบันเปลี่ยน - ส่ง (current, max) ให้ BossHealthBarUI ฟัง</summary>
        public event Action<int, int> OnPhaseHealthChanged;
        /// <summary>เวลาที่เหลือของเฟส SignatureSpellCard เปลี่ยนทุกเฟรม - ส่ง (remaining, total) ให้ SignatureSpellCardUI ฟัง</summary>
        public event Action<float, float> OnPhaseTimerTick;
        /// <summary>Boss ตายจริง (จบทุกเฟสแล้ว) - ใช้แจ้ง StageSpawner/GameManager ว่าจบฉากได้</summary>
        public event Action OnBossDefeated;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            // Kinematic เหตุผลเดียวกับ EnemyV1/EnemyBulletV1: ต้องการแค่ Rigidbody2D ไว้ให้ trigger event ทำงานตอนชน Player
            rb.bodyType = RigidbodyType2D.Kinematic;

            if (hitCollider != null) hitCollider.isTrigger = true;
            if (bodySprite != null) originalColor = bodySprite.color;

            // เก็บ EnemyShooterV1 ทุกตัวที่ติดอยู่บน GameObject นี้ไว้ล่วงหน้า เพื่อใช้ enable/disable ทีละเฟส
            allShooters = GetComponents<EnemyShooterV1>();
        }

        void Start()
        {
            DisableAllShooters();
            SetHittable(false); // อมตะไว้ก่อนจนกว่าจะเข้าฉากเสร็จ

            if (entrancePath != null && entrancePath.Count > 0)
            {
                state = BossState.Entering;
                BeginMovementPath(entrancePath, HandleEntranceComplete);
            }
            else
            {
                HandleEntranceComplete();
            }
        }

        private void HandleEntranceComplete()
        {
            SetHittable(true);
            OnBossAppeared?.Invoke();

            if (phases.Count == 0)
            {
                Debug.LogWarning($"BossV1 ({name}): ไม่มี Phase ตั้งไว้เลย - Boss จะไม่ทำอะไรทั้งนั้น");
                return;
            }

            StartPhase(0);
        }

        void Update()
        {
            switch (state)
            {
                case BossState.Entering:
                case BossState.RepositionMoving:
                    UpdateActiveMovement();
                    break;

                case BossState.Fighting:
                    UpdatePhaseTimer();
                    break;

                case BossState.Dying:
                    UpdateDeathBlink();
                    break;
            }
        }

        // ============================== Phase Flow ==============================

        private void StartPhase(int index)
        {
            currentPhaseIndex = index;
            BossPhase phase = phases[index];

            currentPhaseHealth = Mathf.Max(phase.healthAmount, 1);
            phaseTimer = 0f;
            phase.timeoutTriggered = false;

            DisableAllShooters();

            OnPhaseStarted?.Invoke(phase);
            OnPhaseHealthChanged?.Invoke(currentPhaseHealth, phase.healthAmount);

            if (phase.repositionPath != null && phase.repositionPath.Count > 0)
            {
                state = BossState.RepositionMoving;
                BeginMovementPath(phase.repositionPath, BeginFightingCurrentPhase);
            }
            else
            {
                BeginFightingCurrentPhase();
            }
        }

        private void BeginFightingCurrentPhase()
        {
            EnableShooters(phases[currentPhaseIndex].activeShooters);
            state = BossState.Fighting;
        }

        private void UpdatePhaseTimer()
        {
            BossPhase phase = phases[currentPhaseIndex];
            if (phase.phaseType != BossPhaseType.SignatureSpellCard) return; // เฟส NonSpell ไม่มี timer

            phaseTimer += Time.deltaTime;
            float remaining = Mathf.Max(phase.timeLimit - phaseTimer, 0f);
            OnPhaseTimerTick?.Invoke(remaining, phase.timeLimit);

            if (remaining <= 0f)
            {
                phase.timeoutTriggered = true;
                EndCurrentPhase(clearedByDamage: false);
            }
        }

        /// <summary>
        /// จบเฟสปัจจุบัน 1 ครั้ง แล้วไปเฟสถัดไป หรือเข้าสู่ death sequence ถ้าเป็นเฟสสุดท้าย
        /// clearedByDamage = true เฉพาะตอนโดนยิงจน HP หมด (ไม่ใช่ timer หมด) - ใช้ตัดสินเรื่องโบนัสร่วมกับ timeoutTriggered
        /// </summary>
        private void EndCurrentPhase(bool clearedByDamage)
        {
            BossPhase phase = phases[currentPhaseIndex];

            // ให้โบนัสเฉพาะ: เฟสเป็น SignatureSpellCard, ฆ่าทันด้วยดาเมจจริง, และยังไม่ทัน timeout ก่อน
            bool earnedBonus = clearedByDamage
                && phase.phaseType == BossPhaseType.SignatureSpellCard
                && !phase.timeoutTriggered;

            if (earnedBonus && phase.clearBonusScore != 0 && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(phase.clearBonusScore);
            }

            DisableAllShooters();
            OnPhaseEnded?.Invoke(phase);

            int nextIndex = currentPhaseIndex + 1;
            if (nextIndex >= phases.Count)
            {
                BeginDeathSequence();
            }
            else
            {
                StartPhase(nextIndex);
            }
        }

        // ============================== Damage / IDamageable ==============================

        public void TakeDamage(int amount, GameObject attacker = null)
        {
            // โดนดาเมจได้เฉพาะตอนกำลังสู้จริงเท่านั้น (อมตะตอนเข้าฉาก/เปลี่ยนเฟส/กำลังตายอยู่แล้ว)
            if (state != BossState.Fighting) return;

            currentPhaseHealth -= amount;
            BossPhase phase = phases[currentPhaseIndex];
            OnPhaseHealthChanged?.Invoke(Mathf.Max(currentPhaseHealth, 0), phase.healthAmount);

            if (currentPhaseHealth <= 0)
            {
                EndCurrentPhase(clearedByDamage: true);
            }
        }

        // ============================== Death Sequence ==============================

        private void BeginDeathSequence()
        {
            state = BossState.Dying;
            SetHittable(false);
            DisableAllShooters();

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
                    FinalizeDefeat();
                }
            }
        }

        private void FinalizeDefeat()
        {
            state = BossState.Defeated;

            if (finalDefeatScore != 0 && ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(finalDefeatScore);

            if (dropsFragment && fragmentPrefab != null && PoolManager.Instance != null)
            {
                for (int i = 0; i < fragmentDropCount; i++)
                {
                    PoolManager.Instance.Get(fragmentPrefab, transform.position, Quaternion.identity);
                }
            }

            OnBossDefeated?.Invoke();

            // บอสปกติไม่ใช้ซ้ำ (ต่างจากกระสุน/ศัตรูตัวเล็กที่วน pool) - ทำลายทิ้งไปเลย
            // ถ้าจะทำ Boss หลายตัวจาก prefab เดียวกันแล้วอยากคืน pool แทน เปลี่ยนบรรทัดนี้เป็น PoolManager.Instance.Return(gameObject)
            Destroy(gameObject);
        }

        // ============================== Shooter Enable/Disable ==============================

        private void DisableAllShooters()
        {
            for (int i = 0; i < allShooters.Length; i++)
            {
                allShooters[i].enabled = false;
            }
        }

        private void EnableShooters(List<EnemyShooterV1> shootersToEnable)
        {
            for (int i = 0; i < shootersToEnable.Count; i++)
            {
                EnemyShooterV1 shooter = shootersToEnable[i];
                if (shooter == null) continue;

                shooter.enabled = true;
                // รีเซ็ตทุก ShootGroup ในตัวนี้ให้เริ่มเล่นจาก Burst แรกใหม่ ทุกครั้งที่เฟสเปิดใช้งานมัน
                shooter.ResetAllGroups();
            }
        }

        // ============================== Movement (ใช้ MovementStep/EaseType เดิมจาก EnemySystem) ==============================

        private void BeginMovementPath(List<MovementStep> path, Action onComplete)
        {
            activeMovementPath = path;
            moveStepIndex = 0;
            moveStepTimer = 0f;
            moveSegmentStart = transform.position;
            onMovementPathComplete = onComplete;
        }

        private void UpdateActiveMovement()
        {
            if (activeMovementPath == null || moveStepIndex >= activeMovementPath.Count)
            {
                CompleteMovementPath();
                return;
            }

            MovementStep step = activeMovementPath[moveStepIndex];
            if (step.target == null)
            {
                AdvanceMovementStep();
                return;
            }

            moveStepTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveStepTimer / Mathf.Max(step.duration, 0.0001f));
            float easedT = ApplyEase(t, step.easeType);

            transform.position = Vector3.LerpUnclamped(moveSegmentStart, step.target.position, easedT);

            if (t >= 1f)
            {
                AdvanceMovementStep();
            }
        }

        private void AdvanceMovementStep()
        {
            moveSegmentStart = transform.position;
            moveStepIndex++;
            moveStepTimer = 0f;

            if (moveStepIndex >= activeMovementPath.Count)
            {
                CompleteMovementPath();
            }
        }

        private void CompleteMovementPath()
        {
            Action callback = onMovementPathComplete;
            activeMovementPath = null;
            onMovementPathComplete = null;
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
                default:
                    return t;
            }
        }

        // ============================== Misc ==============================

        private void SetHittable(bool value)
        {
            if (hitCollider != null) hitCollider.enabled = value;
        }

        /// <summary>ชนตัว Boss โดยตรง (ไม่ใช่กระสุน) ก็ต้องทำให้ Player ตายด้วยเช่นกัน เหมือน EnemyV1</summary>
        void OnTriggerEnter2D(Collider2D other)
        {
            if (state == BossState.Dying || state == BossState.Defeated) return;
            if (!other.CompareTag("Player")) return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
            {
                player.Die();
            }
        }
    }
}