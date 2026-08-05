using UnityEngine;
using GameSystem;
using PlayerSystem;

namespace DebugSystem
{
    /// <summary>
    /// สคริปต์ debug ล้วนๆ - ไม่แก้ค่า/state ของระบบเกมเพลย์เลยแม้แต่บรรทัดเดียว มีหน้าที่แค่ "ฟัง" event ที่มีอยู่แล้ว
    /// จากทุก Manager แล้ว Debug.Log ออกมาให้เห็นว่าระบบไหนทำงานถูกจังหวะไหม
    ///
    /// ทำไมแยกเป็นไฟล์ต่างหาก ไม่ฝัง Debug.Log ไว้ในแต่ละสคริปต์เกมเพลย์ตรงๆ:
    /// 1. ลบออกจาก build จริงได้ง่าย แค่เอา component นี้ออกจาก Scene เดียว ไม่ต้องไล่ลบ Debug.Log กระจายอยู่หลายสิบไฟล์
    /// 2. เกมเพลย์โค้ดสะอาด ไม่ปนกับโค้ด debug ที่ไม่เกี่ยวกับ logic จริง
    /// 3. เปิด/ปิดการ log ได้ทีเดียวจากจุดเดียว (ติ๊ก field ด้านล่าง) โดยไม่ต้อง comment/uncomment โค้ดในไฟล์อื่น
    ///
    /// วิธีใช้: สร้าง Empty GameObject ชื่อ "DebugMonitor" ลอยไว้ใน Scene ที่ทดสอบ ติด component นี้ ไม่ต้องตั้งค่าอะไรเพิ่ม
    /// เปิด Console (Window > General > Console) แล้วเล่นเกมดู log จะไล่บอกเองว่า event ไหนยิงตอนไหน ค่าอะไร
    /// </summary>
    public class DebugMonitorV1 : MonoBehaviour
    {
        [Header("เปิด/ปิด Log แต่ละหมวด")]
        [SerializeField] private bool logScore = true;
        [SerializeField] private bool logPlayerStats = true;
        [SerializeField] private bool logSingletonCheck = true;

        void Start()
        {
            if (logSingletonCheck) CheckSingletons();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// เช็คตอนเริ่ม Scene ว่า Singleton หลักๆ ที่ระบบทั้งเกมพึ่งพา (Instance) มีอยู่จริงไหม
        /// เจอบ่อยมากว่าโค้ดถูกแต่ "ลืมวาง GameObject ที่ติด Manager ไว้ใน Scene" ทำให้ error กระจายไปทั่วแบบหาสาเหตุยาก
        /// เช็คจุดเดียวตรงนี้จบ รู้ทันทีตั้งแต่ต้น Scene ว่าตัวไหนหาย
        /// </summary>
        private void CheckSingletons()
        {
            Debug.Log("[DebugMonitor] ==== เช็ค Singleton เริ่มต้น Scene ====");

            LogSingletonStatus("PoolManager", PoolingSystem.PoolManager.Instance != null);
            LogSingletonStatus("ScoreManager", ScoreManager.Instance != null);
            LogSingletonStatus("PlayerStatsManager", PlayerStatsManager.Instance != null);
        }

        private void LogSingletonStatus(string name, bool exists)
        {
            if (exists)
                Debug.Log($"[DebugMonitor] OK: {name}.Instance พร้อมใช้งาน");
            else
                Debug.LogWarning($"[DebugMonitor] หาย: {name}.Instance เป็น null - ลืมวาง GameObject ที่ติด {name} ไว้ใน Scene หรือเปล่า?");
        }

        private void SubscribeEvents()
        {
            if (logScore && ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += LogScoreChanged;
                ScoreManager.Instance.OnHiScoreChanged += LogHiScoreChanged;
            }

            if (logPlayerStats && PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.OnLivesChanged += LogLivesChanged;
                PlayerStatsManager.Instance.OnSpellsChanged += LogSpellsChanged;
                PlayerStatsManager.Instance.OnPowerLevelChanged += LogPowerLevelChanged;
                PlayerStatsManager.Instance.OnGrazeChanged += LogGrazeChanged;
                PlayerStatsManager.Instance.OnGameOver += LogGameOver;
            }
        }

        private void UnsubscribeEvents()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= LogScoreChanged;
                ScoreManager.Instance.OnHiScoreChanged -= LogHiScoreChanged;
            }

            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.OnLivesChanged -= LogLivesChanged;
                PlayerStatsManager.Instance.OnSpellsChanged -= LogSpellsChanged;
                PlayerStatsManager.Instance.OnPowerLevelChanged -= LogPowerLevelChanged;
                PlayerStatsManager.Instance.OnGrazeChanged -= LogGrazeChanged;
                PlayerStatsManager.Instance.OnGameOver -= LogGameOver;
            }
        }

        // ---------- Score ----------
        private void LogScoreChanged(int value) => Debug.Log($"[DebugMonitor] Score เปลี่ยนเป็น {value}");
        private void LogHiScoreChanged(int value) => Debug.Log($"[DebugMonitor] HiScore ใหม่! -> {value}");

        // ---------- PlayerStats ----------
        private void LogLivesChanged(int value) => Debug.Log($"[DebugMonitor] Lives เปลี่ยนเป็น {value}");
        private void LogSpellsChanged(int value) => Debug.Log($"[DebugMonitor] Spells เปลี่ยนเป็น {value}");
        private void LogPowerLevelChanged(int value) => Debug.Log($"[DebugMonitor] Power Level เปลี่ยนเป็น {value}");
        private void LogGrazeChanged(int value) => Debug.Log($"[DebugMonitor] Graze สะสม: {value} ครั้ง");
        private void LogGameOver() => Debug.LogWarning("[DebugMonitor] Game Over ถูกยิง (OnGameOver)");
    }
}