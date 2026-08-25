using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;
using EnemySystem;

namespace StageSystem
{
    [System.Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("เวลานับจากตอนที่กลุ่ม (SpawnGroup) นี้เริ่ม")]
        public float spawnTime = 0f;

        [Tooltip("Enemy prefab ที่จะ spawn ตอนถึงเวลา")]
        public GameObject enemyPrefab;

        [Tooltip("จุด Spawn")]
        public Transform spawnPoint;

        [Tooltip("Waypoint ที่ใช้ override movementPath ของ Prefab")]
        public List<Transform> movementOverride = new List<Transform>();


        // ============================================================
        // SPAWN AMOUNT / SPAWN INTERVAL (เพิ่มใหม่)
        // ============================================================
        //
        // ทำงานที่ระดับ Entry: 1 Entry สามารถ Spawn ศัตรูซ้ำจาก
        // spawnPoint เดิม ได้หลายตัว โดยเว้นระยะห่างตาม spawnInterval
        //
        // ตัวอย่าง: spawnAmount = 3, spawnInterval = 0.5
        // -> Spawn ที่ spawnTime, spawnTime + 0.5, spawnTime + 1.0
        //
        [Header("Repeat Spawn (Amount / Interval)")]
        [Tooltip(
            "จำนวนที่จะ Spawn ในรายการนี้\n" +
            "ค่าต่ำสุดคือ 1 (Spawn ครั้งเดียวเหมือนเดิม) ห้ามต่ำกว่านี้"
        )]
        [Min(1)]
        public int spawnAmount = 1;

        [Tooltip(
            "ระยะเวลาห่างระหว่างแต่ละครั้งที่ Spawn (วินาที)\n" +
            "มีผลเฉพาะตอน spawnAmount > 1 เท่านั้น"
        )]
        [Min(0f)]
        public float spawnInterval = 0f;


        // ============================================================
        // RUNTIME STATE (ไม่ Serialize - รีเซ็ตผ่าน ResetSpawnStates)
        // ============================================================

        [System.NonSerialized]
        public bool hasSpawned; // true เมื่อ Spawn ครบ spawnAmount แล้ว

        [System.NonSerialized]
        public int spawnedCount; // จำนวนที่ Spawn ไปแล้วของรายการนี้
    }


    [System.Serializable]
    public class SpawnGroup
    {
        [Tooltip("ชื่อกลุ่ม")]
        public string groupName = "New Group";

        [Tooltip("เวลาที่กลุ่มนี้เริ่มนับจาก StageTimer")]
        public float groupStartTime = 0f;

        public List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();
    }


    /// <summary>
    /// ควบคุม Timeline ของ Stage
    ///
    /// StageTimer:
    /// - เริ่มจาก 0 เมื่อ StageSpawner ถูกสร้าง
    /// - เดินตาม Time.deltaTime
    /// - หยุดอัตโนมัติเมื่อ Time.timeScale = 0
    /// - ระบบอื่นสามารถอ่านค่าได้ผ่าน StageTimer property
    /// - เมื่อขึ้น Scene/Stage ใหม่ ตัวใหม่จะเริ่มจาก 0
    /// </summary>
    public class StageSpawnerV1 : MonoBehaviour
    {
        // ============================================================
        // SINGLETON (เพิ่มใหม่)
        // ============================================================
        //
        // ให้ระบบอื่น เช่น SceneDialogueV1 อ่าน StageTimer และสั่ง
        // PauseTimer()/ResumeTimer() ได้ โดยไม่ต้องลาก Reference
        // ผ่าน Inspector ทุกจุด (สมมติว่ามี StageSpawnerV1 แค่ 1 ตัว
        // ต่อ Stage/Scene เหมือนที่ใช้งานอยู่ปัจจุบัน)
        //
        public static StageSpawnerV1 Instance { get; private set; }


        [Header("Spawn Groups")]
        [Tooltip("แต่ละกลุ่มมี Timeline ของตัวเอง")]
        [SerializeField]
        private List<SpawnGroup> groups = new List<SpawnGroup>();


        // ============================================================
        // STAGE TIMER
        // ============================================================

        [Header("Stage Timer")]
        [Tooltip(
            "เวลาที่ผ่านไปของด่านนี้\n" +
            "เริ่มจาก 0 ตอน Stage เริ่มเล่น\n" +
            "หยุดอัตโนมัติเมื่อ Time.timeScale = 0"
        )]
        [SerializeField]
        public float stageTimer = 0f;


        /// <summary>
        /// เวลาปัจจุบันของ Stage นี้
        ///
        /// ตัวอย่าง:
        /// 0.0  = เริ่มด่าน
        /// 5.0  = เล่นมาแล้ว 5 วินาที
        /// 30.0 = เล่นมาแล้ว 30 วินาที
        ///
        /// ระบบอื่น เช่น DialogueManager สามารถอ่านได้
        /// โดยไม่ต้องแก้ค่าโดยตรง
        /// </summary>
        public float StageTimer => stageTimer;


        // ============================================================
        // TIMER PAUSE (เพิ่มใหม่)
        // ============================================================
        //
        // ใช้โดย SceneDialogueV1 (หรือระบบอื่นในอนาคต เช่น Cutscene)
        // เพื่อหยุด StageTimer ไม่ให้เดิน และหยุด Spawn Timeline ชั่วคราว
        // โดยไม่ต้องยุ่งกับ Time.timeScale (ผู้เล่น/ศัตรูอื่นยังขยับได้
        // ตามปกติถ้าระบบนั้นไม่ได้ผูกกับ StageTimer โดยตรง)
        //
        [Header("Timer Pause (ควบคุมโดยระบบภายนอก เช่น SceneDialogueV1)")]
        [SerializeField]
        private bool isTimerPaused = false;

        public bool IsTimerPaused => isTimerPaused;

        public void PauseTimer()
        {
            isTimerPaused = true;
        }

        public void ResumeTimer()
        {
            isTimerPaused = false;
        }


        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    "StageSpawnerV1: พบ StageSpawnerV1 มากกว่า 1 ตัวใน Scene " +
                    "- Instance จะถูกแทนที่ด้วยตัวล่าสุด"
                );
            }

            Instance = this;

            // ทุกครั้งที่ StageSpawner ถูกสร้างขึ้น
            // Timer ของ Stage นี้เริ่มจาก 0
            stageTimer = 0f;
            isTimerPaused = false;

            ResetSpawnStates();
        }


        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }


        private void Update()
        {
            // --------------------------------------------------------
            // Stage Timer (ถูก Pause อยู่ เช่นระหว่าง Dialogue)
            // --------------------------------------------------------
            if (isTimerPaused)
                return;


            // --------------------------------------------------------
            // Stage Timer
            // --------------------------------------------------------
            //
            // ใช้ Time.deltaTime แทน Time.unscaledDeltaTime
            //
            // เพราะต้องการให้ Timer หยุดเมื่อ:
            //
            // Time.timeScale = 0
            //
            // ซึ่งเหมาะกับ Loading / Pause / Cutscene
            //
            stageTimer += Time.deltaTime;


            // --------------------------------------------------------
            // Spawn Timeline
            // --------------------------------------------------------

            for (int g = 0; g < groups.Count; g++)
            {
                SpawnGroup group = groups[g];

                if (group == null)
                    continue;


                // เวลาที่ผ่านไปหลังจาก Group นี้เริ่ม
                float groupElapsed = stageTimer - group.groupStartTime;

                // ยังไม่ถึงเวลาเริ่ม Group
                if (groupElapsed < 0f)
                    continue;


                List<EnemySpawnEntry> entries = group.entries;

                if (entries == null)
                    continue;


                for (int i = 0; i < entries.Count; i++)
                {
                    EnemySpawnEntry entry = entries[i];

                    if (entry == null)
                        continue;

                    // Spawn ครบ spawnAmount แล้ว - ข้ามรายการนี้
                    if (entry.hasSpawned)
                        continue;


                    // --------------------------------------------------
                    // Spawn Amount / Spawn Interval
                    // --------------------------------------------------
                    //
                    // ป้องกันค่าผิดพลาดจาก Inspector (เช่น List element
                    // ใหม่ที่ field initializer ไม่ทำงาน) - clamp
                    // ให้ spawnAmount ต่ำสุด 1 และ spawnInterval ต่ำสุด 0
                    // เสมอ ไม่ว่าค่าที่ Serialize มาจะเป็นเท่าไหร่
                    //
                    int amount = Mathf.Max(1, entry.spawnAmount);
                    float interval = Mathf.Max(0f, entry.spawnInterval);

                    // เวลาที่ Spawn ครั้งถัดไป (ลำดับที่ spawnedCount)
                    // ควรเกิดขึ้น นับจาก spawnTime ของ Entry
                    float nextSpawnAt = entry.spawnTime + (entry.spawnedCount * interval);

                    // ยังไม่ถึงเวลา Spawn ครั้งถัดไป
                    if (groupElapsed < nextSpawnAt)
                        continue;


                    // ถึงเวลาแล้ว - Spawn จาก spawnPoint เดิมของ Entry
                    SpawnEnemy(entry);
                    entry.spawnedCount++;

                    // Spawn ครบจำนวนที่กำหนดแล้ว
                    if (entry.spawnedCount >= amount)
                        entry.hasSpawned = true;

                    // หมายเหตุ: ตั้งใจให้ Spawn ได้อย่างมากแค่ 1 ตัว
                    // ต่อ Entry ต่อ 1 Frame (เหมือนโค้ดเดิม) เพื่อไม่ให้
                    // เกิด Spawn กระโดดพร้อมกันหลายตัวถ้า Frame กระตุก
                    // - รายการที่เหลือจะถูก Spawn ต่อใน Frame ถัดไป
                }
            }
        }


        // ============================================================
        // STAGE TIMER CONTROL
        // ============================================================

        /// <summary>
        /// รีเซ็ต Timer ของ Stage กลับเป็น 0
        ///
        /// ใช้กรณี Restart Stage โดยไม่โหลด Scene ใหม่
        /// </summary>
        public void ResetStageTimer()
        {
            stageTimer = 0f;
            isTimerPaused = false;

            ResetSpawnStates();
        }


        /// <summary>
        /// รีเซ็ตสถานะ Spawn ของทุก Entry
        ///
        /// ทำให้ Timeline สามารถเล่นใหม่ตั้งแต่ต้นได้
        /// </summary>
        private void ResetSpawnStates()
        {
            for (int g = 0; g < groups.Count; g++)
            {
                SpawnGroup group = groups[g];

                if (group == null || group.entries == null)
                    continue;


                for (int i = 0; i < group.entries.Count; i++)
                {
                    EnemySpawnEntry entry = group.entries[i];

                    if (entry == null)
                        continue;

                    entry.hasSpawned = false;
                    entry.spawnedCount = 0;
                }
            }
        }


        // ============================================================
        // SPAWN
        // ============================================================

        private void SpawnEnemy(EnemySpawnEntry entry)
        {
            // --------------------------------------------------------
            // Validate
            // --------------------------------------------------------

            if (entry.enemyPrefab == null || entry.spawnPoint == null)
            {
                Debug.LogWarning(
                    "StageSpawnerV1: มีรายการที่ไม่ได้ตั้งค่า " +
                    "enemyPrefab หรือ spawnPoint - ข้ามรายการนี้"
                );

                return;
            }


            // --------------------------------------------------------
            // Get From Pool
            // --------------------------------------------------------

            GameObject instance = PoolManager.Instance.Get(
                entry.enemyPrefab,
                entry.spawnPoint.position,
                entry.spawnPoint.rotation
            );


            // --------------------------------------------------------
            // Movement Override
            // --------------------------------------------------------

            if (entry.movementOverride != null &&
                entry.movementOverride.Count > 0 &&
                instance.TryGetComponent<EnemyV1>(out var enemy))
            {
                enemy.SetMovementTargets(entry.movementOverride);
            }
        }
    }
}