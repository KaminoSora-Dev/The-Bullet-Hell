using UnityEngine;
using EnemySystem;
using PoolingSystem;

namespace AnimationSystem
{
    /// <summary>
    /// วางไว้บน GameObject/Prefab เดียวกับ EnemyV1 (component แยก ไม่แก้ไฟล์ EnemyV1 เลย)
    /// ฟัง event OnSpawnedFromPool / OnDied ที่ EnemyV1 เพิ่ม hook ไว้ให้แล้ว
    ///
    /// FALLBACK: ถ้าไม่ได้ลาก Animator ไว้ - component นี้จะไม่ทำอะไรเลย และ EnemyV1 จะ Blink สีแดง + คืน Pool เอง
    /// เหมือนเดิมทุกประการ (SuppressBuiltInDeathVisual จะไม่ถูกตั้งเป็น true เลยถ้าไม่มี Animator)
    ///
    /// ถ้ามี Animator -> component นี้ "ยึด" การจัดการ Death Visual + การคืน Pool ไปทำเองทั้งหมด
    /// ต้องไปวาง Animation Event ที่เฟรมสุดท้ายของคลิปตาย เรียก OnDeathAnimationComplete() (เหมือน EnemyBulletV2)
    /// </summary>
    [RequireComponent(typeof(EnemyV1))]
    public class EnemyAnimationController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("ปล่อยว่างไว้ได้ถ้า Enemy ตัวนี้ยังไม่มีแอนิเมชัน - จะ fallback ไปใช้ Blink เดิมของ EnemyV1 อัตโนมัติ")]
        [SerializeField] private Animator animator;

        [Header("Animator Parameter Names")]
        [SerializeField] private string isMovingParam = "IsMoving";

        [Tooltip("ตอนนี้ตามที่ต้องการ ใส่ไว้ 2 แบบก่อน - ระบบจะสุ่มเลือก 1 ใน 2 ทุกครั้งที่ Enemy ตัวนี้ตาย")]
        [SerializeField] private string[] deathTriggers = { "Death1", "Death2" };

        private EnemyV1 enemy;

        private void Awake()
        {
            enemy = GetComponent<EnemyV1>();

            if (animator == null)
                Debug.Log($"EnemyAnimationController ({name}): ไม่ได้ลาก Animator ไว้ - ใช้ Blink เดิมของ EnemyV1 ต่อไป");
        }

        private void OnEnable()
        {
            enemy.OnSpawnedFromPool += HandleSpawned;
            enemy.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            enemy.OnSpawnedFromPool -= HandleSpawned;
            enemy.OnDied -= HandleDied;
        }

        private void Update()
        {
            if (animator == null) return;

            // CurrentMovementStepIndex >= 0 หมายถึงกำลังเดินตาม path อยู่ (ดูคอมเมนต์ใน EnemyV1.cs)
            animator.SetBool(isMovingParam, enemy.CurrentMovementStepIndex >= 0);
        }

        private void HandleSpawned()
        {
            if (animator == null) return;

            // กัน Animator ค้าง state "ตาย" จากรอบก่อนหน้า (Enemy ถูกดึงจาก Pool มาใช้ซ้ำ)
            animator.Rebind();
            animator.Update(0f);
        }

        private void HandleDied()
        {
            if (animator == null) return; // ไม่มี Animator -> ปล่อยให้ EnemyV1.UpdateDeathBlink() ทำงานตามปกติ

            enemy.SuppressBuiltInDeathVisual = true; // เพิ่มใหม่ใน EnemyV1: ปิด Blink built-in ทั้งหมด รวมถึงการคืน Pool อัตโนมัติ

            string trigger = PickRandomDeathTrigger();
            if (!string.IsNullOrEmpty(trigger))
                animator.SetTrigger(trigger);
            else
                OnDeathAnimationComplete(); // กันเคสลืมตั้งชื่อ trigger ไว้ - อย่างน้อยก็ยังคืน Pool ได้ ไม่ค้าง
        }

        private string PickRandomDeathTrigger()
        {
            if (deathTriggers == null || deathTriggers.Length == 0) return null;
            int index = Random.Range(0, deathTriggers.Length);
            return deathTriggers[index];
        }

        /// <summary>
        /// ผูกกับ Animation Event ที่เฟรมสุดท้ายของคลิปตายทั้ง 2 แบบ (Death1 กับ Death2 ก็ชี้มาที่ฟังก์ชันเดียวกันนี้ได้เลย)
        /// </summary>
        public void OnDeathAnimationComplete()
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
