using UnityEngine;
using BossSystem;
using PoolingSystem;

namespace AnimationSystem
{
    /// <summary>
    /// เสียบ Animator ให้ Boss (Optional Component - ไม่ต้องใส่ก็ได้)
    ///
    /// ไม่ต้องแก้ BossV2.cs เลย - Component นี้แค่ฟัง event ที่ BossV2 มีอยู่แล้ว
    /// (OnBossDefeated) แล้วสั่ง Animator เอาเอง เป็น Component เดียวกับที่ comment
    /// ใน BossV2.cs (SuppressBuiltInDeathVisual) เตรียมช่องไว้ให้ตั้งแต่แรก
    ///
    /// --- Idle ---
    /// ปล่อยให้เป็น Default State ของ Animator Controller เอง ไม่มีการสั่ง Trigger ใดๆ
    ///
    /// --- Death (ตอนจบ Phase สุดท้าย / Boss พ่ายแพ้) ---
    /// Play With Blink = true  -> เล่น Death Animation ควบคู่ไปกับระบบกระพริบขาว (Blink) เดิม
    ///                             ตัว Boss จะถูก Return เข้า Pool ตอน Blink ครบรอบเหมือนเดิม
    ///
    /// Play With Blink = false -> Animator คุมจังหวะตายเองทั้งหมด (ไม่กระพริบ)
    ///                             ตัว Boss จะถูก Return เข้า Pool หลังจากผ่านไป
    ///                             Death Anim Duration วินาที ต้องตั้งเลขนี้ให้ตรงกับ
    ///                             ความยาว Clip Death จริง ๆ ใน Animator
    ///
    /// หมายเหตุ: ถ้าตั้ง Load Next Scene On Defeat ไว้ใน BossV2 อยู่แล้ว การเปลี่ยน Scene
    /// จะเริ่มทันทีที่ OnBossDefeated ยิง (ไม่รอ Death Animation เล่นจบ) เพราะเป็น
    /// พฤติกรรมเดิมของ BossV2/GameFlowManager ที่ยังไม่ได้แก้ในรอบนี้
    ///
    /// ถ้าไม่ลาก Animator ใส่ในช่อง -> BossV2 จะทำงานแบบเดิมทุกอย่าง (Blink อย่างเดียว)
    ///
    /// --- Movement ---
    /// เช็คว่า Boss "ขยับจริง" ไปทางซ้าย/ขวาไหม โดยเทียบตำแหน่งจริงระหว่าง 2 เฟรม
    /// (แพทเทิร์นเดียวกับ PlayerAnimationController) แล้วสั่ง Bool Parameter (IsMovingLeft / IsMovingRight)
    /// เผื่อ Boss มีท่าเอียงตอนเคลื่อนที่ระหว่าง Entry/Phase Transition/Movement Step
    /// ทั้งสอง Bool เป็น false พร้อมกัน = ไม่ได้ขยับซ้าย-ขวาเลย (รวมถึงตอน Defeated ที่ BossV2 หยุดขยับเองอยู่แล้ว
    /// ไม่ต้องเช็คสถานะเพิ่ม)
    /// </summary>
    [RequireComponent(typeof(BossV2))]
    public class BossAnimationController : MonoBehaviour
    {
        [Header("Animator (ไม่ใส่ก็ได้)")]
        [Tooltip(
            "ลาก Animator ของ Boss ตัวนี้มาใส่\n" +
            "ถ้าปล่อยว่างไว้ - จะไม่มีผลอะไรเลย BossV2 ใช้ระบบ Blink เดิม"
        )]
        [SerializeField] private Animator animator;

        [Header("Death")]
        [Tooltip("ชื่อ Trigger ใน Animator Controller ที่จะสั่งตอน Boss พ่ายแพ้")]
        [SerializeField] private string deathTrigger = "Death";

        [Tooltip(
            "ติ๊ก = เล่น Death Animation ควบคู่กับ Blink กระพริบเดิม (Blink เป็นคนคุมเวลา Return เข้า Pool)\n" +
            "ไม่ติ๊ก = ให้ Animator คุมจังหวะตายเองทั้งหมด (ไม่กระพริบ)"
        )]
        [SerializeField] private bool playWithBlink = true;

        [Tooltip(
            "ใช้ตอน Play With Blink = false เท่านั้น\n" +
            "เวลา (วินาที) ที่จะรอ ก่อน Return Boss ตัวนี้กลับเข้า Pool\n" +
            "ตั้งให้ตรงกับความยาว Clip ของ Death Animation จริง ๆ"
        )]
        [SerializeField] private float deathAnimDuration = 1.5f;

        [Header("Movement")]
        [SerializeField] private string movingLeftParam = "IsMovingLeft";
        [SerializeField] private string movingRightParam = "IsMovingRight";

        [Tooltip(
            "ระยะที่ตำแหน่ง X ต้องเปลี่ยนไปต่อเฟรม (หน่วย Unity Unit) ถึงจะนับว่า \"ขยับจริง\"\n" +
            "ตั้งไว้กันความสั่นเล็กน้อยจาก Floating Point ไม่ให้เด้ง True/False สลับถี่ๆ ตอนยืนนิ่ง\n" +
            "ค่ายิ่งสูง ยิ่งต้องขยับเร็ว/ไกลกว่าเดิมถึงจะติด Moving (ปรับได้ถ้ารู้สึกไวไปหรือช้าไป)"
        )]
        [SerializeField] private float moveThreshold = 0.001f;

        private BossV2 boss;
        private bool isPlayingDeath;
        private float deathTimer;
        private Vector3 lastPosition;

        private void Awake()
        {
            boss = GetComponent<BossV2>();
        }

        private void Start()
        {
            // เริ่มจับจากตำแหน่งปัจจุบันก่อน กัน deltaX เพี้ยนเป็นค่าตำแหน่งเริ่มต้นเทียบกับ (0,0,0) ตอนเฟรมแรก
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            boss.OnBossDefeated += HandleDefeated;
        }

        private void OnDisable()
        {
            boss.OnBossDefeated -= HandleDefeated;
        }

        private void HandleDefeated()
        {
            isPlayingDeath = false;

            // ไม่ได้เสียบ Animator ไว้ -> ไม่ยุ่งอะไร ปล่อยให้ Blink เดิมทำงานตามปกติ
            if (animator == null)
                return;

            animator.SetTrigger(deathTrigger);

            if (!playWithBlink)
            {
                boss.SuppressBuiltInDeathVisual = true;
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
            // ใช้ LateUpdate เพราะต้องรอให้ BossV2 ขยับตัวจริงใน Update() ก่อน
            // แล้วค่อยมาอ่านตำแหน่งล่าสุด ไม่งั้นค่าที่ได้จะช้าไปหนึ่งเฟรม
            if (animator == null) return;

            float deltaX = transform.position.x - lastPosition.x;

            bool movingLeft = deltaX < -moveThreshold;
            bool movingRight = deltaX > moveThreshold;

            animator.SetBool(movingLeftParam, movingLeft);
            animator.SetBool(movingRightParam, movingRight);

            lastPosition = transform.position;
        }
    }
}
