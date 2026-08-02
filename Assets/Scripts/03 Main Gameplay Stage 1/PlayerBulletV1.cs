using UnityEngine;
using EnemySystem;

namespace PlayerSystem
{
    /// <summary>
    /// กระสุนของผู้เล่น - โครงสร้างเหมือน EnemyBullet เกือบทั้งหมด
    /// ต่างกันที่ ทิศทางเริ่มต้น (ขึ้นบน) และเป้าหมายที่ตรวจจับ (Enemy/Boss แทน Player)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerBulletV1 : MonoBehaviour
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private int damage = 1;

        private Vector2 direction = Vector2.up;
        private Rigidbody2D rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Kinematic เหตุผลเดียวกับ EnemyBullet: ไม่ต้องการแรงจากภายนอก
            // แค่ต้องการให้มี Rigidbody2D สำหรับให้ trigger event ทำงาน (เพราะ Enemy ไม่มี Rigidbody2D)
            rb.bodyType = RigidbodyType2D.Kinematic;

            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        public void SetDirection(Vector2 newDirection)
        {
            direction = newDirection.normalized;
        }

        void FixedUpdate()
        {
            Vector2 targetPosition = rb.position + direction * speed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // เช็คทั้ง Enemy และ Boss เพราะทั้งคู่ใช้ระบบเลือดเดียวกัน (component Enemy)
            // ป้องกันไม่ให้กระสุนผู้เล่นไปโดน EnemyBullet หรือกระสุนตัวเอง เพราะเช็คเฉพาะ tag พวกนี้เท่านั้น
            if (!other.CompareTag("Enemy") && !other.CompareTag("Boss")) return;

            if (other.TryGetComponent<EnemyV1>(out var enemy))
            {
                enemy.TakeDamage(damage);
            }

            // TODO: เหมือน EnemyBullet - ทีหลังเปลี่ยนเป็นคืนกระสุนเข้า pool แทน Destroy
            Destroy(gameObject);
        }
    }
}