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

        // สถิติของด่านล่าสุดที่ผ่านสำเร็จ
        public int deathCount;
        public int spellUsedCount;
        public float timeSpentSeconds;
    }

    /// <summary>
    /// สรุปผลของ 1 การเล่นจบ (1 Run)
    ///
    /// completed = true
    ///     จบเกมสำเร็จ
    ///
    /// completed = false
    ///     Game Over และผู้เล่นเลือกไม่ Continue
    ///
    /// เก็บใน RAM เท่านั้นในปัจจุบัน
    /// </summary>
    [Serializable]
    public class RunSummaryRecord
    {
        public int finalScore;
        public float totalTimeSeconds;
        public int deathCount;
        public int spellUsedCount;
        public int stagesCleared;
        public bool completed;
        public string timestampUtc;
    }

    /// <summary>
    /// Singleton ถาวร (DontDestroyOnLoad)
    ///
    /// เก็บสถานะผู้เล่นที่ต้องคงอยู่ข้าม Scene:
    /// Lives, Spells, Power และสถิติต่าง ๆ
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

        [Header("Power Stage Thresholds")]
        [Tooltip(
            "ค่า currentPower ขั้นต่ำที่ต้องถึงเพื่อขึ้น Power Stage 1,2,3,4,5 ตามลำดับ\n" +
            "(index 0 = เกณฑ์ขึ้น Stage 1, index 1 = เกณฑ์ขึ้น Stage 2 ...)\n" +
            "ต้องเรียงค่าจากน้อยไปมาก"
        )]
        [SerializeField] private int[] powerStageThresholds =
            { 16, 32, 64, 96, 128 };

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

        private readonly List<StageProgressRecord> stageRecords =
            new List<StageProgressRecord>();

        // ============================================================
        // RUN / STAGE TIMER
        // ============================================================

        private float currentRunTimer;
        private float currentStageTimer;

        private int stageStartMissCount;
        private int stageStartBombsUsedCount;

        // ============================================================
        // RUN HISTORY
        // ============================================================

        private readonly List<RunSummaryRecord> runHistory =
            new List<RunSummaryRecord>();

        /// <summary>
        /// สำคัญ:
        /// ป้องกัน RecordRunFinished() ถูกเรียกซ้ำใน Run เดียวกัน
        ///
        /// ตัวอย่างบั๊กที่ป้องกัน:
        ///
        /// Update()
        ///   -> UpdateFinalFade()
        ///      -> FinishFinalFade()
        ///         -> RecordRunFinished()
        ///
        /// ถ้า FinishFinalFade ถูกเรียกหลาย Frame
        /// จะไม่สร้าง Run #1, #2, #3, ... ซ้ำอีก
        /// </summary>
        private bool hasRecordedCurrentRun;

        // ============================================================
        // PROPERTIES
        // ============================================================

        public int CurrentLives => currentLives;
        public int CurrentBombs => currentBombs;

        public int CurrentPower => currentPower;
        public int CurrentGraze => currentGraze;

        // Compatibility
        public int CurrentSpells => currentBombs;
        public int CurrentPowerLevel => currentPower;

        public int CurrentPowerStage => currentPowerStage;

        public int MaxLives => maxLives;
        public int MaxBombs => maxBombs;
        public int MaxPower => maxPower;

        public int TotalEnemiesDefeated => totalEnemiesDefeated;
        public int TotalBossesDefeated => totalBossesDefeated;
        public int TotalSpellCardsCaptured => totalSpellCardsCaptured;
        public int MissCount => missCount;
        public int BombsUsedCount => bombsUsedCount;

        public IReadOnlyList<StageProgressRecord> AllStageRecords =>
            stageRecords;

        public float CurrentRunTimeSeconds => currentRunTimer;
        public float CurrentStageTimeSeconds => currentStageTimer;

        public IReadOnlyList<RunSummaryRecord> RunHistory =>
            runHistory;

        // ============================================================
        // EVENTS
        // ============================================================

        public event Action<int> OnLivesChanged;
        public event Action<int> OnBombsChanged;
        public event Action<int> OnPowerChanged;

        // Compatibility events
        public event Action<int> OnSpellsChanged;
        public event Action<int> OnPowerLevelChanged;
        public event Action<int> OnGrazeChanged;

        public event Action<int> OnPowerStageChanged;

        /// <summary>
        /// ยิงเมื่อชีวิตหมดและเข้าสู่ Game Over
        /// </summary>
        public event Action OnGameOver;

#if UNITY_EDITOR

        private void OnValidate()
        {
            if (powerStageThresholds == null ||
                powerStageThresholds.Length == 0)
            {
                return;
            }

            bool wasFixed = false;

            for (int i = 1; i < powerStageThresholds.Length; i++)
            {
                if (powerStageThresholds[i] <= powerStageThresholds[i - 1])
                {
                    powerStageThresholds[i] =
                        powerStageThresholds[i - 1] + 1;

                    wasFixed = true;
                }
            }

            if (wasFixed)
            {
                Debug.LogWarning(
                    "PlayerStatsManager: powerStageThresholds ต้องเรียงจากน้อยไปมาก " +
                    "- ระบบแก้ให้อัตโนมัติแล้ว"
                );
            }
        }

#endif

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

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

            EnemyV1.OnAnyEnemyDefeatedByPlayer +=
                HandleEnemyDefeated;

            BossV2.OnAnyBossDefeatedAny +=
                HandleBossDefeated;

            BossV2.OnAnySpellCardResult +=
                HandleSpellCardResult;
        }

        private void Start()
        {
            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.OnSceneEnter +=
                    CaptureCurrentPlayerPosition;

                LoadingScreenManager.Instance.OnSceneEnter +=
                    HandleStageStarted;
            }
        }

        private void Update()
        {
            currentRunTimer += Time.deltaTime;
            currentStageTimer += Time.deltaTime;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            EnemyV1.OnAnyEnemyDefeatedByPlayer -=
                HandleEnemyDefeated;

            BossV2.OnAnyBossDefeatedAny -=
                HandleBossDefeated;

            BossV2.OnAnySpellCardResult -=
                HandleSpellCardResult;

            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.OnSceneEnter -=
                    CaptureCurrentPlayerPosition;

                LoadingScreenManager.Instance.OnSceneEnter -=
                    HandleStageStarted;
            }

            Instance = null;
        }

        // ============================================================
        // NEW GAME / RESET
        // ============================================================

        public void ResetRun()
        {
            currentLives =
                Mathf.Clamp(
                    startingLives,
                    0,
                    maxLives
                );

            currentBombs =
                Mathf.Clamp(
                    startingBombs,
                    0,
                    maxBombs
                );

            currentPower =
                Mathf.Clamp(
                    startingPower,
                    0,
                    maxPower
                );

            currentGraze = 0;

            currentPowerStage =
                ComputePowerStage(currentPower);

            hasSavedPosition = false;
            savedPlayerPosition = Vector3.zero;

            totalEnemiesDefeated = 0;
            totalBossesDefeated = 0;
            totalSpellCardsCaptured = 0;
            missCount = 0;
            bombsUsedCount = 0;

            currentRunTimer = 0f;
            currentStageTimer = 0f;

            stageStartMissCount = 0;
            stageStartBombsUsedCount = 0;

            stageRecords.Clear();

            // สำคัญ:
            // เมื่อเริ่ม Run ใหม่ ต้องอนุญาตให้ RecordRunFinished()
            // บันทึกผลของ Run นี้ได้อีกครั้ง
            hasRecordedCurrentRun = false;

            // runHistory ไม่ Clear
            // เพื่อเก็บประวัติ Run ก่อนหน้าใน Session เดียวกัน

            OnLivesChanged?.Invoke(currentLives);

            InvokeSpellChanged();

            InvokePowerChanged();

            OnGrazeChanged?.Invoke(currentGraze);
        }

        // ============================================================
        // LIVES
        // ============================================================

        public bool LoseLife()
        {
            currentLives =
                Mathf.Max(
                    0,
                    currentLives - 1
                );

            missCount++;

            OnLivesChanged?.Invoke(currentLives);

            bool canContinue = currentLives > 0;

            if (!canContinue)
                OnGameOver?.Invoke();

            return canContinue;
        }

        public void AddLife(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentLives;

            currentLives =
                Mathf.Min(
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

        public bool UseSpell()
        {
            return TryUseBomb();
        }

        public void AddBomb(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentBombs;

            currentBombs =
                Mathf.Min(
                    currentBombs + amount,
                    maxBombs
                );

            if (currentBombs != oldValue)
                InvokeSpellChanged();
        }

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

        public void AddFragment(int amount = 1)
        {
            if (amount <= 0)
                return;

            int oldValue = currentPower;

            currentPower =
                Mathf.Min(
                    currentPower + amount,
                    maxPower
                );

            if (currentPower != oldValue)
                InvokePowerChanged();
        }

        public void AddPower(int amount = 1)
        {
            AddFragment(amount);
        }

        private void InvokePowerChanged()
        {
            OnPowerChanged?.Invoke(currentPower);
            OnPowerLevelChanged?.Invoke(currentPower);

            int newStage =
                ComputePowerStage(currentPower);

            if (newStage != currentPowerStage)
            {
                currentPowerStage = newStage;

                OnPowerStageChanged?.Invoke(
                    currentPowerStage
                );

#if UNITY_EDITOR || DEVELOPMENT_BUILD

                Debug.Log(
                    $"PlayerStatsManager: Power Stage เปลี่ยนเป็น " +
                    $"{currentPowerStage} " +
                    $"(currentPower = {currentPower})"
                );

#endif
            }
        }

        private int ComputePowerStage(int power)
        {
            int stage = 0;

            if (powerStageThresholds == null)
                return stage;

            for (int i = 0;
                 i < powerStageThresholds.Length;
                 i++)
            {
                if (power >= powerStageThresholds[i])
                    stage = i + 1;
            }

            return Mathf.Clamp(stage, 0, 5);
        }

        // ============================================================
        // GRAZE
        // ============================================================

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

        public void ResetForGameLoseContinue()
        {
            currentLives =
                Mathf.Clamp(
                    startingLives,
                    0,
                    maxLives
                );

            currentBombs =
                Mathf.Clamp(
                    startingBombs,
                    0,
                    maxBombs
                );

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
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
                return;

            savedPlayerPosition =
                playerObject.transform.position;

            hasSavedPosition = true;
        }

        public bool TryConsumeSavedPosition(
            out Vector3 position)
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

            RecordStageCleared(
                SceneManager.GetActiveScene().name
            );
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
                stageRecords.Find(
                    r => r.stageId == stageId
                );

            if (record == null)
            {
                record =
                    new StageProgressRecord
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

            record.deathCount =
                missCount - stageStartMissCount;

            record.spellUsedCount =
                bombsUsedCount - stageStartBombsUsedCount;

            record.timeSpentSeconds =
                currentStageTimer;

            Debug.Log(
                $"PlayerStatsManager: ผ่านด่าน '{stageId}' " +
                $"(ครั้งที่ {record.clearCount}) | " +
                $"Score={scoreNow} (Best={record.highScore}) | " +
                $"ตายในด่านนี้={record.deathCount} | " +
                $"ใช้ Spell={record.spellUsedCount} | " +
                $"เวลาในด่านนี้={record.timeSpentSeconds:F1}s"
            );
        }

        public StageProgressRecord GetStageRecord(
            string stageId)
        {
            return stageRecords.Find(
                r => r.stageId == stageId
            );
        }

        private void HandleStageStarted()
        {
            currentStageTimer = 0f;

            stageStartMissCount = missCount;

            stageStartBombsUsedCount =
                bombsUsedCount;

            Debug.Log(
                $"PlayerStatsManager: เริ่มด่าน " +
                $"'{SceneManager.GetActiveScene().name}' - " +
                "รีเซ็ตตัวจับเวลาด่าน และ snapshot สถิติเริ่มด่านแล้ว"
            );
        }

        // ============================================================
        // RUN SUMMARY
        // ============================================================

        /// <summary>
        /// บันทึกผลการเล่น 1 Run
        ///
        /// สำคัญ:
        /// ฟังก์ชันนี้อาจถูกเรียกจาก GameFlowManager มากกว่า 1 ครั้ง
        /// เช่น Final Fade ถูก Update ซ้ำหลาย Frame
        ///
        /// hasRecordedCurrentRun จะทำหน้าที่เป็น Guard
        /// ทำให้ Run เดียวถูกบันทึกได้เพียงครั้งเดียว
        /// </summary>
        public void RecordRunFinished(bool completed)
        {
            // ========================================================
            // DUPLICATE RUN GUARD
            // ========================================================
            //
            // ถ้า Run นี้ถูกบันทึกไปแล้ว:
            // ไม่สร้าง RunSummaryRecord ใหม่
            // และไม่เพิ่ม Run #2, #3, #4...
            //
            if (hasRecordedCurrentRun)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD

                Debug.LogWarning(
                    "PlayerStatsManager: RecordRunFinished() " +
                    "ถูกเรียกซ้ำใน Run เดิม - ข้ามการบันทึกครั้งนี้"
                );

#endif

                return;
            }

            // ล็อกทันที ก่อนสร้าง Record
            // เพื่อป้องกันกรณีที่มี callback / event อื่น
            // เรียกซ้ำในระหว่างกระบวนการเดียวกัน
            hasRecordedCurrentRun = true;

            var record =
                new RunSummaryRecord
                {
                    finalScore =
                        ScoreManager.Instance != null
                            ? ScoreManager.Instance.CurrentScore
                            : 0,

                    totalTimeSeconds =
                        currentRunTimer,

                    deathCount =
                        missCount,

                    spellUsedCount =
                        bombsUsedCount,

                    stagesCleared =
                        totalBossesDefeated,

                    completed =
                        completed,

                    timestampUtc =
                        DateTime.UtcNow.ToString("u")
                };

            runHistory.Add(record);

            Debug.Log(
                $"PlayerStatsManager: จบการเล่น " +
                $"(Run #{runHistory.Count}) | " +
                $"{(completed ? "ผ่านจบเกม" : "จบก่อน (Game Over)")} | " +
                $"Score={record.finalScore} | " +
                $"เวลาเล่นรวม={record.totalTimeSeconds:F1}s | " +
                $"ตายทั้งหมด={record.deathCount} ครั้ง | " +
                $"ใช้ Spell={record.spellUsedCount} ครั้ง | " +
                $"ผ่านไป {record.stagesCleared} ด่าน"
            );
        }
    }
}