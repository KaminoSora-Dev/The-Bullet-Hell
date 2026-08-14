using UnityEngine;
using EnemySystem;
using PoolingSystem;
using GameSystem;
using CombatSystem;

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

        // ============================================================
        // เพิ่มใหม่: Death Animation (Optional) - แพทเทิร์นเดียวกับ EnemyBulletV2
        // ============================================================
        [Header("Death Animation (Optional)")]
        [Tooltip("ถ้าลากมาใส่ - ตอนกระสุนยิงโดนศัตรู จะเล่นแอนิเมชันนี้ก่อนค่อยกลับ Pool จริง " +
                 "ถ้าไม่ใส่ไว้ กระสุนจะกลับ Pool ทันทีเหมือนพฤติกรรมเดิมทุกประการ")]
        [SerializeField] private Animator deathAnimator;
        [Tooltip("ชื่อ Trigger parameter บน Animator ที่จะสั่งตอนกระสุนโดนทำลาย")]
        [SerializeField] private string deathTrigger = "Death";

        private Vector2 direction = Vector2.up;
        private Rigidbody2D rb;
        private CircleCollider2D col;
        private bool isDying;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Kinematic เหตุผลเดียวกับ EnemyBullet: ไม่ต้องการแรงจากภายนอก
            // แค่ต้องการให้มี Rigidbody2D สำหรับให้ trigger event ทำงาน (เพราะ Enemy ไม่มี Rigidbody2D)
            rb.bodyType = RigidbodyType2D.Kinematic;

            col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
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
            if (isDying) return; // เพิ่มใหม่: กำลังเล่น Death Animation อยู่ (collider ปิดไปแล้วด้วยปกติ)

            if (other.CompareTag("HiddenBorder"))
            {
                ReturnToPoolNow(); // หายนอกจอ ไม่ใช่โดนทำลาย - ไม่เล่นแอนิเมชัน (เหมือน EnemyBulletV2)
                return;
            }

            // เช็คทั้ง Enemy และ Boss เพราะทั้งคู่ต้องโดนกระสุนผู้เล่นได้เหมือนกัน
            // ป้องกันไม่ให้กระสุนผู้เล่นไปโดน EnemyBullet หรือกระสุนตัวเอง เพราะเช็คเฉพาะ tag พวกนี้เท่านั้น
            if (!other.CompareTag("Enemy") && !other.CompareTag("Boss")) return;

            // ใช้ IDamageable แทนการเช็ค EnemyV1 ตรงๆ เพราะตอนนี้มี BossV1 ที่เป็นคนละคลาสแยกจาก EnemyV1
            // แต่ก็ต้องรับดาเมจจากกระสุนผู้เล่นได้เหมือนกัน - เช็ค interface เดียวจบ ไม่ต้องมาแก้ไฟล์นี้อีก
            // ทุกครั้งที่โปรเจกต์เพิ่มศัตรู/บอสชนิดใหม่ในอนาคต
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                // ส่ง gameObject ตัวเองเป็น attacker เพื่อให้ฝั่งที่โดนดาเมจเช็ค tag "PlayerBullet" ได้ตอนตัดสินให้คะแนน
                damageable.TakeDamage(damage, gameObject);

                // คะแนนตอน "ยิงโดน" ให้ทันทีตรงนี้เลย ไม่ต้องรอผลว่าเป้าหมายตายไหม
                // เพราะเป็นคนละก้อนกับคะแนนตอน "ฆ่าได้" ซึ่งอยู่ฝั่งเป้าหมายเอง (EnemyV1.Die() / BossV1.EndCurrentPhase())
                if (hitScoreValue != 0 && ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.AddScore(hitScoreValue);
                }
            }

            RequestDestroy(); // เปลี่ยนจาก PoolManager.Instance.Return(gameObject) ตรงๆ
        }

        /// <summary>
        /// เพิ่มใหม่: เรียกจุดนี้แทน PoolManager.Instance.Return(gameObject) ตรงๆ ทุกที่ที่ต้อง "ทำลาย" กระสุนนี้
        /// (ยกเว้นตอนชน HiddenBorder ที่ยัง Return ตรงๆ เหมือนเดิม เพราะเป็นการหายออกนอกจอ ไม่ใช่ถูกทำลาย)
        /// มี deathAnimator ผูกไว้ -> เล่นแอนิเมชันก่อน แล้วรอ Animation Event เรียก OnDeathAnimationComplete()
        /// ไม่มี deathAnimator -> Return ทันทีเหมือนพฤติกรรมเดิม (กระสุนที่ไม่ได้ผูกแอนิเมชันไว้ไม่พัง)
        /// </summary>
        public void RequestDestroy()
        {
            if (isDying) return;

            if (deathAnimator == null)
            {
                ReturnToPoolNow();
                return;
            }

            isDying = true;
            if (col != null) col.enabled = false;

            deathAnimator.SetTrigger(deathTrigger);
        }

        /// <summary>
        /// ผูกกับ Animation Event ที่เฟรมสุดท้ายของคลิป Death ใน Animator
        /// (เปิด Animation clip -> คลิกเฟรมสุดท้าย -> Add Event -> เลือกฟังก์ชันนี้)
        /// </summary>
        public void OnDeathAnimationComplete()
        {
            ReturnToPoolNow();
        }

        private void ReturnToPoolNow()
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        public void OnSpawnFromPool()
        {
            direction = Vector2.up;

            // เพิ่มใหม่: reset สถานะตาย + เปิด collider กลับ + rebind Animator กันค้าง state เดิม
            isDying = false;
            if (col != null) col.enabled = true;

            if (deathAnimator != null)
            {
                deathAnimator.Rebind();
                deathAnimator.Update(0f);
            }
            
        }

        public void OnReturnToPool()
        {
            isDying = false; // เพิ่มใหม่
        }
    }
}