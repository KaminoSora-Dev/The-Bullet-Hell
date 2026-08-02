using System.Collections.Generic;
using UnityEngine;

namespace StageSystem
{
    /// <summary>
    /// 1 รายการ spawn - บอกว่า "เวลาไหน" ต้อง spawn "Enemy ตัวไหน" ที่ "จุดไหน"
    /// ทำเป็น [System.Serializable] เพื่อให้แก้ค่าใน Inspector เป็น list ได้ตรงๆ เหมือน ShootBurst ใน EnemyShooterV1
    /// </summary>
    [System.Serializable]
    public class EnemySpawnEntry
    {
        [Tooltip("เวลานับจากเริ่มฉาก (วินาที) ที่จะ spawn ศัตรูตัวนี้ - ไม่จำเป็นต้องเรียงตามเวลาใน list ก็ได้")]
        public float spawnTime = 0f;

        [Tooltip("Enemy prefab ที่จะ spawn ตอนถึงเวลา")]
        public GameObject enemyPrefab;

        [Tooltip("ลาก Transform ของจุดที่จะ spawn (Empty GameObject ที่วางไว้ใน Scene)")]
        public Transform spawnPoint;

        // เก็บ state ว่า spawn ไปแล้วหรือยัง - ไม่โชว์ใน Inspector เพราะเป็นสถานะตอนรัน ไม่ใช่ค่าที่ผู้ออกแบบฉากต้องแก้
        [System.NonSerialized] public bool hasSpawned;
    }

    /// <summary>
    /// ผู้กำกับฉาก - ไล่เช็ค spawnList ทุกเฟรมว่าถึงเวลา spawn รายการไหนหรือยัง
    /// ใช้ timer สะสมแบบเดียวกับระบบอื่นในโปรเจกต์ (ไม่ใช้ Coroutine/WaitForSeconds)
    /// </summary>
    public class StageSpawnerV1 : MonoBehaviour
    {
        [Tooltip("รายการศัตรูที่จะเกิดตามเวลา ใส่ตามลำดับที่สะดวก โค้ดจะเช็คให้เองว่าถึงเวลาไหนแล้วบ้าง")]
        [SerializeField] private List<EnemySpawnEntry> spawnList = new List<EnemySpawnEntry>();

        // เวลาที่ผ่านไปนับจากฉากเริ่ม (เริ่มนับตั้งแต่ตัวนี้ Awake ไม่ใช่ Time.timeSinceLevelLoad
        // เพราะถ้ามี loading screen/cutscene ก่อนเข้าฉากจริง อยากให้ timer เริ่มนับตอนฉากพร้อมเล่นจริงๆ)
        private float stageTimer;

        void Update()
        {
            stageTimer += Time.deltaTime;

            // ไล่ทีละรายการ - ถ้า list ใหญ่มาก (หลายร้อยรายการ) ค่อยพิจารณาเรียงตาม spawnTime
            // แล้วใช้ index ชี้ตำแหน่งแทนการไล่ทั้ง list ทุกเฟรม แต่สเกลปกติของ bullet hell (ไม่กี่สิบ-ร้อยรายการ)
            // ไล่ตรงๆ แบบนี้เพียงพอ ไม่คุ้มที่จะเพิ่มความซับซ้อนตอนนี้
            for (int i = 0; i < spawnList.Count; i++)
            {
                EnemySpawnEntry entry = spawnList[i];

                if (entry.hasSpawned) continue;
                if (stageTimer < entry.spawnTime) continue;

                SpawnEnemy(entry);
                entry.hasSpawned = true;
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

            Instantiate(entry.enemyPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation);
        }
    }
}
