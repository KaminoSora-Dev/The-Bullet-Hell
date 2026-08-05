using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace ItemSystem
{
    /// <summary>
    /// Fragment ที่ดรอปจาก Enemy ตอนตาย - ลอยลงมาปกติ แต่ถ้าผู้เล่นเข้าใกล้ในระยะ homingRadius
    /// จะเปลี่ยนมาบินเข้าหาผู้เล่นแทน (ฟีลแบบไอเทมเกม bullet hell ทั่วไปที่ "โดนดูด" เข้าตัวละคร)
    ///
    /// โครงสร้างเหมือน EnemyBulletV1/PlayerBulletV1 เกือบทั้งหมด (Rigidbody2D Kinematic + Trigger)
    /// เพราะใช้กฎ 2D Trigger เดียวกัน: ต้องมี Rigidbody2D อย่างน้อยฝั่งใดฝั่งหนึ่งในคู่ collider ถึงจะ trigger event ทำงาน
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class FragmentPickupV1 : MonoBehaviour, IPoolable
    {
        [Header("Value")]
        [Tooltip("จำนวน Fragment ที่ได้ต่อ 1 ชิ้น (ปกติ = 1 แต่เผื่อไว้ถ้าอยากทำไอเทมพิเศษที่ได้ทีละหลายหน่วย)")]
        [SerializeField] private int fragmentValue = 1;

        [Header("Movement - Homing")]
        [Tooltip("ระยะจากผู้เล่นที่จะเริ่มโดนดูด - ก่อนหน้านั้น Fragment จะอยู่นิ่งกับที่ตรงจุดที่ดรอปเป๊ะ ไม่ตกตามแรงโน้มถ่วงแล้ว")]
        [SerializeField] private float homingRadius = 3f;
        [Tooltip("ความเร็วตอนถูกดูดเข้าหาผู้เล่นแล้ว")]
        [SerializeField] private float homingSpeed = 8f;

        private Rigidbody2D rb;
        private Transform playerTransform;
        private bool isHoming;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // กลับไปเป็น Kinematic เหมือนเดิม เพราะไม่ต้องการแรงโน้มถ่วงหรือแรงอื่นใดๆ อีกต่อไป
            // (ก่อนหน้านี้เคยเปลี่ยนเป็น Dynamic + gravityScale เพื่อให้ตกแบบ physics แต่ตอนนี้ดรอปนิ่งอยู่กับที่แทน)
            rb.bodyType = RigidbodyType2D.Kinematic;
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        void Start()
        {
            // cache หา Player ครั้งเดียวตอน spawn เหมือน pattern ที่ใช้ใน EnemyShooterV1
            // (ไม่ FindGameObjectWithTag ทุกเฟรม เพราะเปลืองและ Player ไม่ได้เปลี่ยน object กลางเกม)
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        void FixedUpdate()
        {
            if (playerTransform == null) return; // หา Player ไม่เจอ - อยู่นิ่งกับที่ไปก่อน กันพัง

            if (!isHoming)
            {
                float distanceToPlayer = Vector2.Distance(rb.position, playerTransform.position);

                // เข้าโหมด homing แล้วไม่ออกกลับ แม้ผู้เล่นจะขยับหนีไปไกลกว่า radius อีกครั้งก็ตาม
                // เพราะถ้าปล่อยให้สลับ homing <-> นิ่ง ไปมา จะรู้สึกกระตุกและ "หลอน" เวลาผู้เล่นขยับแกว่งๆ ใกล้ขอบระยะพอดี
                if (distanceToPlayer <= homingRadius)
                {
                    isHoming = true;
                }
                else
                {
                    return; // ยังไม่เข้าระยะ - อยู่นิ่งกับที่เป๊ะ ไม่ต้องขยับอะไรเลย
                }
            }

            Vector2 direction = ((Vector2)playerTransform.position - rb.position).normalized;
            rb.MovePosition(rb.position + direction * homingSpeed * Time.fixedDeltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("HiddenBorder"))
            {
                PoolManager.Instance.Return(gameObject);
                return;
            }

            if (!other.CompareTag("Player")) return;

            if (PlayerStatsManager.Instance != null)
            {
                PlayerStatsManager.Instance.AddFragment(fragmentValue);
            }

            PoolManager.Instance.Return(gameObject);
        }

        /// <summary>
        /// reset ทุก state ที่เกี่ยวกับ "รอบการดรอปนี้" - สำคัญสุดคือ isHoming เพราะถ้าลืม reset
        /// Fragment ที่เคยโดนดูดเข้าผู้เล่นไปแล้วในรอบก่อน พอถูกดึงมาใช้ใหม่จะเข้าสถานะ "กำลังโดนดูด" ตั้งแต่ spawn ทันที
        /// ทั้งที่ควรจะลอยตกลงมาปกติก่อนแบบของใหม่
        /// </summary>
        public void OnSpawnFromPool()
        {
            isHoming = false;
            rb.position = transform.position; // sync ให้ rb กับตำแหน่ง spawn ตรงกันเป๊ะตั้งแต่เฟรมแรก (กันเคส MovePosition เพี้ยนเฟรมแรก)
        }

        public void OnReturnToPool()
        {
        }
    }
}