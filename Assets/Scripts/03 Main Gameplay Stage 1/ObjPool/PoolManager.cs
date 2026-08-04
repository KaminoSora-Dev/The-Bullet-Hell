using System.Collections.Generic;
using UnityEngine;

namespace PoolingSystem
{
    /// <summary>
    /// ตั้งค่า 1 รายการ pool ใน Inspector: prefab ตัวไหน, prewarm ไว้กี่ตัวตอนเริ่มฉาก
    /// </summary>
    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        [Tooltip("จำนวนที่จะสร้างไว้ล่วงหน้าตอนเริ่มฉาก (prewarm) - ตั้งให้พอกับ peak การใช้งานจริงของ prefab นี้")]
        public int prewarmCount = 20;
    }

    /// <summary>
    /// ตัวกลางจัดการ Object Pool ทั้งหมดในเกม - Singleton ตัวเดียว รองรับหลาย prefab พร้อมกัน
    /// แยก pool ตาม prefab reference (Dictionary key = prefab) เพราะกระสุน/Enemy/Fragment แต่ละแบบ
    /// มี component/behavior ไม่เหมือนกัน เอามาปนกันเป็น pool เดียวไม่ได้
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [Tooltip("รายการ prefab ที่ต้องการ prewarm ไว้ล่วงหน้าตอนเริ่มฉาก - ใส่ทุก prefab ที่จะดึงผ่าน pool")]
        [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();

        // pool ของแต่ละ prefab เก็บเป็น Queue เพราะ FIFO เหมาะกับ pool: ตัวที่ถูกคืนนานสุดจะถูกหยิบไปใช้ก่อน
        // (จริงๆ Stack ก็ใช้ได้เหมือนกันสำหรับ pooling ทั่วไป แต่ Queue ทำให้ instance หมุนเวียนใช้ทั่วถึงกว่า
        // ไม่ใช้ตัวเดิมซ้ำๆ ตลอด ซึ่งช่วยกระจาย "อายุการใช้งาน" ของแต่ละ instance เท่าๆ กัน)
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

        // เก็บ Transform สำหรับจัดกลุ่มลูกใน Hierarchy ตาม prefab เพื่อความเป็นระเบียบตอนเปิดดูใน Editor
        // (ไม่มีผลต่อการทำงานจริง แค่ทำให้ debug ง่ายขึ้นเวลาเปิด Hierarchy ดู)
        private readonly Dictionary<GameObject, Transform> poolContainers = new Dictionary<GameObject, Transform>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (PoolConfig config in poolConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogWarning("PoolManager: มีรายการ poolConfigs ที่ไม่ได้ตั้งค่า prefab ไว้ - ข้ามรายการนี้ไป");
                    continue;
                }
                Prewarm(config.prefab, config.prewarmCount);
            }
        }

        /// <summary>
        /// สร้าง pool ของ prefab นี้ล่วงหน้าตามจำนวนที่กำหนด (ถ้ามี pool อยู่แล้วจะเพิ่มเข้าไปอีก ไม่ล้างของเดิม)
        /// เรียกจาก Awake() ตาม poolConfigs โดยอัตโนมัติ หรือจะเรียกเพิ่มเองระหว่างเล่นก็ได้ (เช่นก่อนเข้าฉากบอส)
        /// </summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (!pools.ContainsKey(prefab))
            {
                pools[prefab] = new Queue<GameObject>();

                // สร้าง container แยกใน Hierarchy ตามชื่อ prefab เพื่อความเป็นระเบียบ
                GameObject containerObj = new GameObject($"Pool_{prefab.name}");
                containerObj.transform.SetParent(transform);
                poolContainers[prefab] = containerObj.transform;
            }

            Queue<GameObject> queue = pools[prefab];
            Transform container = poolContainers[prefab];

            for (int i = 0; i < count; i++)
            {
                GameObject instance = CreateNewInstance(prefab, container);
                instance.SetActive(false);
                queue.Enqueue(instance);
            }
        }

        private GameObject CreateNewInstance(GameObject prefab, Transform container)
        {
            GameObject instance = Instantiate(prefab, container);

            // ติด tag บอกที่มาไว้ตั้งแต่สร้าง เพื่อให้ Return() รู้ว่าต้องคืนเข้า pool ไหน
            PooledObjectTag tag = instance.AddComponent<PooledObjectTag>();
            tag.SourcePrefab = prefab;

            return instance;
        }

        /// <summary>
        /// หยิบ object จาก pool มาใช้ - ถ้า pool ของ prefab นี้ว่าง (ใช้หมด) จะสร้างตัวใหม่เพิ่มให้อัตโนมัติ (dynamic growth)
        /// พร้อม warning เตือน เพราะถ้าเจอ warning นี้บ่อยๆ แปลว่าตั้ง prewarmCount ไว้น้อยเกินไปสำหรับ pattern จริง
        /// ควรกลับไปเพิ่มค่าใน PoolConfig แทนที่จะปล่อยให้โต dynamic ตลอด (โตแบบ dynamic ก็ยัง Instantiate จริงตอนนั้นอยู่ดี = GC spike)
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolManager.Get: prefab เป็น null");
                return null;
            }

            if (!pools.ContainsKey(prefab))
            {
                Debug.LogWarning($"PoolManager: '{prefab.name}' ไม่ได้ prewarm ไว้ล่วงหน้า - กำลังสร้าง pool ใหม่แบบ lazy (ควรไปเพิ่มใน poolConfigs แทน)");
                Prewarm(prefab, 0);
            }

            Queue<GameObject> queue = pools[prefab];

            GameObject instance;
            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
            }
            else
            {
                // pool หมด (exhaustion) - โตเพิ่มตอนนี้เลยแทนที่จะคืน null ให้ caller งง
                // ยอมเสีย GC spike ครั้งนี้ครั้งเดียว ดีกว่ากระสุน/ศัตรูหายไปเฉยๆ ตาม pattern ที่ออกแบบไว้
                Debug.LogWarning($"PoolManager: pool ของ '{prefab.name}' หมด (exhausted) - สร้างเพิ่มตอนนี้ (ลองเพิ่ม prewarmCount ถ้าเจอบ่อย)");
                instance = CreateNewInstance(prefab, poolContainers[prefab]);
            }

            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);

            // ต้องตั้งตำแหน่ง + SetActive(true) ให้เรียบร้อยก่อน ค่อยเรียก OnSpawnFromPool()
            // เพราะบางคลาส (เช่น EnemyV1) ใช้ transform.position ปัจจุบันเป็นค่าเริ่มต้นในการคำนวณ state ตอน spawn
            if (instance.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnSpawnFromPool();
            }

            return instance;
        }

        /// <summary>
        /// คืน object กลับเข้า pool - ต้องเรียกแทนการใช้ Destroy() ทุกจุดสำหรับ object ที่มาจาก pool
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;

            if (!instance.TryGetComponent<PooledObjectTag>(out var tag) || tag.SourcePrefab == null)
            {
                // object นี้ไม่ได้มาจาก PoolManager.Get() (เช่นวางไว้ใน scene ตรงๆ ไม่ผ่าน pool)
                // ปิดไว้เฉยๆ กันพัง แต่ไม่ enqueue เข้า pool ไหน เพราะไม่รู้ว่าควรเข้า pool ไหน
                Debug.LogWarning($"PoolManager.Return: '{instance.name}' ไม่มี PooledObjectTag - ปิดไว้เฉยๆ ไม่ได้คืนเข้า pool");
                instance.SetActive(false);
                return;
            }

            if (instance.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnReturnToPool();
            }

            instance.SetActive(false);
            instance.transform.SetParent(poolContainers[tag.SourcePrefab]);

            pools[tag.SourcePrefab].Enqueue(instance);
        }
    }
}