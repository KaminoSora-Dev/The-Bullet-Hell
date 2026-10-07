using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;
using EnemySystem;
using AudioSystem;

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
    ///
    /// MUSIC SYNC (เพิ่มใหม่):
    /// - ถ้า MusicManager มี Stage Clock (นาฬิกาเพลงจาก dspTime) พร้อมใช้งาน
    ///   StageTimer จะถูกปรับให้ตรงกับเพลงเสมอ - กระสุน/ศัตรูจึงตรงกับเพลง
    ///   ไม่ว่าเสียงจะเริ่มช้า/เร็วแค่ไหน หรือเฟรมจะสะดุดหรือไม่
    /// - ถ้ายังไม่มีนาฬิกาเพลง (หรือปิด syncToMusic) จะนับด้วย Time.deltaTime
    ///   แบบเดิมทุกประการ
    /// - StageTimer ไม่เคยเดินถอยหลัง (ถ้าเดินนำเพลง จะหยุดรอเพลงตามทัน)
    /// - PauseTimer()/ResumeTimer() (เช่น ระหว่าง Dialogue) ยังทำงานเหมือนเดิม
    ///   แม้เพลงจะเล่นต่อ: เวลาที่ Pause จะถูกหักออกจากนาฬิกาเพลงให้อัตโนมัติ
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
        // MUSIC SYNC (เพิ่มใหม่)
        // ============================================================
        //
        // ซิงก์ StageTimer ตามนาฬิกาเพลงของ MusicManager (Stage Clock)
        //
        [Header("Music Sync (ซิงก์ StageTimer ตามเพลง)")]
        [Tooltip(
            "เปิด = StageTimer ตามเวลาเพลงของ MusicManager (แนะนำ)\n" +
            "ปิด = นับด้วย Time.deltaTime แบบเดิมอย่างเดียว"
        )]
        [SerializeField]
        private bool syncToMusic = true;

        [Tooltip(
            "ถ้า StageTimer คลาดจากเพลงเกินค่านี้ (วินาที) จะปรับให้ตรงทันที\n" +
            "ถ้าน้อยกว่านี้จะค่อย ๆ ปรับแบบนุ่มนวล (กันเวลากระตุกจากความละเอียดของ dspTime)"
        )]
        [Min(0.01f)]
        [SerializeField]
        private float snapThreshold = 0.1f;

        [Tooltip(
            "ความเร็วในการปรับ drift เล็ก ๆ (ครั้ง/วินาที) ยิ่งมากยิ่งตามเพลงเร็ว"
        )]
        [Min(0f)]
        [SerializeField]
        private float correctionRate = 8f;

        [Tooltip(
            "ถ้า StageTimer กับเพลงต่างกันเกินค่านี้ (วินาที) ถือว่านาฬิกาเพลง\n" +
            "ไม่เกี่ยวกับ Timeline นี้ (เช่น เพลงถูกสั่งเริ่มทีหลังมาก) -> ไม่ซิงก์\n" +
            "และใช้ Time.deltaTime ต่อ พร้อมแจ้งเตือนใน Console 1 ครั้ง"
        )]
        [Min(0.5f)]
        [SerializeField]
        private float maxTrustedDrift = 3f;

        [Tooltip("เปิด Log เมื่อมีการ Snap ปรับเวลาให้ตรงเพลง (ใช้ไล่บัค)")]
        [SerializeField]
        private bool debugSyncLogs = false;

        // ค่าที่หักออกจากนาฬิกาเพลง เพื่อให้ได้ StageTimer
        // (เพิ่มขึ้นเมื่อ Pause Timer ขณะเพลงเล่นต่อ หรือตอน ResetStageTimer)
        private float musicClockOffset = 0f;

        // เวลาของนาฬิกาเพลงตอนเรียก PauseTimer() (-1 = ไม่มีนาฬิกาตอนนั้น)
        private float pauseStartMusicClock = -1f;

        private bool syncRejectedLogged = false;


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
            // จดเวลาเพลง ณ จุดที่เริ่ม Pause (ถ้าเพลงเล่นต่อระหว่าง Pause
            // จะได้รู้ว่าต้องหักออกเท่าไหร่ตอน Resume)
            if (!isTimerPaused)
            {
                float clock;
                pauseStartMusicClock = TryGetMusicClock(out clock) ? clock : -1f;
            }

            isTimerPaused = true;
        }

        public void ResumeTimer()
        {
            // หักช่วงเวลาที่ Pause ออกจากนาฬิกาเพลง เพื่อให้ StageTimer
            // เดินต่อจากจุดเดิมพอดี ไม่ถูกดึงข้ามไปตามเพลงที่เล่นต่อ
            if (isTimerPaused && pauseStartMusicClock >= 0f)
            {
                float clock;
                if (TryGetMusicClock(out clock))
                    musicClockOffset += clock - pauseStartMusicClock;
            }

            pauseStartMusicClock = -1f;
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

            musicClockOffset = 0f;
            pauseStartMusicClock = -1f;
            syncRejectedLogged = false;

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
            // (เพิ่มใหม่) ถ้ามีนาฬิกาเพลง จะปรับให้ตรงกับเพลงด้วย
            //
            AdvanceStageTimer();


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
        // STAGE TIMER ADVANCE + MUSIC SYNC (เพิ่มใหม่)
        // ============================================================
        //
        // เดิน StageTimer ด้วย Time.deltaTime แล้วเทียบกับนาฬิกาเพลง:
        //
        //   target = นาฬิกาเพลง - musicClockOffset
        //   drift  = target - (เวลาที่ควรจะเป็นถ้าเดินด้วย deltaTime ล้วน ๆ)
        //
        // - |drift| >= snapThreshold  -> ปรับให้ตรง target ทันที
        //   (ถ้า target ต่ำกว่าเวลาปัจจุบัน = Timer นำเพลง -> หยุดรอเพลงตามทัน
        //    เพราะ Timer ห้ามเดินถอยหลัง)
        // - |drift| < snapThreshold   -> ค่อย ๆ ปรับตาม correctionRate
        // - |drift| > maxTrustedDrift -> ไม่ซิงก์ (นาฬิกาเพลงน่าจะไม่เกี่ยวกับ
        //   Timeline นี้) ใช้ deltaTime ล้วน ๆ
        //
        private void AdvanceStageTimer()
        {
            float dt = Time.deltaTime;

            // timeScale = 0 (Loading / Pause) - ไม่ต้องทำอะไร
            // (นาฬิกาเพลงก็ถูก Pause พร้อมกันอยู่แล้ว)
            if (dt <= 0f)
                return;

            float previous = stageTimer;
            float next = previous + dt;

            float musicClock;
            if (TryGetMusicClock(out musicClock))
            {
                float target = musicClock - musicClockOffset;
                float drift = target - next;
                float absDrift = Mathf.Abs(drift);

                if (absDrift > maxTrustedDrift)
                {
                    if (!syncRejectedLogged)
                    {
                        syncRejectedLogged = true;

                        Debug.LogWarning(
                            $"StageSpawnerV1: StageTimer ({next:F2}s) ต่างจากนาฬิกาเพลง " +
                            $"({target:F2}s) เกิน maxTrustedDrift ({maxTrustedDrift:F1}s) " +
                            "- ปิดการซิงก์ชั่วคราวและใช้ Time.deltaTime ต่อ " +
                            "(เช็คว่าเพลงฉากปกติถูกสั่งเล่นช้ากว่าที่ควรหรือไม่)"
                        );
                    }
                }
                else if (absDrift >= snapThreshold)
                {
                    if (debugSyncLogs)
                    {
                        Debug.Log(
                            $"[StageSpawner] Sync snap: StageTimer {next:F3}s → {target:F3}s " +
                            $"(drift {drift:+0.000;-0.000}s)"
                        );
                    }

                    next = target;
                }
                else
                {
                    next += drift * Mathf.Clamp01(correctionRate * dt);
                }
            }

            // Timer ห้ามเดินถอยหลัง
            stageTimer = Mathf.Max(next, previous);
        }


        /// <summary>
        /// อ่านนาฬิกาเพลงจาก MusicManager (ถ้าเปิด syncToMusic และนาฬิกาพร้อมแล้ว)
        /// </summary>
        private bool TryGetMusicClock(out float clock)
        {
            clock = 0f;

            if (!syncToMusic)
                return false;

            MusicManager music = MusicManager.Instance;

            if (music == null)
                return false;

            return music.TryGetStageClock(out clock);
        }


        // ============================================================
        // STAGE TIMER CONTROL
        // ============================================================

        /// <summary>
        /// รีเซ็ต Timer ของ Stage กลับเป็น 0
        ///
        /// ใช้กรณี Restart Stage โดยไม่โหลด Scene ใหม่
        ///
        /// (เพิ่มใหม่) ถ้านาฬิกาเพลงกำลังเดินอยู่ จะถือว่า "เวลาเพลง ณ ตอนนี้"
        /// คือ StageTimer = 0 - ถ้าต้องการให้เริ่มใหม่พร้อมเพลง ให้สั่ง
        /// MusicManager.StopMusic() แล้วเล่นเพลงใหม่ ซึ่งจะเริ่มนาฬิกาเพลงจาก 0 เอง
        /// </summary>
        public void ResetStageTimer()
        {
            stageTimer = 0f;
            isTimerPaused = false;

            pauseStartMusicClock = -1f;
            syncRejectedLogged = false;

            float clock;
            musicClockOffset = TryGetMusicClock(out clock) ? clock : 0f;

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