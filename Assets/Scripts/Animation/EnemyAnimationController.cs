using UnityEngine;
using EnemySystem;
using PoolingSystem;

namespace AnimationSystem
{
    /// <summary>
    /// เสียบ Animator ให้ Enemy (Optional Component - ไม่ต้องใส่ก็ได้)
    ///
    /// ไม่ต้องแก้ EnemyV1.cs เลย - Component นี้แค่ฟัง event ที่ EnemyV1 มีอยู่แล้ว
    /// (OnDied) แล้วสั่ง Animator เอาเอง
    ///
    /// --- Idle ---
    /// ปล่อยให้เป็น Default State ของ Animator Controller เอง ไม่มีการสั่ง Trigger ใดๆ
    /// ตอน Spawn ถ้า Animator Controller ไม่ได้ตั้ง Idle Animation ไว้ Enemy จะโชว์เป็น
    /// Sprite นิ่ง ๆ ตามปกติ (ไม่ error อะไร)
    ///
    /// --- Death ---
    /// Play With Blink = true  -> เล่น Death Animation ควบคู่ไปกับระบบกระพริบแดง (Blink) เดิม
    ///                             ตัว Enemy จะถูก Return เข้า Pool ตอน Blink ครบรอบเหมือนเดิมทุกอย่าง
    ///
    /// Play With Blink = false -> Animator คุมจังหวะตายเองทั้งหมด (ไม่กระพริบ)
    ///                             ตัว Enemy จะถูก Return เข้า Pool หลังจากผ่านไป
    ///                             Death Anim Duration วินาที ต้องตั้งเลขนี้ให้ตรงกับ
    ///                             ความยาว Clip Death จริง ๆ ใน Animator
    ///
    /// ถ้าไม่ลาก Animator ใส่ในช่อง -> EnemyV1 จะทำงานแบบเดิมทุกอย่าง (Blink อย่างเดียว
    /// เหมือนก่อนมี Component นี้)
    ///
    /// --- Movement ---
    /// เช็คว่า Enemy "ขยับจริง" ไปทางซ้าย/ขวาไหม โดยเทียบตำแหน่งจริงระหว่าง 2 เฟรม
    /// (แพทเทิร์นเดียวกับ Player/BossAnimationController) แล้วสั่ง Bool Parameter (IsMovingLeft / IsMovingRight)
    /// เผื่อ Enemy มีท่าเอียงตอนเคลื่อนที่ตาม Waypoint/Path
    ///
    /// หมายเหตุเฉพาะ Enemy: ต่างจาก Player/Boss ตรงที่ Enemy ถูก "เทเลพอร์ต" ไปตำแหน่ง Spawn ใหม่
    /// ทุกครั้งที่ดึงจาก Pool มาใช้ซ้ำ (ไม่ใช่แค่ครั้งแรกตอนสร้าง) ถ้าจับตำแหน่งเริ่มต้นแค่ตอน Start()
    /// (ซึ่งรันครั้งเดียวตลอดอายุ Object) จะเกิด Delta ก้อนใหญ่ผิดปกติ 1 เฟรมตอน Spawn รอบถัดๆ ไป
    /// เพราะเทียบตำแหน่งเก่าก่อนถูก Return เข้า Pool กับตำแหน่ง Spawn ใหม่ - โค้ดนี้เลย reset ใน OnEnable()
    /// แทน (รันทุกครั้งที่ Object ถูกเปิดใช้งานใหม่จาก Pool) กัน Bool กระพริบผิดจังหวะตอน Spawn
    /// </summary>
    [RequireComponent(typeof(EnemyV1))]
    public class EnemyAnimationController : MonoBehaviour
    {
        [Header("Animator (ไม่ใส่ก็ได้)")]
        [Tooltip(
            "ลาก Animator ของ Enemy ตัวนี้มาใส่\n" +
            "ถ้าปล่อยว่างไว้ - จะไม่มีผลอะไรเลย EnemyV1 ใช้ระบบ Blink เดิม"
        )]
        [SerializeField] private Animator animator;

        [Header("Death")]
        [Tooltip("ชื่อ Trigger ใน Animator Controller ที่จะสั่งตอน Enemy ตาย")]
        [SerializeField] private string deathTrigger = "Death";

        [Tooltip(
            "ติ๊ก = เล่น Death Animation ควบคู่กับ Blink กระพริบเดิม (Blink เป็นคนคุมเวลา Return เข้า Pool)\n" +
            "ไม่ติ๊ก = ให้ Animator คุมจังหวะตายเองทั้งหมด (ไม่กระพริบ)"
        )]
        [SerializeField] private bool playWithBlink = true;

        [Tooltip(
            "ใช้ตอน Play With Blink = false เท่านั้น\n" +
            "เวลา (วินาที) ที่จะรอ ก่อน Return Enemy ตัวนี้กลับเข้า Pool\n" +
            "ตั้งให้ตรงกับความยาว Clip ของ Death Animation จริง ๆ"
        )]
        [SerializeField] private float deathAnimDuration = 1f;

        [Header("Movement")]
        [SerializeField] private string movingLeftParam = "IsMovingLeft";
        [SerializeField] private string movingRightParam = "IsMovingRight";

        [Tooltip(
            "ระยะที่ตำแหน่ง X ต้องเปลี่ยนไปต่อเฟรม (หน่วย Unity Unit) ถึงจะนับว่า \"ขยับจริง\"\n" +
            "ตั้งไว้กันความสั่นเล็กน้อยจาก Floating Point ไม่ให้เด้ง True/False สลับถี่ๆ ตอนยืนนิ่ง\n" +
            "ค่ายิ่งสูง ยิ่งต้องขยับเร็ว/ไกลกว่าเดิมถึงจะติด Moving (ปรับได้ถ้ารู้สึกไวไปหรือช้าไป)"
        )]
        [SerializeField] private float moveThreshold = 0.001f;

        private EnemyV1 enemy;
        private bool isPlayingDeath;
        private float deathTimer;
        private Vector3 lastPosition;

        private void Awake()
        {
            enemy = GetComponent<EnemyV1>();
        }

        private void OnEnable()
        {
            enemy.OnDied += HandleDied;

            // เพิ่มใหม่: reset ตำแหน่งอ้างอิงตอนถูกเปิดใช้งาน (ทั้งครั้งแรกตอนสร้าง และทุกครั้งที่ดึงจาก Pool มาใช้ซ้ำ)
            // กัน Delta ก้อนใหญ่ผิดปกติจากการเทเลพอร์ตไป Spawn Point ใหม่
            lastPosition = transform.position;
        }

        private void OnDisable()
        {
            enemy.OnDied -= HandleDied;
        }

        private void HandleDied()
        {
            isPlayingDeath = false;

            // ไม่ได้เสียบ Animator ไว้ -> ไม่ยุ่งอะไร ปล่อยให้ Blink เดิมทำงานตามปกติ
            if (animator == null)
                return;

            animator.SetTrigger(deathTrigger);

            if (!playWithBlink)
            {
                enemy.SuppressBuiltInDeathVisual = true;
                isPlayingDeath = true;
                deathTimer = 0f;
            }
        }

        private void Update()
        {
            if (!isPlayingDeath)
                return;

            deathTimer += Time.deltaTime;

            if (deathTimer >= deathAnimDuration)
            {
                isPlayingDeath = false;

                if (PoolManager.Instance != null)
                    PoolManager.Instance.Return(gameObject);
            }
        }

        private void LateUpdate()
        {
            // ใช้ LateUpdate เพราะต้องรอให้ EnemyV1 ขยับตัวจริงใน Update()/FixedUpdate() ก่อน
            // แล้วค่อยมาอ่านตำแหน่งล่าสุด ไม่งั้นค่าที่ได้จะช้าไปหนึ่งเฟรม
            if (animator == null) return;
            if (isPlayingDeath) return; // เพิ่มใหม่: กำลังเล่น Death Animation อยู่ ไม่ต้องอัพเดต Movement Bool ต่อ

            float deltaX = transform.position.x - lastPosition.x;

            bool movingLeft = deltaX < -moveThreshold;
            bool movingRight = deltaX > moveThreshold;

            animator.SetBool(movingLeftParam, movingLeft);
            animator.SetBool(movingRightParam, movingRight);

            lastPosition = transform.position;
        }
    }
}