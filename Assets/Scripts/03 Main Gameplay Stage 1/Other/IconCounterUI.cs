using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameSystem
{
    /// <summary>
    /// แสดงจำนวน Life / Spell เป็นแถว icon สไตล์ Touhou Fantasia
    /// ใช้ 2 prefab แยกอิสระกัน: ShadowIcon (เงา) กับ ColorIcon (สี)
    ///
    /// วิธีทำให้ icon สี "ซ้อนทับตำแหน่งเดียวกันเป๊ะ" กับ icon เงา โดยไม่ต้องรวมเป็น prefab เดียว:
    /// ให้ shadowContainer กับ colorContainer เป็นคนละ RectTransform ที่วาง "ซ้อนพิกัดเดียวกันพอดี"
    /// (Anchor/Position/Size เท่ากันทุกอย่าง - เหมือนวางกระดาษใสซ้อนกัน 2 แผ่น)
    /// แล้วติด HorizontalLayoutGroup ตั้งค่า (Spacing, Padding, Child Alignment) ให้ "เหมือนกันทุกตัวเลข" ทั้ง 2 container
    /// เมื่อ Layout Group คำนวณตำแหน่ง icon ลำดับที่ i จากค่าตั้งเดียวกัน ทั้ง 2 แถวจะได้ตำแหน่ง x เท่ากันโดยอัตโนมัติ
    /// โดยไม่ต้องมี wrapper object ครอบคู่กันเลย
    ///
    /// colorContainer ต้องถูกวางเป็น child "หลัง" shadowContainer ใน Hierarchy (sibling index สูงกว่า)
    /// เพื่อให้ icon สีเรนเดอร์ทับหน้า icon เงาเสมอ
    /// </summary>
    public class IconCounterUI : MonoBehaviour
    {
        [Header("Prefabs (แยกกันอิสระ)")]
        [Tooltip("Prefab icon เงา - โชว์ตลอดตามจำนวน maxIcons ไม่ว่าค่าจริงจะเท่าไหร่")]
        [SerializeField] private RectTransform shadowIconPrefab;
        [Tooltip("Prefab icon สี - โชว์เฉพาะช่องที่ 0..count-1 เท่านั้น")]
        [SerializeField] private RectTransform colorIconPrefab;

        [Header("Containers (ต้องวางซ้อนตำแหน่งเดียวกัน + ตั้ง Layout Group ค่าเท่ากัน)")]
        [SerializeField] private Transform shadowContainer;
        [SerializeField] private Transform colorContainer;

        [Header("Settings (ตั้งค่าได้)")]
        [Tooltip("จำนวนช่อง icon สูงสุดที่แสดงบนจอ - เช็คค่านี้ให้ดีก่อนกด Play เพราะถ้าตั้งเผลอเป็นเลขใหญ่ผิดปกติ "
                 + "จะ Instantiate วนเยอะเกินจำเป็นจนโหลดช้า")]
        [SerializeField] private int maxIcons = 8;

        [Header("Overflow (ทางเลือก)")]
        [Tooltip("ถ้าจำนวนจริงเกิน maxIcons จะโชว์ตัวเลขส่วนเกินตรงนี้แทน เช่น '+2' - ปล่อยว่างได้ถ้าไม่ต้องการ")]
        [SerializeField] private TMP_Text overflowText;

        // เก็บ reference ของ icon สีแต่ละช่อง ไว้สลับ active/inactive ตอน SetCount
        // สร้างครั้งเดียวตอน Awake ไม่ Instantiate/Destroy ซ้ำทุกครั้งที่ค่าเปลี่ยน (ค่าเปลี่ยนบ่อยกว่าตอนสร้าง UI เยอะ)
        private readonly List<GameObject> colorIcons = new List<GameObject>();

        void Awake()
        {
            BuildIcons();
        }

        private void BuildIcons()
        {
            if (shadowIconPrefab == null || colorIconPrefab == null || shadowContainer == null || colorContainer == null)
            {
                Debug.LogWarning("IconCounterUI: ยังไม่ได้ตั้งค่า prefab หรือ container ให้ครบใน Inspector");
                return;
            }

            if (maxIcons <= 0)
            {
                Debug.LogWarning("IconCounterUI: maxIcons ต้องมากกว่า 0");
                return;
            }

            // สร้างแถวเงาให้ครบ maxIcons ช่องก่อน - แถวนี้ไม่มีการเปิด/ปิดทีหลัง โชว์เต็มตลอด
            for (int i = 0; i < maxIcons; i++)
            {
                Instantiate(shadowIconPrefab, shadowContainer);
            }

            // สร้างแถวสีให้ครบ maxIcons ช่องเท่ากัน แต่เริ่มต้นปิดไว้ก่อนทั้งหมด รอ SetCount() มาเปิดทีหลัง
            for (int i = 0; i < maxIcons; i++)
            {
                RectTransform colorInstance = Instantiate(colorIconPrefab, colorContainer);
                colorInstance.gameObject.SetActive(false);
                colorIcons.Add(colorInstance.gameObject);
            }
        }

        /// <summary>
        /// เรียกจาก event ของ PlayerStatsManager (OnLivesChanged / OnSpellsChanged)
        /// เปิด icon สีของช่อง index 0..count-1 ที่เหลือปิดไว้ (เห็นแต่เงา)
        /// </summary>
        public void SetCount(int count)
        {
            int clampedCount = Mathf.Clamp(count, 0, maxIcons);

            for (int i = 0; i < colorIcons.Count; i++)
            {
                if (colorIcons[i] == null) continue;
                colorIcons[i].SetActive(i < clampedCount);
            }

            if (overflowText != null)
            {
                int overflow = count - maxIcons;
                if (overflow > 0)
                {
                    overflowText.gameObject.SetActive(true);
                    overflowText.text = "+" + overflow;
                }
                else
                {
                    overflowText.gameObject.SetActive(false);
                }
            }
        }
    }
}