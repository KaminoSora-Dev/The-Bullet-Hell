using System.Collections.Generic;
using UnityEngine;

namespace BackgroundSystem
{
    /// <summary>
    /// ชุดพื้นหลัง 1 ชุด - อ้างอิงถึง root GameObject ที่รวม Layer ทั้งหมด
    /// (เช่น ScrollingBackgroundLayerV1 หลายชั้น หรือ StaticBackgroundV1) ของช่วงหนึ่งในฉากไว้ในตัวเดียว
    /// ทุกชุดต้องวางไว้ล่วงหน้าใน Scene ทั้งหมดตั้งแต่ต้น (ไม่ Instantiate ระหว่างเล่น) แล้วปิดไว้ก่อนยกเว้นชุดเริ่มต้น
    /// </summary>
    [System.Serializable]
    public class BackgroundSet
    {
        [Tooltip("ชื่อชุด - ใช้อ้างอิงตอนเรียก SwitchTo(string) และให้อ่านง่ายใน Inspector")]
        public string setName = "New Set";

        [Tooltip("ลาก root GameObject ของชุดพื้นหลังนี้มาใส่ (รวม Layer ย่อยทั้งหมดของชุดนี้ไว้เป็นลูกของมัน)")]
        public GameObject root;
    }

    /// <summary>
    /// ตัวควบคุมสลับชุดพื้นหลังแบบกระทันหัน (ปิด-เปิดทันที ไม่มี fade/transition ใดๆ)
    /// ไม่มี timer ในตัวเอง - ต้องถูกเรียกจากภายนอกเสมอ เช่นจาก StageSpawnerV1 ตอนถึงเวลาที่กำหนด
    /// หรือจากระบบ boss ตอน HP ถึงเกณฑ์ที่ต้องการเปลี่ยนบรรยากาศฉาก
    ///
    /// ทำไมไม่ทำ timer ผูกไว้ในตัวนี้เลย: แต่ละฉากมีเงื่อนไข "เมื่อไหร่ควรสลับ" ไม่เหมือนกัน
    /// (บางฉากสลับตามเวลา, บางฉากสลับตาม event จากการ์ตัว boss) ให้ผู้เรียกเป็นคนตัดสินใจเงื่อนไขเอง
    /// ตัวนี้มีหน้าที่แค่ "สลับให้เมื่อถูกสั่ง" เท่านั้น เหมือนกับที่ EnemyShooterV1 แยกเรื่อง "ยิงเมื่อถูกสั่ง"
    /// ออกจากเรื่อง "ตัดสินใจว่าเมื่อไหร่ควรยิง" (ซึ่งอยู่ที่ EnemyV1)
    /// </summary>
    public class StageBackgroundControllerV1 : MonoBehaviour
    {
        [Tooltip("รายการชุดพื้นหลังทั้งหมดของฉากนี้ - เรียงตามลำดับที่สะดวก อ้างอิงตอนสลับได้ทั้งด้วย index หรือชื่อ")]
        [SerializeField] private List<BackgroundSet> sets = new List<BackgroundSet>();

        [Tooltip("Index ของชุดที่จะเปิดตอนเริ่มฉาก (ชุดอื่นทั้งหมดจะถูกปิดให้อัตโนมัติตอน Awake)")]
        [SerializeField] private int startingSetIndex = 0;

        private int currentSetIndex = -1;

        void Awake()
        {
            SwitchTo(startingSetIndex);
        }

        /// <summary>
        /// สลับไปชุดพื้นหลัง index ที่ระบุทันที - ปิดทุกชุดก่อนแล้วเปิดเฉพาะชุดใหม่
        /// เรียกได้จากภายนอก เช่น StageSpawnerV1 ตอนถึงเวลาที่กำหนด
        /// </summary>
        public void SwitchTo(int index)
        {
            if (index < 0 || index >= sets.Count)
            {
                Debug.LogWarning($"StageBackgroundControllerV1: index {index} อยู่นอกขอบเขตรายการ sets (มี {sets.Count} ชุด) - ไม่สลับ");
                return;
            }

            // ปิดทุกชุดก่อนเสมอ (กันเคสมีมากกว่า 1 ชุดเปิดค้างพร้อมกันจากการตั้งค่าผิดพลาดใน Inspector)
            for (int i = 0; i < sets.Count; i++)
            {
                if (sets[i].root != null)
                    sets[i].root.SetActive(i == index);
                else
                    Debug.LogWarning($"StageBackgroundControllerV1: ชุด '{sets[i].setName}' (index {i}) ยังไม่ได้ลาก root ไว้");
            }

            currentSetIndex = index;
        }

        /// <summary>
        /// สลับไปชุดพื้นหลังโดยอ้างอิงจากชื่อ (setName) แทน index
        /// สะดวกกว่าตอนเรียกจากสคริปต์อื่นที่ไม่อยากจำเลข index ของแต่ละชุด
        /// </summary>
        public void SwitchTo(string name)
        {
            int index = sets.FindIndex(s => s.setName == name);
            if (index < 0)
            {
                Debug.LogWarning($"StageBackgroundControllerV1: ไม่พบชุดชื่อ '{name}' ในรายการ sets");
                return;
            }
            SwitchTo(index);
        }

        /// <summary>เผยค่า index ของชุดที่เปิดอยู่ตอนนี้ออกไปให้สคริปต์อื่นอ่านได้ (เช่นเช็คก่อนสั่งสลับซ้ำ)</summary>
        public int CurrentSetIndex => currentSetIndex;
    }
}
