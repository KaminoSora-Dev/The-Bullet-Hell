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

        [System.NonSerialized]
        public bool hasSpawned;
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
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            // ทุกครั้งที่ StageSpawner ถูกสร้างขึ้น
            // Timer ของ Stage นี้เริ่มจาก 0
            stageTimer = 0f;

            ResetSpawnStates();
        }


        private void Update()
        {
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

                    // Spawn ไปแล้ว
                    if (entry.hasSpawned)
                        continue;

                    // ยังไม่ถึงเวลา Spawn
                    if (groupElapsed < entry.spawnTime)
                        continue;


                    // ถึงเวลาแล้ว
                    SpawnEnemy(entry);

                    // สำคัญ:
                    // ตั้งเป็น true หลังจากพยายาม Spawn
                    entry.hasSpawned = true;
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