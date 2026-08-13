using UnityEngine;
using PlayerSystem;

namespace AnimationSystem
{
    /// <summary>
    /// เสียบ Animator ให้ Player (Optional Component - ไม่ต้องใส่ก็ได้)
    ///
    /// ไม่ต้องแก้ Logic การตาย/ฟื้นคืนชีพใน PlayerControllerV1 เลย - Component นี้แค่ฟัง event ที่มีอยู่แล้ว
    /// (OnDied / OnRespawned) แล้วสั่ง Animator เอาเอง
    ///
    /// ต่างจาก EnemyAnimationController / BossAnimationController ตรงที่ Player ไม่ได้ถูก Return
    /// เข้า Pool ตอนตาย (เป็น Object เดียวที่อยู่ยาวตลอดเกม) - จังหวะกระพริบ/Fade Out/Fade In/Respawn
    /// ทั้งหมดยังคุมโดย PlayerControllerV1 เหมือนเดิมทุกประการ Component นี้แค่ "ขอสิทธิ์" ไม่ให้
    /// PlayerControllerV1 ไปยุ่งกับสี/อัลฟ่าของ bodySprite เอง (ผ่าน SuppressBuiltInDeathVisual)
    /// แล้วปล่อยให้ Animator เล่นแอนิเมชันคุมภาพแทนในช่วงเวลาเดียวกัน
    ///
    /// --- Idle / Move ---
    /// ปล่อยให้เป็น Default State ของ Animator Controller เอง ไม่มีการสั่ง Trigger ใดๆ จากโค้ดนี้
    ///
    /// --- Death ---
    /// Play With Blink = true  -> เล่น Death Animation ควบคู่ไปกับระบบกระพริบแดง + Fade เดิมของ PlayerControllerV1
    /// Play With Blink = false -> Animator คุมภาพเองทั้งหมด (ไม่กระพริบ ไม่ Fade) ส่วนจังหวะเวลา/การย้ายจุด Respawn
    ///                             ยังทำงานตามปกติเป๊ะ ๆ เพราะเป็น Logic การเล่นเกม ไม่ใช่แค่ภาพ
    ///
    /// --- Respawn ---
    /// ถ้าใส่ชื่อ Respawn Trigger ไว้ (ไม่ปล่อยว่าง) จะสั่ง Trigger นี้ตอน OnRespawned ยิง
    /// เผื่อ Animator Controller ต้องการ Trigger บังคับกลับ Idle เอง (ถ้า Death State ตั้ง Exit Time
    /// ให้กลับ Idle อัตโนมัติอยู่แล้ว ปล่อยช่องนี้ว่างไว้ได้เลย ไม่ต้องใช้)
    ///
    /// ถ้าไม่ลาก Animator ใส่ในช่อง -> PlayerControllerV1 จะทำงานแบบเดิมทุกอย่าง (Blink/Fade อย่างเดียว)
    ///
    /// --- Movement (รวมเข้ามาจาก MovementDirectionAnimator) ---
    /// เช็คว่า Player "ขยับจริง" ไปทางซ้าย/ขวาไหม โดยเทียบตำแหน่งจริงระหว่าง 2 เฟรม (ไม่ใช่เช็คจาก Input)
    /// แล้วสั่ง Bool Parameter (IsMovingLeft / IsMovingRight) - กันปัญหา Input ยังกดอยู่แต่ตำแหน่งไม่ขยับ
    /// เพราะชนขอบจอ (Border Clamp) ซึ่งจะทำให้ Bool ค้าง True ทั้งที่ตัวละครยืนนิ่ง
    /// ทั้งสอง Bool เป็น false พร้อมกัน = ไม่ได้ขยับซ้าย-ขวาเลย (รวมถึงตอนกำลังเล่น Death Animation อยู่ด้วย
    /// เพราะ PlayerControllerV1 หยุดขยับเองอยู่แล้วตอนตาย ไม่ต้องเช็คสถานะ Dead เพิ่ม)
    ///
    /// หมายเหตุ: ถ้าเคยแปะ MovementDirectionAnimator.cs แยกไว้บน Player Object ด้วย ให้เอาออก
    /// เพราะรวมเข้ามาในไฟล์นี้แล้ว - แปะซ้ำสองตัวจะสั่ง SetBool ชนกันเฉยๆ (ไฟล์ MovementDirectionAnimator.cs
    /// ยังเก็บไว้ใช้แปะกับ Object อื่นที่ไม่มี PlayerControllerV1 ได้ตามปกติ)
    /// </summary>
    [RequireComponent(typeof(PlayerControllerV1))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("Animator (ไม่ใส่ก็ได้)")]
        [Tooltip(
            "ลาก Animator ของ Player ตัวนี้มาใส่\n" +
            "ถ้าปล่อยว่างไว้ - จะไม่มีผลอะไรเลย PlayerControllerV1 ใช้ระบบ Blink/Fade เดิม"
        )]
        [SerializeField] private Animator animator;

        [Header("Death")]
        [Tooltip("ชื่อ Trigger ใน Animator Controller ที่จะสั่งตอน Player ตาย")]
        [SerializeField] private string deathTrigger = "Death";

        [Tooltip(
            "ชื่อ Trigger ที่จะสั่งตอน Player Respawn เสร็จ (จบ Fade In) - ปล่อยว่างได้ถ้า Animator Controller\n" +
            "ตั้ง Exit Time ให้ Death State กลับ Idle เองอยู่แล้ว"
        )]
        [SerializeField] private string respawnTrigger = "";

        [Tooltip(
            "ติ๊ก = เล่น Death Animation ควบคู่กับ Blink + Fade เดิม\n" +
            "ไม่ติ๊ก = ให้ Animator คุมภาพตอนตายเองทั้งหมด (ไม่กระพริบ ไม่ Fade) - จังหวะเวลา/Respawn ยังเหมือนเดิม"
        )]
        [SerializeField] private bool playWithBlink = true;

        [Header("Movement")]
        [SerializeField] private string movingLeftParam = "IsMovingLeft";
        [SerializeField] private string movingRightParam = "IsMovingRight";

        [Tooltip(
            "ระยะที่ตำแหน่ง X ต้องเปลี่ยนไปต่อเฟรม (หน่วย Unity Unit) ถึงจะนับว่า \"ขยับจริง\"\n" +
            "ตั้งไว้กันความสั่นเล็กน้อยจาก Floating Point ไม่ให้เด้ง True/False สลับถี่ๆ ตอนยืนนิ่ง\n" +
            "ค่ายิ่งสูง ยิ่งต้องขยับเร็ว/ไกลกว่าเดิมถึงจะติด Moving (ปรับได้ถ้ารู้สึกไวไปหรือช้าไป)"
        )]
        [SerializeField] private float moveThreshold = 0.001f;

        private PlayerControllerV1 player;
        private Vector3 lastPosition;

        private void Awake()
        {
            player = GetComponent<PlayerControllerV1>();
        }

        private void Start()
        {
            // เริ่มจับจากตำแหน่งปัจจุบันก่อน กัน deltaX เพี้ยนเป็นค่าตำแหน่งเริ่มต้นเทียบกับ (0,0,0) ตอนเฟรมแรก
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            player.OnDied += HandleDied;
            player.OnRespawned += HandleRespawned;
        }

        private void OnDisable()
        {
            player.OnDied -= HandleDied;
            player.OnRespawned -= HandleRespawned;
        }

        private void HandleDied()
        {
            // ไม่ได้เสียบ Animator ไว้ -> ไม่ยุ่งอะไร ปล่อยให้ Blink/Fade เดิมทำงานตามปกติ
            if (animator == null)
                return;

            animator.SetTrigger(deathTrigger);

            if (!playWithBlink)
                player.SuppressBuiltInDeathVisual = true;
        }

        private void HandleRespawned()
        {
            if (animator == null)
                return;

            if (!string.IsNullOrEmpty(respawnTrigger))
                animator.SetTrigger(respawnTrigger);
        }

        private void LateUpdate()
        {
            // ใช้ LateUpdate เพราะต้องรอให้ PlayerControllerV1.ApplyMovement() รันจบใน Update() ก่อน
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
