using UnityEngine;
using TMPro;

namespace BossSystem
{
    /// <summary>
    /// แสดงชื่อ Signature Spell Card + เวลานับถอยหลัง - ลาก Text ให้ตรงช่องเองใน Inspector
    /// โชว์/ซ่อนอัตโนมัติ: โชว์เฉพาะตอนเฟสปัจจุบันเป็น SignatureSpellCard เท่านั้น เฟส NonSpell จะซ่อนกลุ่ม UI นี้ไว้
    /// </summary>
    public class SignatureSpellCardUI : MonoBehaviour
    {
        [Header("ลาก Boss ที่จะแสดงผลมาใส่")]
        [SerializeField] private BossV1 boss;

        [Header("ลาก Text ให้ตรงช่อง")]
        [Tooltip("ช่องโชว์ชื่อลาย (phase.phaseName)")]
        [SerializeField] private TMP_Text nameText;
        [Tooltip("ช่องโชว์เวลานับถอยหลัง")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("Root object ของ UI ชุดนี้ (เช่น panel ที่รวม nameText + timerText) - ปิด/เปิดทั้งกลุ่มตอนไม่ใช่เฟส Spell Card " +
                 "ปล่อยว่างได้ถ้าอยากคุม visibility เองจากที่อื่น")]
        [SerializeField] private GameObject root;

        [Tooltip("จำนวนหลักหลังจุดทศนิยมของเวลาที่โชว์ เช่น 2 = \"12.34\"")]
        [SerializeField] private int decimalPlaces = 2;

        void OnEnable()
        {
            if (boss == null)
            {
                Debug.LogWarning("SignatureSpellCardUI: ไม่ได้ลาก BossV1 ไว้ใน field \"boss\"");
                return;
            }

            boss.OnPhaseStarted += HandlePhaseStarted;
            boss.OnPhaseEnded += HandlePhaseEnded;
            boss.OnPhaseTimerTick += HandleTimerTick;
        }

        void OnDisable()
        {
            if (boss == null) return;

            boss.OnPhaseStarted -= HandlePhaseStarted;
            boss.OnPhaseEnded -= HandlePhaseEnded;
            boss.OnPhaseTimerTick -= HandleTimerTick;
        }

        private void HandlePhaseStarted(BossPhase phase)
        {
            bool isSpellCard = phase.phaseType == BossPhaseType.SignatureSpellCard;

            if (root != null) root.SetActive(isSpellCard);
            if (!isSpellCard) return;

            if (nameText != null) nameText.text = phase.phaseName;
            if (timerText != null) timerText.text = FormatTime(phase.timeLimit);
        }

        private void HandlePhaseEnded(BossPhase phase)
        {
            if (root != null) root.SetActive(false);
        }

        private void HandleTimerTick(float remaining, float total)
        {
            if (timerText != null) timerText.text = FormatTime(remaining);
        }

        private string FormatTime(float seconds)
        {
            return seconds.ToString("F" + decimalPlaces);
        }
    }
}