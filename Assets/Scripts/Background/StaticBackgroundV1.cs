using UnityEngine;

namespace BackgroundSystem
{
    /// <summary>
    /// พื้นหลังภาพนิ่ง - ไม่มีการเลื่อนใดๆ ใช้กับฉากที่ไม่ต้องการฟีล scroll
    /// (เช่น ห้องต่อสู้บอสที่ตัวละครหยุดอยู่กับที่ หรือ cutscene)
    ///
    /// เตรียมช่อง optionalAnimator ไว้เผื่ออนาคต: ถ้าอยากเปลี่ยนพื้นหลังนิ่งให้กลายเป็นแอนิเมชัน
    /// (เช่น ท้องฟ้าเคลื่อนไหวเบาๆ, ฟ้าแลบ) แค่ลาก Animator ที่ผูก Controller ไว้แล้วมาใส่ในช่องนี้
    /// โดยไม่ต้องแก้โครงสร้างคลาสนี้เลย - ตัว Animator จะเล่นของมันเองอัตโนมัติตาม Controller ที่ตั้งไว้
    /// </summary>
    public class StaticBackgroundV1 : MonoBehaviour
    {
        [Tooltip("SpriteRenderer หลักของพื้นหลังนี้ - แสดงภาพนิ่งตามที่ตั้งไว้ใน Inspector")]
        [SerializeField] private SpriteRenderer backgroundSprite;

        [Tooltip("ไม่บังคับ - ถ้าลาก Animator มาใส่ (ปกติติดอยู่ GameObject เดียวกับ backgroundSprite) " +
                 "จะเล่นอนิเมชันของ Animator แทนภาพนิ่งเฉยๆ โดยอัตโนมัติ ปล่อยว่างไว้ถ้าอยากได้ภาพนิ่งจริงๆ")]
        [SerializeField] private Animator optionalAnimator;

        void Awake()
        {
            if (backgroundSprite == null)
            {
                Debug.LogWarning($"StaticBackgroundV1 ({name}): ยังไม่ได้ลาก backgroundSprite ไว้ - จะไม่มีภาพอะไรแสดง");
            }

            // ไม่ต้องมี logic เพิ่มเติมตรงนี้:
            // - ถ้าไม่ได้ลาก optionalAnimator ไว้ -> backgroundSprite แสดงภาพนิ่งตามที่ตั้งค่าไว้เฉยๆ
            // - ถ้าลาก optionalAnimator ไว้ -> Animator เล่น Controller ของตัวเองอัตโนมัติอยู่แล้วโดย Unity เอง
        }
    }
}
