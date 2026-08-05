using UnityEngine;

namespace PoolingSystem
{
    /// <summary>
    /// ติดอัตโนมัติกับทุก instance ที่ PoolManager สร้างขึ้น เก็บ reference ว่า
    /// "ตัวเองเป็น instance ที่มาจาก prefab ตัวไหน" เพื่อให้ตอน Return() รู้ว่าต้องคืนเข้า queue ไหน
    ///
    /// ไม่ใส่ field นี้ไว้ใน PoolManager โดยตรง (เช่น Dictionary&lt;GameObject instance, GameObject prefab&gt;)
    /// เพราะถ้า instance ถูก Destroy โดยไม่ได้ผ่าน PoolManager (เช่นเปลี่ยน scene กะทันหัน) Dictionary จะมี entry ค้าง
    /// เป็น memory leak เล็กๆ สะสมได้ - ให้ข้อมูลนี้ติดไปกับตัว object เองจะปลอดภัยกว่า (object หายไป ข้อมูลก็หายไปด้วย)
    /// </summary>
    public class PooledObjectTag : MonoBehaviour
    {
        public GameObject SourcePrefab { get; set; }
    }
}
