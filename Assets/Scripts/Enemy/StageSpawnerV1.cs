using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;
using EnemySystem;

namespace StageSystem
{
    /// <summary>
    /// 1 รายการ spawn - บอกว่า "เวลาไหน" (นับจากตอนที่กลุ่มของมันเริ่ม ไม่ใช่นับจากเริ่มฉากตรงๆ)
    /// ต้อง spawn "Enemy ตัวไหน" ที่ "จุดไหน"
    /// </summary>
    [System.Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("เวลานับจากตอนที่กลุ่ม (SpawnGroup) นี้เริ่ม (วินาที) - ไม่ใช่นับจากเริ่มฉากตรงๆ ดู groupStartTime ใน SpawnGroup")]
        public float spawnTime = 0f;

        [Tooltip("Enemy prefab ที่จะ spawn ตอนถึงเวลา")]
        public GameObject enemyPrefab;

        [Tooltip("ลาก Transform ของจุดที่จะ spawn (Empty GameObject ที่วางไว้ใน Scene)")]
        public Transform spawnPoint;

        [Tooltip("ไม่บังคับ - ลาก Transform waypoint จาก Scene มา override movementPath เริ่มต้นที่ตั้งไว้บน Prefab " +
                 "ใช้ตอนอยากให้ Enemy ตัวเดียวกันเดินคนละเส้นทางในแต่ละกลุ่ม/แต่ละครั้งที่ spawn " +
                 "เรียงลำดับตรงกับ step ใน movementPath ของ Prefab เว้นว่างไว้ได้ถ้าอยากใช้ path เดิมจาก Prefab ตรงๆ")]
        public List<Transform> movementOverride = new List<Transform>();

        // เก็บ state ว่า spawn ไปแล้วหรือยัง - ไม่โชว์ใน Inspector เพราะเป็นสถานะตอนรัน ไม่ใช่ค่าที่ผู้ออกแบบฉากต้องแก้
        [System.NonSerialized] public bool hasSpawned;
    }

    /// <summary>
    /// 1 กลุ่มศัตรู (Wave) - มี timeline อิสระของตัวเอง (groupStartTime) แยกจากกลุ่มอื่นโดยสิ้นเชิง
    /// ทำให้ออกแบบฉากที่มีศัตรูหลายชุดเคลื่อนที่/ยิงคนละแบบพร้อมกันได้ โดยไม่ต้องมึนกับ list ยาวๆ ที่ spawnTime ปนกันหมดทุกตัว
    /// เช่น Group "ฝูงซ้าย" เริ่มที่ 0 วิ, Group "ฝูงขวา" เริ่มที่ 2 วิ, Group "บอส" เริ่มที่ 20 วิ
    /// แต่ละกลุ่มอ่านและแก้ได้อิสระใน Inspector โดยไม่ปนกับกลุ่มอื่นเลย
    /// </summary>
    [System.Serializable]
    public class SpawnGroup
    {
        [Tooltip("ชื่อกลุ่ม - ใช้แค่ให้อ่านง่ายใน Inspector เท่านั้น ไม่มีผลต่อการทำงานจริง")]
        public string groupName = "New Group";

        [Tooltip("เวลาที่กลุ่มนี้จะเริ่มนับ (วินาทีนับจากเริ่มฉาก/stageTimer) - spawnTime ของแต่ละ entry ในกลุ่มนี้จะนับต่อจากจุดนี้ไป " +
                 "เช่น groupStartTime = 5 แล้ว entry ตัวแรกตั้ง spawnTime = 2 หมายถึง spawn ตอนวินาทีที่ 7 ของฉากจริงๆ")]
        public float groupStartTime = 0f;

        [Tooltip("รายการ Enemy ที่จะ spawn ในกลุ่มนี้ ใส่ตามลำดับที่สะดวก โค้ดจะเช็คให้เองว่าถึงเวลาไหนแล้วบ้าง")]
        public List<EnemySpawnEntry> entries = new List<EnemySpawnEntry>();
    }

    /// <summary>
    /// ผู้กำกับฉาก - ไล่เช็คทุกกลุ่ม (SpawnGroup) แล้วในแต่ละกลุ่มไล่เช็คทุก entry ว่าถึงเวลา spawn รายการไหนหรือยัง
    /// ยังคง component เดียวจัดการทั้งฉากเหมือนเดิม (ไม่ต้องวางหลาย component ในฉาก) แค่แบ่งข้อมูลภายในเป็นกลุ่มย่อย
    /// เพื่อให้จัดฉากที่มีศัตรูหลายเซ็ตเคลื่อนที่/จังหวะไม่เหมือนกันได้โดยไม่ต้องปนกันเป็น list เดียวยาวๆ
    /// </summary>
    public class StageSpawnerV1 : MonoBehaviour
    {
        [Tooltip("แต่ละกลุ่มคือชุดศัตรูที่มี timeline อิสระของตัวเอง (groupStartTime) แยกกันชัดเจนใน Inspector")]
        [SerializeField] private List<SpawnGroup> groups = new List<SpawnGroup>();

        // เวลาที่ผ่านไปนับจากฉากเริ่ม (เริ่มนับตั้งแต่ตัวนี้ Awake ไม่ใช่ Time.timeSinceLevelLoad
        // เพราะถ้ามี loading screen/cutscene ก่อนเข้าฉากจริง อยากให้ timer เริ่มนับตอนฉากพร้อมเล่นจริงๆ)
        private float stageTimer;

        void Update()
        {
            stageTimer += Time.deltaTime;

            // ไล่ทีละกลุ่ม แล้วในแต่ละกลุ่มไล่ทีละ entry - ซ้อน loop 2 ชั้น แต่แต่ละชั้นสเกลเล็ก (ไม่กี่กลุ่ม, ไม่กี่สิบ entry/กลุ่ม)
            // ไม่กระทบ performance อย่างมีนัยสำคัญเมื่อเทียบกับความชัดเจนที่ได้ตอนแก้ฉากใน Inspector
            for (int g = 0; g < groups.Count; g++)
            {
                SpawnGroup group = groups[g];

                // เวลาที่ผ่านไป "เทียบกับจุดเริ่มของกลุ่มนี้" - ถ้ายังไม่ถึงจุดเริ่มกลุ่ม (ติดลบ) ข้ามกลุ่มนี้ไปก่อนทั้งหมด
                float groupElapsed = stageTimer - group.groupStartTime;
                if (groupElapsed < 0f) continue;

                List<EnemySpawnEntry> entries = group.entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    EnemySpawnEntry entry = entries[i];

                    if (entry.hasSpawned) continue;
                    if (groupElapsed < entry.spawnTime) continue;

                    SpawnEnemy(entry);
                    entry.hasSpawned = true;
                }
            }
        }

        private void SpawnEnemy(EnemySpawnEntry entry)
        {
            // กัน error ถ้าลืมลาก prefab หรือจุด spawn ไว้ในบางรายการ - ข้ามรายการนั้นไปเฉยๆ ไม่ทำให้รายการอื่นค้าง
            if (entry.enemyPrefab == null || entry.spawnPoint == null)
            {
                Debug.LogWarning("StageSpawnerV1: มีรายการที่ไม่ได้ตั้งค่า enemyPrefab หรือ spawnPoint ไว้ - ข้ามรายการนี้ไป");
                return;
            }

            GameObject instance = PoolManager.Instance.Get(entry.enemyPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation);

            // ถ้ามีการ override waypoint ไว้ในรายการนี้ ให้ยัดเข้าไปแทนที่ movementPath เริ่มต้นของ Prefab
            if (entry.movementOverride.Count > 0 && instance.TryGetComponent<EnemyV1>(out var enemy))
            {
                enemy.SetMovementTargets(entry.movementOverride);
            }
        }
    }
}