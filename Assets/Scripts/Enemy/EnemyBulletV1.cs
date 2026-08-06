using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace EnemySystem
{
    /// <summary>
    /// กระสุนศัตรู - เคลื่อนที่เป็นเส้นตรงตามทิศที่กำหนด
    /// ต้องมี Rigidbody2D (Kinematic) ติดตัว เพราะ Player ไม่มี Rigidbody2D แล้ว
    /// (กฎ 2D Trigger ของ Unity: ต้องมีอย่างน้อยฝั่งใดฝั่งหนึ่งใน Collider คู่ที่ชนกันมี Rigidbody2D)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBulletV1 : MonoBehaviour, IPoolable
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
            // สำคัญ: ต้องขยับผ่าน rb.MovePosition() ใน FixedUpdate() เท่านั้น (ไม่ใช่ transform.position ใน Update())
            // เพราะ Rigidbody2D เป็น Kinematic ที่มีไว้เพื่อให้ physics engine sync ตำแหน่ง collider ให้ตรงกับ
            // ตำแหน่งจริงในจังหวะที่ physics step ทำงาน ถ้าแก้ transform.position ตรงๆ ใน Update() แทน
            // จะทำให้ trigger detection (OnTriggerEnter2D) หลุดจังหวะ/เพี้ยนได้ โดยเฉพาะตอนเฟรมเรตไม่คงที่
            // หรือกระสุนความเร็วสูง (อาจทะลุ collider โดยไม่ trigger เลย)
            Vector2 targetPosition = rb.position + direction * speed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("HiddenBorder"))
            {
                // ออกนอกจอไปแล้ว ไม่มีใครยิงโดนหรือชนอะไรทัน - คืนกลับ pool แทน SetActive(false) เฉยๆ
                // (SetActive(false) เฉยๆ จะทำให้ object นี้ "หาย" ไปจากทุก pool ไม่มีใครหยิบมันไปใช้ซ้ำได้อีก)
                PoolManager.Instance.Return(gameObject);
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

            PoolManager.Instance.Return(gameObject);
        }

        /// <summary>
        /// เรียกโดย PoolManager ทุกครั้งที่ดึงกระสุนนี้จาก pool มาใช้
        /// reset direction กลับเป็นค่าเริ่มต้นไว้ก่อนเป็นการป้องกัน (defensive) - จริงๆ แล้ว Spawner
        /// จะเรียก SetDirection() ทับอีกทีทันทีหลังจากนี้อยู่แล้วเสมอ แต่เผื่อวันหนึ่งมีจุดเรียกใช้ที่ลืมเซ็ตทิศทาง
        /// จะได้เห็นกระสุนพุ่งไปทิศเริ่มต้นที่คาดเดาได้ (ลงล่าง) แทนที่จะพุ่งไปทิศเก่าจากการใช้งานรอบที่แล้วแบบงงๆ
        /// </summary>
        public void OnSpawnFromPool()
        {
            direction = Vector2.down;
        }

        public void OnReturnToPool()
        {
            // ไม่มี state อื่นที่ต้องเคลียร์ตอนคืน pool - direction จะถูก reset ใหม่ตอน OnSpawnFromPool() อยู่แล้ว
        }
    }
}