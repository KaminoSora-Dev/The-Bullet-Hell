using UnityEngine;
using GameSystem;

namespace PlayerSystem
{
    /// <summary>
    /// ตรวจจับ "Graze" (กระสุนเฉียดตัวแบบไม่โดน) แล้วบวกคะแนนให้ผู้เล่นตรงๆ ทันที
    /// (ตามที่ตกลงไว้ - ไม่ทำระบบ Value/Power แบบ Touhou ต้นฉบับ ให้ Graze ไปเพิ่ม Score เลย)
    ///
    /// วิธีติดตั้งสำคัญมาก: สคริปต์นี้ต้องอยู่บน "GameObject ลูกแยกต่างหาก" ของผู้เล่น
    /// ไม่ใช่ตัว Player object เดียวกับ hitbox ที่ตรวจการตาย! เหตุผล:
    /// - ต้องใช้ Collider2D วงใหญ่กว่า hitbox จริงมาก (ตามคอนเซปต์ graze ของบูลเลตเฮล คือระยะ "เฉียด" ต้องกว้างกว่าระยะ "โดน")
    /// - ต้องตั้ง Tag ของ GameObject นี้เป็นอย่างอื่นที่ไม่ใช่ "Player"
    ///   เพราะ EnemyBulletV1.OnTriggerEnter2D เช็ค other.CompareTag("Player") แล้วเรียก Die() ทันที
    ///   ถ้า graze collider ใช้ tag "Player" ด้วย จะกลายเป็นกระสุนที่แค่ "เฉียด" ทำให้ผู้เล่นตายจริงๆ โดยไม่ตั้งใจ
    ///
    /// ไม่ต้องมี Rigidbody2D ติดที่ตัว graze object นี้เอง เพราะกฎ 2D Trigger ของ Unity
    /// ("ต้องมีฝั่งใดฝั่งหนึ่งมี Rigidbody2D") ถูกฝั่งกระสุน (EnemyBulletV1) ที่มี Rigidbody2D อยู่แล้วเป็นคนรับผิดชอบไปแทน
    /// </summary>
    public class GrazeDetectorV1 : MonoBehaviour
    {
        [Tooltip("คะแนนที่ได้ต่อ 1 ครั้งที่เฉียดกระสุน 1 นัด")]
        [SerializeField] private int grazeScoreValue = 10;

        void Awake()
        {
            // บังคับ isTrigger = true ผ่านโค้ดเสมอ แทนที่จะพึ่งการติ๊กใน Inspector อย่างเดียว
            // เพราะถ้าลืมติ๊ก (สาเหตุที่ Graze ไม่ทำงานได้บ่อยที่สุด) Collider จะกลายเป็นของแข็งไปดันผู้เล่น/กระสุนแทน
            // ไม่ยิง OnTriggerEnter2D เลย ผู้เล่นก็จะรู้สึกเหมือน "โดนกระสุนศัตรูดันตัว" แปลกๆ ด้วย
            if (TryGetComponent<Collider2D>(out var col))
            {
                col.isTrigger = true;
            }
            else
            {
                Debug.LogWarning("GrazeDetectorV1: GameObject นี้ไม่มี Collider2D ติดอยู่เลย - Graze จะไม่ทำงาน");
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // ต้อง tag กระสุนศัตรูเป็น "EnemyBullet" ใน Prefab ด้วย (แยกจาก tag ที่ใช้เช็คชนกันตายจริง)
            if (!other.CompareTag("EnemyBullet")) return;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(grazeScoreValue);
            }

            // นับจำนวนครั้งสะสมแยกจากคะแนน - PlayerStatsManager เก็บเป็น "จำนวนครั้ง" ไว้โชว์ผ่าน UI ได้ในอนาคต
            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.AddGraze(1);
            }

            // ไม่ต้องกันนับซ้ำเพิ่มเติม: OnTriggerEnter2D ยิงแค่ครั้งเดียวตอน "เข้า" การซ้อนทับ
            // (ไม่ใช่ OnTriggerStay2D ที่ยิงทุกเฟรมระหว่างซ้อนทับ) กระสุน 1 นัดจึงเฉียดได้แค่ครั้งเดียวต่อการเข้าใกล้ 1 รอบอยู่แล้ว
        }
    }
}