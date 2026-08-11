using UnityEngine;
using PlayerSystem;

namespace AnimationSystem
{
    /// <summary>
    /// วางไว้บน GameObject เดียวกับ PlayerControllerV1 (component แยก ไม่แก้ไฟล์ PlayerControllerV1 เลย)
    /// อ่านค่า/ฟัง event จาก PlayerControllerV1 ที่เพิ่ม hook ไว้แล้ว (CurrentMoveInput, OnDied, OnRespawned, OnBombUsed)
    ///
    /// ถ้าไม่ได้ลาก Animator ใส่ field ด้านล่าง component นี้จะไม่ทำอะไรเลย (Log เตือนครั้งเดียวตอน Awake)
    /// - Blink/Fade เดิมของ PlayerControllerV1 ยังทำงานเหมือนเดิมทุกประการเสมอ ไม่ว่าจะมี Animator หรือไม่
    ///   (ตัวนี้ไม่ได้ suppress อะไรฝั่ง PlayerControllerV1 - แค่เล่นแอนิเมชันซ้อนทับไปด้วยเฉยๆ)
    /// </summary>
    [RequireComponent(typeof(PlayerControllerV1))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ปล่อยว่างไว้ได้ถ้ายังไม่มีแอนิเมชันผู้เล่น - จะไม่มีผลอะไรกับเกมเลย")]
        [SerializeField] private Animator animator;

        [Header("Animator Parameter Names")]
        [SerializeField] private string isMovingParam = "IsMoving";
        [SerializeField] private string moveXParam = "MoveX";
        [SerializeField] private string deathTrigger = "Death";
        [SerializeField] private string respawnTrigger = "Respawn";
        [SerializeField] private string spellTrigger = "Spell";

        [Tooltip("threshold ของ input แนวนอนก่อนจะถือว่ากำลัง \"เอียงซ้าย/ขวา\" (กันสั่นตอน input ใกล้ 0)")]
        [SerializeField] private float moveXDeadZone = 0.05f;

        private PlayerControllerV1 player;

        private void Awake()
        {
            player = GetComponent<PlayerControllerV1>();

            if (animator == null)
                Debug.Log("PlayerAnimationController: ไม่ได้ลาก Animator ไว้ - จะไม่มีแอนิเมชันผู้เล่นทำงาน (ใช้ Blink เดิมของ PlayerControllerV1 ต่อไป)");
        }

        private void OnEnable()
        {
            player.OnDied += HandleDied;
            player.OnRespawned += HandleRespawned;
            player.OnBombUsed += HandleBombUsed;
        }

        private void OnDisable()
        {
            player.OnDied -= HandleDied;
            player.OnRespawned -= HandleRespawned;
            player.OnBombUsed -= HandleBombUsed;
        }

        private void Update()
        {
            if (animator == null) return;

            // อ่านค่า input ทุกเฟรมเพื่อคุม Idle/Move + ทิศเอียงซ้ายขวา (ไม่ต้องรอ event)
            Vector2 input = player.CurrentMoveInput;

            bool isMoving = input.sqrMagnitude > moveXDeadZone * moveXDeadZone;
            animator.SetBool(isMovingParam, isMoving);
            animator.SetFloat(moveXParam, Mathf.Abs(input.x) > moveXDeadZone ? input.x : 0f);
        }

        private void HandleDied()
        {
            if (animator != null) animator.SetTrigger(deathTrigger);
        }

        private void HandleRespawned()
        {
            if (animator != null) animator.SetTrigger(respawnTrigger);
        }

        private void HandleBombUsed()
        {
            if (animator != null) animator.SetTrigger(spellTrigger);
        }
    }
}
