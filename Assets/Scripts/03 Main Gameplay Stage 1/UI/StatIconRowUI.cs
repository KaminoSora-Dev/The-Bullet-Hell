using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;

namespace GameSystem
{
    /// <summary>
    /// แสดงจำนวน Life / Spell / Fragment เป็นแถวไอคอนที่ "วางไว้ล่วงหน้าใน Scene เอง" (ไม่ Instantiate สร้างเองแบบ IconCounterUI)
    /// วิธีทำงาน: มี List ของ GameObject ที่ลากมาจาก Scene เรียงลำดับซ้าย -> ขวา (หรือลำดับที่ต้องการ)
    /// พอเรียก SetCount(n) จะเปิด (Active) ไอคอน index 0..n-1 แล้วปิด (Inactive) ที่เหลือทั้งหมด
    /// ตัวอย่าง: มี 5 ไอคอน xxxxx, ใช้ไป 1 -> SetCount(4) -> ไอคอนตัวท้ายสุดที่เคยเปิดอยู่ (index 4) จะโดนปิด กลายเป็น xxxx_
    ///
    /// ใช้ตัวเดียวกันได้ทั้ง 3 อย่าง (Life/Spell/Fragment) เพราะ logic การ "เปิดไอคอนตามจำนวน" เหมือนกันหมด
    /// ต่างกันแค่ "ฟัง event ไหนจาก PlayerStatsManager" - เลือกผ่าน Inspector ด้วย field mode
    /// (แนวคิดเดียวกับที่ ScoreDisplayUI.cs เลือกโหมด Score/HiScore)
    /// </summary>
    public class StatIconRowUI : MonoBehaviour
    {
        private enum StatMode
        {
            Life,
            Spell,
            // Fragment แต่ละชิ้นตอนนี้เพิ่ม Power ทันที 1 ระดับ (ไม่ต้องสะสมให้ครบก่อนเหมือนเดิม)
            // แถวไอคอนนี้เลยผูกตรงกับ Power Level ปัจจุบันได้เลย ไม่ต้องมีแนวคิด "ความคืบหน้า" อีกต่อไป
            PowerLevel
        }

        [Tooltip("เลือกว่าแถวไอคอนนี้ใช้แสดงค่าอะไร - จะไปฟัง event ที่ตรงกันจาก PlayerStatsManager ให้อัตโนมัติ")]
        [SerializeField] private StatMode mode = StatMode.Life;

        [Tooltip("ลาก GameObject ไอคอนที่วางไว้ใน Scene ล่วงหน้ามาใส่ตามลำดับ (เช่น Heart_1, Heart_2, ... Heart_5) " +
                 "จำนวนช่องใน list นี้ = จำนวนสูงสุดที่แถวนี้แสดงได้ ควรตั้งให้ตรงกับ maxLives/maxSpells/maxPowerLevel " +
                 "ใน PlayerStatsManager ไม่งั้นค่าที่เกินจะ \"หายไปเงียบๆ\" เพราะไม่มีไอคอนเหลือให้เปิดต่อ")]
        [SerializeField] private List<GameObject> icons = new List<GameObject>();

        void Start()
        {
            if (PlayerStatsManager.Instance == null)
            {
                Debug.LogWarning("StatIconRowUI: ไม่พบ PlayerStatsManager ใน scene - ต้องมี GameObject ที่ติด PlayerStatsManager อยู่ก่อน");
                return;
            }

            // Subscribe ใน Start() เหตุผลเดียวกับ ScoreDisplayUI: การันตีว่า Awake() ของ PlayerStatsManager
            // (ที่สร้าง Instance) รันจบไปแล้วแน่นอนก่อน Start() ของสคริปต์นี้จะเริ่ม
            switch (mode)
            {
                case StatMode.Life:
                    PlayerStatsManager.Instance.OnLivesChanged += SetCount;
                    SetCount(PlayerStatsManager.Instance.CurrentLives); // sync ค่าปัจจุบันทันที ไม่ต้องรอ event ครั้งแรก
                    break;

                case StatMode.Spell:
                    PlayerStatsManager.Instance.OnSpellsChanged += SetCount;
                    SetCount(PlayerStatsManager.Instance.CurrentSpells);
                    break;

                case StatMode.PowerLevel:
                    PlayerStatsManager.Instance.OnPowerLevelChanged += SetCount;
                    SetCount(PlayerStatsManager.Instance.CurrentPowerLevel);
                    break;
            }
        }

        void OnDestroy()
        {
            // unsubscribe กัน memory leak / NullReferenceException เหมือน pattern ใน ScoreDisplayUI
            if (PlayerStatsManager.Instance == null) return;

            switch (mode)
            {
                case StatMode.Life:
                    PlayerStatsManager.Instance.OnLivesChanged -= SetCount;
                    break;

                case StatMode.Spell:
                    PlayerStatsManager.Instance.OnSpellsChanged -= SetCount;
                    break;

                case StatMode.PowerLevel:
                    PlayerStatsManager.Instance.OnPowerLevelChanged -= SetCount;
                    break;
            }
        }

        /// <summary>
        /// เปิดไอคอน index 0..count-1 ปิดที่เหลือทั้งหมด
        /// เรียกเองตรงๆ ได้ด้วยถ้าอยากเทสหรือผูกกับระบบอื่นนอกเหนือจาก PlayerStatsManager
        /// </summary>
        public void SetCount(int count)
        {
            int clampedCount = Mathf.Clamp(count, 0, icons.Count);

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                icons[i].SetActive(i < clampedCount);
            }
        }
    }
}