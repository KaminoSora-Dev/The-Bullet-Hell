using UnityEngine;
using PoolingSystem;

namespace PlayerSystem
{
    /// <summary>
    /// โล่ป้องกันชั่วคราวที่เปิดใช้ตอนผู้เล่นใช้ Spell (Bomb) - ดูดกระสุนศัตรูที่ชนโล่ทิ้งไปตลอดช่วงเวลาที่เปิดอยู่
    ///
    /// วิธีติดตั้ง: สร้าง Prefab/GameObject ที่มี SpriteRenderer (รูปโล่) + CircleCollider2D
    /// วางเป็น "child object ของ Player" ใน Scene ไว้ล่วงหน้า แล้วลาก reference มาใส่ field
    /// "shield" ใน PlayerControllerV1
    ///
    /// ทำไมไม่ผ่าน PoolManager เหมือนกระสุน/Fragment: โล่นี้มีอยู่แค่ "ตัวเดียวต่อผู้เล่นตลอดทั้งเกม"
    /// ไม่มีจังหวะไหนที่ต้องมีหลาย instance พร้อมกัน (ต่างจากกระสุนที่ spawn ซ้ำๆ จำนวนมาก)
    /// การ pool จึงไม่ได้ช่วยประหยัด Instantiate/GC อะไรเพิ่มในเคสนี้ - วาง fixed instance ไว้เฉยๆ ง่ายกว่า
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerShieldV1 : MonoBehaviour
    {
        // เวลาที่เหลือก่อนโล่จะปิดตัวเอง - นับถอยหลังใน Update() ตามหลัก "ไม่ใช้ Coroutine" ของโปรเจกต์นี้
        private float remainingTime;
        private bool isActive;

        void Awake()
        {
            // บังคับ isTrigger ผ่านโค้ดเสมอ แทนที่จะพึ่งการติ๊กใน Inspector อย่างเดียว
            // เหตุผลเดียวกับ GrazeDetectorV1: ถ้าลืมติ๊ก โล่จะกลายเป็นของแข็งไปดันผู้เล่น/กระสุนแทน
            GetComponent<Collider2D>().isTrigger = true;

            // ปิดไว้ตั้งแต่ต้น กันโล่โผล่ค้างมาตั้งแต่เริ่ม Scene ทั้งที่ยังไม่ได้กด Bomb
            gameObject.SetActive(false);
        }

        /// <summary>
        /// เรียกจาก PlayerControllerV1.Bomb() ตอนใช้ spell สำเร็จ
        /// เปิดโล่และเริ่มนับถอยหลัง duration วินาที
        /// </summary>
        public void Activate(float duration)
        {
            remainingTime = duration;
            isActive = true;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!isActive) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                isActive = false;
                gameObject.SetActive(false);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // ดูดเฉพาะกระสุนศัตรู - ใช้ tag "EnemyBullet" ตัวเดียวกับที่ GrazeDetectorV1 เช็คอยู่แล้ว
            // ไม่ต้องสร้าง tag ใหม่ หรือไปแก้ EnemyBulletV1.cs ให้รู้จักโล่เพิ่ม (narrow fix - ไม่แตะไฟล์ที่เทสผ่านแล้ว)
            if (!other.CompareTag("EnemyBullet")) return;

            // คืนกลับ pool แทน Destroy ตรงๆ เพราะกระสุนตัวนี้มาจาก PoolManager เหมือนกันทุกนัด
            PoolManager.Instance.Return(other.gameObject);
        }
    }
}