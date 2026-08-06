using UnityEngine;
using UnityEngine.UI;

namespace BossSystem
{
    /// <summary>
    /// แถบเลือด Boss - ใช้ Image ประเภท Filled (Fill Method = Horizontal) แล้วปรับ fillAmount ตาม HP ของเฟสปัจจุบัน
    /// HP จะเต็มแถบใหม่ทุกครั้งที่ Boss ขึ้นเฟสใหม่ (ดู BossV1.StartPhase ที่ยิง OnPhaseHealthChanged เต็มค่าเสมอ)
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [Header("ลาก Boss ที่จะแสดงผลมาใส่")]
        [SerializeField] private BossV1 boss;

        [Header("ลาก Image (Image Type = Filled) มาใส่")]
        [SerializeField] private Image fillImage;

        [Tooltip("ไล่ fillAmount แบบนุ่มๆ แทนกระโดดทันที (หน่วย: fillAmount ต่อวินาที) - ยิ่งค่าสูงยิ่งไล่เร็ว")]
        [SerializeField] private float fillLerpSpeed = 2f;

        private float targetFill = 1f;

        void OnEnable()
        {
            if (boss == null)
            {
                Debug.LogWarning("BossHealthBarUI: ไม่ได้ลาก BossV1 ไว้ใน field \"boss\"");
                return;
            }

            boss.OnPhaseStarted += HandlePhaseStarted;
            boss.OnPhaseHealthChanged += HandleHealthChanged;
        }

        void OnDisable()
        {
            if (boss == null) return;

            boss.OnPhaseStarted -= HandlePhaseStarted;
            boss.OnPhaseHealthChanged -= HandleHealthChanged;
        }

        private void HandlePhaseStarted(BossPhase phase)
        {
            // ขึ้นเฟสใหม่ = เติมแถบเต็มทันที ไม่ต้องไล่ (กันแถบไล่ขึ้นแปลกๆ ตอนเพิ่งขึ้นเฟส)
            targetFill = 1f;
            if (fillImage != null) fillImage.fillAmount = 1f;
        }

        private void HandleHealthChanged(int current, int max)
        {
            targetFill = max > 0 ? (float)current / max : 0f;
        }

        void Update()
        {
            if (fillImage == null) return;
            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, targetFill, fillLerpSpeed * Time.deltaTime);
        }
    }
}