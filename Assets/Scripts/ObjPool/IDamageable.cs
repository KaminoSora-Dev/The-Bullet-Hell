using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// Interface กลางสำหรับ object ที่ต้องรับดาเมจจากกระสุนผู้เล่นได้ (EnemyV1, BossV1, หรืออะไรก็ตามในอนาคต)
    ///
    /// ทำไมต้องมี interface นี้แทนที่จะให้ PlayerBulletV1 เช็ค TryGetComponent&lt;EnemyV1&gt; ตรงๆ เหมือนเดิม:
    /// พอมี BossV1 เป็นคลาสแยกต่างหาก (ไม่ได้สืบทอด/ครอบ EnemyV1) ถ้ายังเช็คแบบเดิม กระสุนผู้เล่นจะ "มองไม่เห็น" Boss เลย
    /// การเช็คผ่าน interface กลางแทน ทำให้ PlayerBulletV1.cs ไม่ต้องรู้จัก/แก้ไขอีกเลย ไม่ว่าอนาคตจะเพิ่มศัตรู/บอสชนิดใหม่กี่แบบก็ตาม
    /// แค่ให้คลาสใหม่นั้น implement IDamageable ก็พอ
    /// </summary>
    public interface IDamageable
    {
        /// <param name="amount">จำนวนดาเมจ</param>
        /// <param name="attacker">GameObject ตัวที่ทำดาเมจ (เผื่อฝั่งที่โดนอยากเช็ค tag เพื่อตัดสินเรื่องคะแนน เหมือน EnemyV1/BossV1 เช็ค tag "PlayerBullet")</param>
        void TakeDamage(int amount, GameObject attacker = null);
    }
}