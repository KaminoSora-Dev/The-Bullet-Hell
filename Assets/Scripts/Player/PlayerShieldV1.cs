using UnityEngine;
using PoolingSystem;
using EnemySystem;

namespace PlayerSystem
{
    /// <summary>
    /// สถานะภายในของโล่ - แยก "กำลังป้องกันอยู่จริง" (Active) ออกจาก "หมดฤทธิ์แล้วแต่ยังจางหายอยู่" (FadingOut)
    /// เพราะ IsActive ต้องเป็น false ทันทีที่หมดเวลาป้องกัน (ให้กด Spell ใหม่ได้/โดนโจมตีได้)
    /// แม้ภาพจะยังจางอยู่ก็ตาม
    /// </summary>
    public enum ShieldState
    {
        Inactive,
        Active,
        FadingOut
    }

    [RequireComponent(typeof(Collider2D))]
    public class PlayerShieldV1 : MonoBehaviour
    {
        [Header("Life Time")]
        [Tooltip("ระยะเวลาที่โล่ทำงาน (วินาที) ถ้า Activate() ถูกเรียกโดยไม่ระบุ duration (หรือระบุค่า <= 0) จะใช้ค่านี้แทน")]
        [SerializeField] private float defaultLifeTime = 2f;

        [Header("Fade Out (ตอนโล่หมดฤทธิ์)")]
        [Tooltip("เวลาที่ใช้จางหายไป (วินาที) หลังหมด Life Time ตั้งเป็น 0 = หายทันทีไม่จาง")]
        [SerializeField] private float fadeOutDuration = 0.3f;

        [Tooltip(
            "ลาก SpriteRenderer ของโล่มาใส่เพื่อให้ Fade Out ทำงาน (ปรับค่า Alpha) " +
            "ถ้าไม่ใส่ไว้ จะไม่มีการ Fade แค่ปิด GameObject ทันทีตอนหมดเวลาเหมือนพฤติกรรมเดิม"
        )]
        [SerializeField] private SpriteRenderer bodySprite;

        [Header("Animator (Optional - ไม่ใส่ก็ได้)")]
        [Tooltip(
            "ลาก Animator ของโล่มาใส่ ถ้าต้องการสั่ง Trigger ตอน Activate เช่น เล่นแอนิเมชันจังหวะเดียวค้างเฟรม " +
            "หรือ Trigger เข้า Loop State ถ้าไม่ใส่ไว้ ระบบจะไม่ยุ่งกับ Animator เลย " +
            "(ปล่อยให้ Idle/Default State ของ Animator เล่นเองตามปกติตอน GameObject Active ก็ได้ ไม่จำเป็นต้องใช้ช่องนี้)"
        )]
        [SerializeField] private Animator animator;
        [SerializeField] private string activateTrigger = "Activate";

        private Collider2D col;
        private float remainingTime;
        private float fadeTimer;

        public ShieldState State { get; private set; } = ShieldState.Inactive;

        /// <summary>
        /// true เฉพาะช่วงที่โล่ "กำลังป้องกันอยู่จริง" เท่านั้น (ไม่รวมช่วง Fade Out หลังหมดฤทธิ์)
        /// ใช้เช็คได้ทั้งฝั่ง Bomb() (กันกด Spell ซ้ำ) และฝั่ง Die() (กันตัวจาก Enemy/กระสุนที่หลุดเข้ามา)
        /// </summary>
        public bool IsActive => State == ShieldState.Active;

        void Awake()
        {
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// เปิดโล่ ระบุ duration เองได้ (เช่นจาก PlayerControllerV1.shieldDuration)
        /// หรือปล่อยว่าง/ใส่ค่า <= 0 เพื่อใช้ defaultLifeTime ที่ตั้งไว้ใน Inspector ของโล่เอง
        /// </summary>
        public void Activate(float duration = -1f)
        {
            // กันเรียกซ้ำระหว่างที่ยังทำงานอยู่ (PlayerControllerV1.Bomb() เช็คไว้ชั้นนอกแล้ว
            // แต่กันไว้อีกชั้นเผื่อมีจุดอื่นในอนาคตเรียก Activate() ตรงๆ)
            if (State == ShieldState.Active) return;

            remainingTime = duration > 0f ? duration : defaultLifeTime;

            State = ShieldState.Active;
            fadeTimer = 0f;

            gameObject.SetActive(true);
            if (col != null) col.enabled = true;

            SetAlpha(1f);

            if (animator != null)
                animator.SetTrigger(activateTrigger);
        }

        void Update()
        {
            switch (State)
            {
                case ShieldState.Active:
                    UpdateActive();
                    break;

                case ShieldState.FadingOut:
                    UpdateFadingOut();
                    break;
            }
        }

        private void UpdateActive()
        {
            remainingTime -= Time.deltaTime;
            if (remainingTime > 0f) return;

            // หมดเวลาป้องกันแล้ว - ปิด Collider ทันทีกันโล่ยังกันของระหว่างจาง
            // แต่ State เปลี่ยนเป็น FadingOut ไม่ใช่ Inactive ทันที เพื่อให้ภาพยังจางหายทีหลังได้
            State = ShieldState.FadingOut;
            fadeTimer = 0f;

            if (col != null) col.enabled = false;

            if (bodySprite == null || fadeOutDuration <= 0f)
                Deactivate();
        }

        private void UpdateFadingOut()
        {
            fadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(fadeTimer / fadeOutDuration);

            SetAlpha(1f - t);

            if (t >= 1f)
                Deactivate();
        }

        private void Deactivate()
        {
            State = ShieldState.Inactive;
            gameObject.SetActive(false);
        }

        private void SetAlpha(float alpha)
        {
            if (bodySprite == null) return;

            Color c = bodySprite.color;
            c.a = alpha;
            bodySprite.color = c;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("EnemyBullet")) return;

            // เปลี่ยนใหม่: ใช้ RequestDestroy() แทน PoolManager.Instance.Return() ตรงๆ
            // เพื่อให้กระสุนที่ผูก Death Animation ไว้เล่นแอนิเมชันตอนโดนโล่ดูดด้วย
            if (other.TryGetComponent<EnemyBulletV2>(out var bulletV2))
                bulletV2.RequestDestroy();
            else
                PoolManager.Instance.Return(other.gameObject);
        }
    }
}