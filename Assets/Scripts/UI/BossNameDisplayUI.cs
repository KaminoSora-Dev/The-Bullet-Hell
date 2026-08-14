using UnityEngine;
using TMPro;
using BossSystem;

namespace UISystem
{
    /// <summary>
    /// แสดงชื่อบอส (ข้อความอย่างเดียว ไม่มีเลือด) ตอนเจอบอส - ฟัง static event ของ BossV2 โดยตรง
    /// ไม่ต้องลาก reference ผูก Boss ตัวใดตัวหนึ่งใน Inspector เลย วางไว้ใน Scene ครั้งเดียวใช้ได้กับทุกด่าน/ทุกบอส
    /// </summary>
    public class BossNameDisplayUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("GameObject ที่จะเปิด/ปิดพร้อมข้อความ (ปกติคือ parent panel ของ nameText เอง) " +
                 "ปล่อยว่างไว้ได้ถ้าอยากให้แค่ข้อความว่างเปล่าตอนไม่มีบอส ไม่ปิดทั้ง panel")]
        [SerializeField] private GameObject panelRoot;

        [Header("Timing")]
        [Tooltip("เวลาที่โชว์ชื่อบอสค้างไว้หลังเข้าฉาก (วินาที) ก่อนจะซ่อนเอง - ตั้งเป็น 0 = โชว์ค้างไว้จนกว่าบอสจะตาย")]
        [Min(0f)]
        [SerializeField] private float autoHideDelay = 3f;

        private float hideTimer;
        private bool isCountingDown;

        private void OnEnable()
        {
            BossV2.OnAnyBossNameReveal += HandleReveal;
            BossV2.OnAnyBossNameHide += HandleHide;

            SetVisible(false);
        }

        private void OnDisable()
        {
            BossV2.OnAnyBossNameReveal -= HandleReveal;
            BossV2.OnAnyBossNameHide -= HandleHide;
        }

        private void Update()
        {
            if (!isCountingDown || autoHideDelay <= 0f) return;

            hideTimer += Time.deltaTime;
            if (hideTimer >= autoHideDelay)
            {
                isCountingDown = false;
                SetVisible(false);
            }
        }

        private void HandleReveal(string bossName)
        {
            if (nameText != null)
                nameText.text = bossName;

            SetVisible(true);

            hideTimer = 0f;
            isCountingDown = autoHideDelay > 0f;
        }

        private void HandleHide()
        {
            isCountingDown = false;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (panelRoot != null)
                panelRoot.SetActive(visible);
            else if (nameText != null)
                nameText.gameObject.SetActive(visible);
        }
    }
}
