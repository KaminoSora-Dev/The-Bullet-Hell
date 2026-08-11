using UnityEngine;
using BossSystem;
using PoolingSystem;

namespace AnimationSystem
{
    /// <summary>
    /// วางไว้บน GameObject/Prefab เดียวกับ BossV2 (component แยก ไม่แก้ไฟล์ BossV2 เลย)
    /// ฟัง OnPhaseStarted / OnBossDefeated ที่มีอยู่แล้วในตัว BossV2 (ไม่ต้องเพิ่ม event ใหม่)
    ///
    /// FALLBACK: ถ้าไม่ได้ลาก Animator ไว้ - component นี้จะไม่ทำอะไรเลย และ BossV2 จะ Blink สีขาว + คืน Pool เอง
    /// เหมือนเดิมทุกประการ (SuppressBuiltInDeathVisual จะไม่ถูกตั้งเป็น true เลยถ้าไม่มี Animator)
    /// </summary>
    [RequireComponent(typeof(BossV2))]
    public class BossAnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ปล่อยว่างไว้ได้ถ้า Boss ตัวนี้ยังไม่มีแอนิเมชัน - จะ fallback ไปใช้ Blink เดิมของ BossV2 อัตโนมัติ")]
        [SerializeField] private Animator animator;

        [Header("Animator Parameter Names")]
        [Tooltip("ยิงทุกครั้งที่เข้า Phase ใหม่ - ใช้เล่นแอนิเมชัน \"ขยับ/เปลี่ยนท่า\" ของบอสได้ (ไม่บังคับต้องใช้)")]
        [SerializeField] private string phaseChangedTrigger = "PhaseChanged";
        [SerializeField] private string deathTrigger = "Death";

        private BossV2 boss;

        private void Awake()
        {
            boss = GetComponent<BossV2>();

            if (animator == null)
                Debug.Log($"BossAnimationController ({name}): ไม่ได้ลาก Animator ไว้ - ใช้ Blink เดิมของ BossV2 ต่อไป");
        }

        private void OnEnable()
        {
            boss.OnPhaseStarted += HandlePhaseStarted;
            boss.OnBossDefeated += HandleDefeated;
        }

        private void OnDisable()
        {
            boss.OnPhaseStarted -= HandlePhaseStarted;
            boss.OnBossDefeated -= HandleDefeated;
        }

        private void HandlePhaseStarted(int phaseIndex)
        {
            if (animator == null) return;

            // เพิ่งถูก Spawn/Reset มาใหม่ (Phase 0) - กัน Animator ค้าง state ตายจากรอบก่อนหน้า
            if (phaseIndex == 0)
            {
                animator.Rebind();
                animator.Update(0f);
            }

            if (!string.IsNullOrEmpty(phaseChangedTrigger))
                animator.SetTrigger(phaseChangedTrigger);
        }

        private void HandleDefeated()
        {
            if (animator == null) return; // ไม่มี Animator -> ปล่อยให้ BossV2.UpdateDefeated() ทำงานตามปกติ

            boss.SuppressBuiltInDeathVisual = true; // เพิ่มใหม่ใน BossV2: ปิด Blink built-in ทั้งหมด รวมถึงการคืน Pool อัตโนมัติ
            animator.SetTrigger(deathTrigger);
        }

        /// <summary>ผูกกับ Animation Event ที่เฟรมสุดท้ายของคลิปตายบอส</summary>
        public void OnDeathAnimationComplete()
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
