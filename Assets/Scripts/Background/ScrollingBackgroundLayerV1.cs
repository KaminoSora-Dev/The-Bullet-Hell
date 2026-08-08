using UnityEngine;

namespace BackgroundSystem
{
    /// <summary>
    /// เลเยอร์พื้นหลังที่เลื่อนวนไม่มีที่สิ้นสุด - ใช้ Sprite 2 ชิ้นต่อกันสนิท (ออกแบบให้ขอบต่อกันได้แบบ seamless)
    /// พอชิ้นไหนเลื่อนพ้นตำแหน่งของอีกชิ้นไปครบ 1 ความยาว segment จะถูกสลับไปต่อท้ายอีกชิ้นทันที
    /// ทำให้เลื่อนวนไปเรื่อยๆ ได้โดยไม่ต้องสร้าง/ทำลาย object ใหม่เลย (ไม่มี GC spike ระหว่างเล่น)
    ///
    /// ใช้ component นี้ตัวเดียวได้ทั้ง 3 ชั้นตามที่ออกแบบไว้ (Base/Far, Mid, Front Fog)
    /// แค่วางหลาย instance ใน Scene แล้วตั้งค่าต่างกันต่อชั้น:
    /// - speed: ชั้นไกลเลื่อนช้ากว่าชั้นใกล้ (ฟีล parallax)
    /// - sortingOrder ของ SpriteRenderer: ชั้นไกลอยู่ล่างสุด, Front Fog อยู่บนสุด (แต่ต่ำกว่า gameplay object)
    /// - สี/อัลฟาของ Sprite: Front Fog มักตั้งให้กึ่งโปร่งใส
    /// ไม่ต้องแยกคลาสตามชั้น เพราะ logic การเลื่อน/วนซ้ำเหมือนกันทุกชั้น ต่างกันแค่ค่าที่ตั้งใน Inspector
    /// </summary>
    public class ScrollingBackgroundLayerV1 : MonoBehaviour
    {
        [Tooltip("Sprite ชิ้นที่ 1 และ 2 ของเลเยอร์นี้ - ต้องออกแบบภาพให้ขอบต่อกันสนิท (seamless) ล่วงหน้า " +
                 "และวางตำแหน่งเริ่มต้นใน Scene ให้ชิ้นที่ 2 ต่อท้ายชิ้นที่ 1 ตามทิศทางเลื่อนพอดี ก่อนกด Play")]
        [SerializeField] private SpriteRenderer segmentA;
        [SerializeField] private SpriteRenderer segmentB;

        [Tooltip("ความเร็วเลื่อน (world unit/วินาที) - ชั้นไกล (Base) ควรตั้งค่าน้อยกว่าชั้นใกล้ (Front Fog) เพื่อฟีล parallax")]
        [SerializeField] private float speed = 2f;

        [Tooltip("ทิศทางเลื่อน - ปกติใช้ลง (Vector2.down) ตาม convention เกมยิงแนวตั้งแบบ Touhou")]
        [SerializeField] private Vector2 direction = Vector2.down;

        private Transform segmentATransform;
        private Transform segmentBTransform;
        private Vector2 normalizedDirection;
        private float segmentLength;

        void Awake()
        {
            if (segmentA == null || segmentB == null)
            {
                Debug.LogWarning($"ScrollingBackgroundLayerV1 ({name}): ยังไม่ได้ลาก segmentA/segmentB ไว้ - ปิดการเลื่อนของเลเยอร์นี้ไว้ก่อน");
                enabled = false;
                return;
            }

            normalizedDirection = direction.normalized;
            segmentATransform = segmentA.transform;
            segmentBTransform = segmentB.transform;

            // ความยาวของ 1 segment ตามแนวทิศทางเลื่อน (ไม่ใช่ความสูง/กว้างเฉยๆ เผื่อเลื่อนแนวเฉียงในอนาคต)
            // ใช้ sprite.bounds ตรงๆ เพราะคำนวณรวม scale ของ transform ให้แล้วโดย Unity เอง ไม่ต้อง hardcode ตัวเลข
            Vector2 size = segmentA.bounds.size;
            segmentLength = Mathf.Abs(size.x * normalizedDirection.x) + Mathf.Abs(size.y * normalizedDirection.y);
        }

        void Update()
        {
            Vector2 movement = normalizedDirection * speed * Time.deltaTime;

            segmentATransform.position += (Vector3)movement;
            segmentBTransform.position += (Vector3)movement;

            // เช็คทีละชิ้นว่าเลื่อนนำหน้าอีกชิ้นเกิน 1 segmentLength แล้วหรือยัง (แปลว่าเลื่อนพ้นจอไปแล้ว)
            // ถ้าใช่ ให้สลับตัวเองไปต่อท้ายอีกชิ้นทันที เพื่อคงความต่อเนื่องของภาพไว้เสมอ
            RepositionIfPassed(segmentATransform, segmentBTransform);
            RepositionIfPassed(segmentBTransform, segmentATransform);
        }

        private void RepositionIfPassed(Transform self, Transform other)
        {
            // ระยะห่างของ self เทียบกับ other โดยวัดตามแนวทิศทางเลื่อนเท่านั้น (dot product)
            // ค่าเป็นบวก = self เลื่อนนำหน้า other ไปตามทิศทางที่กำลังเคลื่อนที่
            float distanceAhead = Vector2.Dot((Vector2)(self.position - other.position), normalizedDirection);

            if (distanceAhead > segmentLength)
            {
                // self นำหน้าไปเกิน 1 ความยาว segment แล้ว - ย้ายไปต่อท้าย other แทน (กลายเป็นชิ้นที่ตามหลังสุด)
                self.position = other.position - (Vector3)(normalizedDirection * segmentLength);
            }
        }
    }
}
