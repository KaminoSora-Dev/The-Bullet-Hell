using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameSystem;
using EnemySystem;
using BossSystem;
using GameFlowSystem;

namespace PlayerSystem
{
    [Serializable]
    public class StageProgressRecord
    {
        public string stageId;
        public int clearCount;
        public int highScore;
    }

    /// <summary>
    /// Singleton ถาวร (DontDestroyOnLoad) - เก็บสถานะผู้เล่นที่ต้องคงอยู่ข้าม Scene
    /// รวมถึง Lives, Spells, Power และสถิติสะสม
    ///
    /// ชื่อ API เดิมและ API ที่ UI/PlayerController รุ่นเก่าใช้อยู่ถูกเก็บไว้
    /// เพื่อให้ระบบต่าง ๆ compile ร่วมกันได้โดยไม่ต้องแก้ทุก consumer พร้อมกัน
    /// </summary>
    public class PlayerStatsManager : MonoBehaviour
    {
        public static PlayerStatsManager Instance { get; private set; }

        [Header("ค่าเริ่มต้นตอนเริ่มเกมใหม่ (New Game)")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int startingBombs = 3;
        [SerializeField] private int startingPower = 0;

        [Header("Maximum Values")]
        [Tooltip("จำนวนชีวิตสูงสุดที่ผู้เล่นมีได้")]
        [Min(0)]
        [SerializeField] private int maxLives = 8;

        [Tooltip("จำนวน Spell/Bomb สูงสุดที่ผู้เล่นมีได้")]
        [Min(0)]
        [SerializeField] private int maxBombs = 8;

        [Tooltip("Power สูงสุดที่ Fragment สามารถเพิ่มได้")]
        [Min(0)]
        [SerializeField] private int maxPower = 128;

        [Header("Power Stage Thresholds (สำหรับระบบยิงหลายลูก - เพิ่มใหม่)")]
        [Tooltip("ค่า currentPower ขั้นต่ำที่ต้องถึงเพื่อขึ้น Power Stage 1,2,3,4,5 ตามลำดับ " +
                 "(index 0 = เกณฑ์ขึ้น Stage 1, index 1 = เกณฑ์ขึ้น Stage 2 ... ) ต้องเรียงค่าจากน้อยไปมาก " +
                 "ค่าเริ่มต้นแบ่งเท่า ๆ กันจาก maxPower เริ่มต้น (128) ปรับได้อิสระใน Inspector")]
        [SerializeField] private int[] powerStageThresholds = { 16, 32, 64, 96, 128 };

        private int currentLives;
        private int currentBombs;
        private int currentPower;
        private int currentGraze;
        private int currentPowerStage;

        private bool hasSavedPosition;
        private Vector3 savedPlayerPosition;

        private int totalEnemiesDefeated;
        private int totalBossesDefeated;
        private int totalSpellCardsCaptured;
        private int missCount;
        private int bombsUsedCount;

        private readonly List<StageProgressRecord> stageRecords = new List<StageProgressRecord>();

        public int CurrentLives => currentLives;
        public int CurrentBombs => currentBombs;

        public int CurrentPower => currentPower;
        public int CurrentGraze => currentGraze;

        // Compatibility names used by existing UI/debug code
        public int CurrentSpells => currentBombs;
        public int CurrentPowerLevel => currentPower;

        /// <summary>
        /// Power Stage แบบ Discrete (0-5) แปลงมาจาก currentPower ผ่าน powerStageThresholds
        /// ใช้โดย PlayerControllerV1 เพื่อตัดสินรูปแบบการยิง (จำนวนกระสุน/มุม/Homing)
        /// คนละตัวกับ CurrentPowerLevel ด้านบนซึ่งเป็น raw power value (compat เดิม) - ตั้งชื่อแยกกันเจตนา
        /// </summary>
        public int CurrentPowerStage => currentPowerStage;

        public int MaxLives => maxLives;
        public int MaxBombs => maxBombs;
        public int MaxPower => maxPower;

        public int TotalEnemiesDefeated => totalEnemiesDefeated;
        public int TotalBossesDefeated => totalBossesDefeated;
        public int TotalSpellCardsCaptured => totalSpellCardsCaptured;
        public int MissCount => missCount;
        public int BombsUsedCount => bombsUsedCount;

        public IReadOnlyList<StageProgressRecord> AllStageRecords => stageRecords;

        public event Action<int> OnLivesChanged;
        public event Action<int> OnBombsChanged;
        public event Action<int> OnPowerChanged;

        // Compatibility events for existing UI/debug scripts.
        public event Action<int> OnSpellsChanged;
        public event Action<int> OnPowerLevelChanged;
        public event Action<int> OnGrazeChanged;

        /// <summary>
        /// ยิงเฉพาะตอน Power Stage (0-5) เปลี่ยนค่าจริงเท่านั้น (ไม่ยิงทุกครั้งที่ currentPower ขยับ)
        /// เหมาะกับ UI ไอคอน Power Level หรือ PlayerControllerV1 ที่อยากรู้ตอนขึ้น Stage แบบ event-driven
        /// แทนที่จะอ่าน CurrentPowerStage ทุกเฟรม
        /// </summary>
        public event Action<int> OnPowerStageChanged;

        /// <summary>
        /// ยิงเมื่อชีวิตหมดและการตายครั้งนั้นทำให้เข้าสู่ Game Over
        /// </summary>
        public event Action OnGameOver;

#if UNITY_EDITOR
        /// <summary>
        /// เพิ่มใหม่: ป้องกันบัค Power Stage กระโดดข้ามหลาย Stage ทีเดียว
        /// สาเหตุที่พบบ่อย: เวลาลาก resize ขนาด array ใน Inspector Unity จะ copy ค่าตัวสุดท้ายไปใส่ช่องใหม่ให้อัตโนมัติ
        /// (เช่นได้ {16,16,16,16,16} หรือ {1,1,2,3,4} โดยไม่ตั้งใจ) พอมีค่าเท่ากัน/ไม่เรียงจากน้อยไปมาก
        /// ComputePowerStage() จะข้าม Stage กลางไปเลยตอน currentPower แตะค่านั้นพอดี
        /// ฟังก์ชันนี้บังคับให้เรียงจากน้อยไปมากเสมอ ถ้าเจอค่าซ้ำ/ค่าลดลงจะบวกเพิ่มให้อัตโนมัติ + เตือนใน Console
        /// </summary>
        private void OnValidate()
        {
            if (powerStageThresholds == null || powerStageThresholds.Length == 0)
                return;

            bool wasFixed = false;

            for (int i = 1; i < powerStageThresholds.Length; i++)
            {
                if (powerStageThresholds[i] <= powerStageThresholds[i - 1])
                {
                    powerStageThresholds[i] = powerStageThresholds[i - 1] + 1;
                    wasFixed = true;
                }
            }

            if (wasFixed)
            {
                Debug.LogWarning("PlayerStatsManager: powerStageThresholds ต้องเรียงจากน้อยไปมากเท่านั้น (ห้ามค่าเท่ากันหรือค่าลดลง) " +
                    "- ระบบแก้ให้อัตโนมัติแล้ว ถ้าตัวเลขที่ได้ไม่ตรงกับที่ต้องการ กรุณาปรับค่าใน Inspector อีกครั้งให้เรียงน้อยไปมากเอง");
            }
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetRun();

            EnemyV1.OnAnyEnemyDefeatedByPlayer += HandleEnemyDefeated;
            BossV2.OnAnyBossDefeatedAny += HandleBossDefeated;
            BossV2.OnAnySpellCardResult += HandleSpellCardResult;
        }

        private void Start()
        {
            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.OnSceneEnter += CaptureCurrentPlayerPosition;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            EnemyV1.OnAnyEnemyDefeatedByPlayer -= HandleEnemyDefeated;
            BossV2.OnAnyBossDefeatedAny -= HandleBossDefeated;
            BossV2.OnAnySpellCardResult -= HandleSpellCardResult;

            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.OnSceneEnter -= CaptureCurrentPlayerPosition;

            Instance = null;
        }

        // ============================================================
        // NEW GAME / RESET
        // ============================================================

        public void ResetRun()
        {
            // Clamp ค่าเริ่มต้นด้วย Max เพื่อป้องกัน Inspector ตั้ง starting > max
            currentLives = Mathf.Clamp(startingLives, 0, maxLives);
            currentBombs = Mathf.Clamp(startingBombs, 0, maxBombs);
            currentPower = Mathf.Clamp(startingPower, 0, maxPower);
            currentGraze = 0;

            hasSavedPosition = false;
            savedPlayerPosition = Vector3.zero;

            totalEnemiesDefeated = 0;
            totalBossesDefeated = 0;
            totalSpellCardsCaptured = 0;
            missCount = 0;
            bombsUsedCount = 0;

            stageRecords.Clear();

            OnLivesChanged?.Invoke(currentLives);
            InvokeSpellChanged();
            InvokePowerChanged();
            OnGrazeChanged?.Invoke(currentGraze);
        }

        // ============================================================
        // LIVES
        // ============================================================

        /// <summary>
        /// ลดชีวิต 1 และคืน true ถ้ายังสามารถ Respawn/Continue ได้
        /// คืน false เมื่อชีวิตหมดและควรเข้าสู่ Game Over
        /// </summary>
        public bool LoseLife()
        {
            currentLives = Mathf.Max(0, currentLives - 1);
            missCount++;

            OnLivesChanged?.Invoke(currentLives);

            bool canContinue = currentLives > 0;

            if (!canContinue)
                OnGameOver?.Invoke();

            return canContinue;
        }

        /// <summary>
        /// เพิ่มชีวิต แต่จะไม่เกิน MaxLives
        /// ถ้าค่าเต็มอยู่แล้ว จะไม่ยิง OnLivesChanged เพราะค่าจริงไม่เปลี่ยน
        /// </summary>
        public void AddLife(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentLives;

            currentLives = Mathf.Min(
                currentLives + amount,
                maxLives
            );

            if (currentLives != oldValue)
                OnLivesChanged?.Invoke(currentLives);
        }

        // ============================================================
        // BOMBS / SPELLS
        // ============================================================

        public bool TryUseBomb()
        {
            if (currentBombs <= 0)
                return false;

            currentBombs--;
            bombsUsedCount++;

            InvokeSpellChanged();
            return true;
        }

        /// <summary>
        /// Compatibility API ที่ PlayerControllerV1 ใช้อยู่
        /// UseSpell() = ใช้ Spell/Bomb 1 ลูก
        /// </summary>
        public bool UseSpell()
        {
            return TryUseBomb();
        }

        /// <summary>
        /// เพิ่ม Spell/Bomb แต่จะไม่เกิน MaxBombs
        /// </summary>
        public void AddBomb(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentBombs;

            currentBombs = Mathf.Min(
                currentBombs + amount,
                maxBombs
            );

            if (currentBombs != oldValue)
                InvokeSpellChanged();
        }

        /// <summary>
        /// Compatibility API สำหรับ UI/ระบบที่เรียกเพิ่ม Spell โดยตรง
        /// </summary>
        public void AddSpell(int amount = 1)
        {
            AddBomb(amount);
        }

        private void InvokeSpellChanged()
        {
            OnBombsChanged?.Invoke(currentBombs);
            OnSpellsChanged?.Invoke(currentBombs);
        }

        // ============================================================
        // POWER
        // ============================================================

        /// <summary>
        /// เพิ่ม Power จาก Fragment แต่จะไม่เกิน MaxPower
        /// </summary>
        public void AddFragment(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentPower;

            currentPower = Mathf.Min(
                currentPower + amount,
                maxPower
            );

            if (currentPower != oldValue)
                InvokePowerChanged();
        }

        /// <summary>
        /// Compatibility API สำหรับระบบที่เรียกเพิ่ม Power โดยตรง
        /// </summary>
        public void AddPower(int amount = 1)
        {
            AddFragment(amount);
        }

        private void InvokePowerChanged()
        {
            OnPowerChanged?.Invoke(currentPower);
            OnPowerLevelChanged?.Invoke(currentPower);

            int newStage = ComputePowerStage(currentPower);
            if (newStage != currentPowerStage)
            {
                currentPowerStage = newStage;
                OnPowerStageChanged?.Invoke(currentPowerStage);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // เพิ่มใหม่: log ไว้ debug ว่า Power Stage เปลี่ยนตอน currentPower เท่าไหร่
                // ถ้าเห็นมันกระโดดข้าม Stage (เช่น 0 -> 3 ทีเดียว) ให้เช็ค powerStageThresholds ใน Inspector ก่อน
                Debug.Log($"PlayerStatsManager: Power Stage เปลี่ยนเป็น {currentPowerStage} (currentPower = {currentPower})");
#endif
            }
        }

        /// <summary>
        /// แปลง currentPower (raw) ให้เป็น Power Stage 0-5 ตาม powerStageThresholds
        /// Stage 0 = ต่ำกว่าเกณฑ์แรกทั้งหมด, Stage N = ผ่านเกณฑ์ index (N-1) มาแล้ว
        /// Clamp ไว้ที่ 0-5 เผื่อ Inspector ใส่ threshold มาเกิน 5 ค่า
        /// </summary>
        private int ComputePowerStage(int power)
        {
            int stage = 0;

            for (int i = 0; i < powerStageThresholds.Length; i++)
            {
                if (power >= powerStageThresholds[i])
                    stage = i + 1;
            }

            return Mathf.Clamp(stage, 0, 5);
        }

        // ============================================================
        // GRAZE
        // ============================================================

        /// <summary>
        /// เพิ่มจำนวน Graze
        /// แยกจาก ScoreManager เพราะ Graze เป็นสถิติของผู้เล่น
        /// </summary>
        public void AddGraze(int amount = 1)
        {
            if (amount <= 0)
                return;

            currentGraze += amount;
            OnGrazeChanged?.Invoke(currentGraze);
        }

        // ============================================================
        // GAME LOSE / CONTINUE RESET
        // ============================================================

        /// <summary>
        /// Reset ค่าสถานะสำหรับ Continue จาก Game Lose:
        /// Lives = ค่าเริ่มต้น, Spells/Bombs = ค่าเริ่มต้น, Power = MaxPower
        /// ไม่แตะ Score หรือสถิติสะสม
        /// </summary>
        public void ResetForGameLoseContinue()
        {
            currentLives = Mathf.Clamp(startingLives, 0, maxLives);
            currentBombs = Mathf.Clamp(startingBombs, 0, maxBombs);
            currentPower = maxPower;

            OnLivesChanged?.Invoke(currentLives);
            InvokeSpellChanged();
            InvokePowerChanged();
        }

        // ============================================================
        // POSITION
        // ============================================================

        private void CaptureCurrentPlayerPosition()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return;

            savedPlayerPosition = playerObject.transform.position;
            hasSavedPosition = true;
        }

        public bool TryConsumeSavedPosition(out Vector3 position)
        {
            position = savedPlayerPosition;

            bool result = hasSavedPosition;
            hasSavedPosition = false;

            return result;
        }

        // ============================================================
        // STATS - EVENT HANDLERS
        // ============================================================

        private void HandleEnemyDefeated()
        {
            totalEnemiesDefeated++;
        }

        private void HandleBossDefeated()
        {
            totalBossesDefeated++;

            // หมายเหตุ:
            // ตอนนี้ยังคงพฤติกรรมเดิมไว้เพื่อไม่เปลี่ยน gameplay ก่อนสร้าง
            // StageFlow/GameFlow อย่างเป็นทางการ
            RecordStageCleared(SceneManager.GetActiveScene().name);
        }

        private void HandleSpellCardResult(bool captured)
        {
            if (captured)
                totalSpellCardsCaptured++;
        }

        // ============================================================
        // STAGE PROGRESS
        // ============================================================

        public void RecordStageCleared(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return;

            StageProgressRecord record =
                stageRecords.Find(r => r.stageId == stageId);

            if (record == null)
            {
                record = new StageProgressRecord
                {
                    stageId = stageId
                };

                stageRecords.Add(record);
            }

            record.clearCount++;

            int scoreNow =
                ScoreManager.Instance != null
                    ? ScoreManager.Instance.CurrentScore
                    : 0;

            if (scoreNow > record.highScore)
                record.highScore = scoreNow;
        }

        public StageProgressRecord GetStageRecord(string stageId)
        {
            return stageRecords.Find(r => r.stageId == stageId);
        }
    }
}