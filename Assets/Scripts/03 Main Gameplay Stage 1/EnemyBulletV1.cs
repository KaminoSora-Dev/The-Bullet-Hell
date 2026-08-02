using UnityEngine;
using PlayerSystem;

namespace EnemySystem
{
    /// <summary>
    /// กระสุนศัตรู - เคลื่อนที่เป็นเส้นตรงตามทิศที่กำหนด
    /// ต้องมี Rigidbody2D (Kinematic) ติดตัว เพราะ Player ไม่มี Rigidbody2D แล้ว
    /// (กฎ 2D Trigger ของ Unity: ต้องมีอย่างน้อยฝั่งใดฝั่งหนึ่งใน Collider คู่ที่ชนกันมี Rigidbody2D)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBulletV1 : MonoBehaviour
    {
        [SerializeField] private float speed = 6f;

        // ทิศทางที่กระสุนจะพุ่งไป - ตั้งค่าจาก Spawner ตอนสร้างกระสุน (ดู SetDirection ด้านล่าง)
        private Vector2 direction = Vector2.down;

        private Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Bullet ไม่ต้องการแรงโน้มถ่วงหรือแรงกระทำใดๆ เหมือนกับที่อธิบายไปตอน Player
            // Kinematic พอ เพราะแค่ต้องการให้มันมี Rigidbody2D สำหรับ trigger event เท่านั้น
            rb.bodyType = RigidbodyType2D.Kinematic;

            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        /// <summary>
        /// เรียกจาก Spawner ตอนยิงกระสุนออกมา เพื่อกำหนดทิศทาง
        /// แยกเป็น method ต่างหากแทนที่จะ hardcode ทิศทางในนี้ เพราะกระสุนแต่ละแบบ (pattern)
        /// ต้องยิงคนละทิศ - ให้ตัวสร้าง pattern เป็นคนสั่งแทน
        /// </summary>
        public void SetDirection(Vector2 newDirection)
        {
            direction = newDirection.normalized;
        }

        void FixedUpdate()
        {
            // ยังใช้ MovePosition เหมือนเดิมสำหรับกระสุน เพราะกระสุนมี Rigidbody2D จริง
            // (ต่างจาก Player ที่เอา Rigidbody2D ออกไปแล้ว)
            Vector2 targetPosition = rb.position + direction * speed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("HiddenBorder"))
            {
                // ออกนอกจอไปแล้ว ไม่มีใครยิงโดนหรือชนอะไรทัน - ปิดตัวไว้ก่อนแทนที่จะ Destroy
                // ไม่ reset ตำแหน่ง/ทิศทางตรงนี้ เพราะตอนถูกดึงกลับมาใช้ใหม่ (pool manager ที่จะทำทีหลัง)
                // จะเป็นคนกำหนดตำแหน่ง/ทิศทางใหม่ให้เองอยู่แล้ว ปล่อยค้างไว้ตรงนี้ไม่มีผลอะไร
                gameObject.SetActive(false);
                return;
            }

            // ใช้ CompareTag แทน GetComponent<PlayerController>() ตรงๆ
            // เพราะ CompareTag เร็วกว่า (ไม่ต้องเสิร์ช component) และเช็คได้ก่อนว่าเป็น Player จริงไหม
            // ก่อนจะเสีย cost ไป GetComponent ทีหลัง
            if (!other.CompareTag("Player")) return;

            // ใช้ TryGetComponent กัน error ถ้า tag ตั้งผิด object แล้วไม่มี PlayerController ติดอยู่จริง
            if (other.TryGetComponent<PlayerControllerV1>(out var player))
            {
                player.Die();
            }

            // TODO: ตอนนี้ Destroy ไปเลยก่อน ทีหลังพอทำ Object Pool เต็มระบบแล้ว
            // ให้เปลี่ยนตรงนี้เป็น gameObject.SetActive(false) เหมือน HiddenBorder ด้านบนแทน
            // ไม่งั้น Destroy/Instantiate รัวๆ ตอนกระสุนเยอะๆ จะกิน performance หนักจาก GC
            Destroy(gameObject);
        }
    }
}