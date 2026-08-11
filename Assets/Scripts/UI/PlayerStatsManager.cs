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

        private int currentLives;
        private int currentBombs;
        private int currentPower;
        private int currentGraze;

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
        /// ยิงเมื่อชีวิตหมดและการตายครั้งนั้นทำให้เข้าสู่ Game Over
        /// </summary>
        public event Action OnGameOver;

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