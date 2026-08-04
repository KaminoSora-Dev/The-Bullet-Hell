using UnityEngine;
using EnemySystem;
using PoolingSystem;
using GameSystem;

namespace PlayerSystem
{
    /// <summary>
    /// กระสุนของผู้เล่น - โครงสร้างเหมือน EnemyBullet เกือบทั้งหมด
    /// ต่างกันที่ ทิศทางเริ่มต้น (ขึ้นบน) และเป้าหมายที่ตรวจจับ (Enemy/Boss แทน Player)
    ///
    /// สำคัญ: Prefab ของกระสุนนี้ต้องตั้ง Tag เป็น "PlayerBullet" ใน Inspector ด้วย
    /// เพราะ EnemyV1.TakeDamage() เช็ค tag นี้เพื่อตัดสินว่าให้คะแนนตอนคิลไหม (ดูคอมเมนต์ใน EnemyV1.cs)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerBulletV1 : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speed = 10f;
        [SerializeField] private int damage = 1;

        [Header("Score")]
        [Tooltip("คะแนนที่ได้ทุกครั้งที่ยิงโดนศัตรู (ไม่ว่าจะฆ่าได้หรือไม่ก็ตาม) - แยกก้อนกับคะแนนตอน \"ฆ่าได้\" ที่อยู่ใน EnemyV1.scoreValue " +
                 "สองก้อนนี้บวกสะสมด้วยกันได้ ตั้งเป็น 0 ถ้าไม่อยากให้คะแนนตอนแค่โดน")]
        [SerializeField] private int hitScoreValue = 10;

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
            if (other.CompareTag("HiddenBorder"))
            {
                PoolManager.Instance.Return(gameObject);
                return;
            }

            // เช็คทั้ง Enemy และ Boss เพราะทั้งคู่ใช้ระบบเลือดเดียวกัน (component Enemy)
            // ป้องกันไม่ให้กระสุนผู้เล่นไปโดน EnemyBullet หรือกระสุนตัวเอง เพราะเช็คเฉพาะ tag พวกนี้เท่านั้น
            if (!other.CompareTag("Enemy") && !other.CompareTag("Boss")) return;

            if (other.TryGetComponent<EnemyV1>(out var enemy))
            {
                // ส่ง gameObject ตัวเองเป็น attacker เพื่อให้ EnemyV1 เช็ค tag "PlayerBullet" ได้ตอนตัดสินให้คะแนน
                enemy.TakeDamage(damage, gameObject);

                // คะแนนตอน "ยิงโดน" ให้ทันทีตรงนี้เลย ไม่ต้องรอผลว่าศัตรูตายไหม
                // เพราะเป็นคนละก้อนกับคะแนนตอน "ฆ่าได้" ซึ่งอยู่ใน EnemyV1.Die()
                if (hitScoreValue != 0 && ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.AddScore(hitScoreValue);
                }
            }

            PoolManager.Instance.Return(gameObject);
        }

        public void OnSpawnFromPool()
        {
            direction = Vector2.up;
        }

        public void OnReturnToPool()
        {
        }
    }
}