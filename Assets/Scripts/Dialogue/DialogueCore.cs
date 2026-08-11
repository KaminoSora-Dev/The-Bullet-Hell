using System;
using System.Collections.Generic;
using UnityEngine;
using BossSystem;

namespace DialogueSystem
{
    /// <summary>
    /// รูปแบบการหายไปของกล่องข้อความ หลังจาก Live Time หมด
    /// </summary>
    public enum TextEndStyle
    {
        /// <summary>หายทันที ไม่มี fade</summary>
        Instant,

        /// <summary>กล่อง + ข้อความจางหายไปพร้อมกันทั้งหมด</summary>
        FadeOut,

        /// <summary>ข้อความไล่ลบออกทีละตัวอักษรจากท้ายไปหน้า (เหมือน Typewriter ย้อนกลับ) แล้วกล่องค่อยจางหายทีหลัง</summary>
        TypewriterErase
    }

    /// <summary>
    /// เงื่อนไขพิเศษที่ให้บทพูดเริ่มเล่นทันทีที่เหตุการณ์เกิดขึ้น แทนที่จะรอ StageTimer ตามปกติ
    /// เพิ่ม case ใหม่ได้ในอนาคตโดยเพิ่ม enum ตรงนี้ + จุด subscribe ใน DialogueManager.SubscribeConditionalGroups()
    /// </summary>
    public enum DialogueConditionType
    {
        OnBossSpawn,
        OnBossDefeat
    }

    /// <summary>
    /// 1 บรรทัดของบทพูด - ใช้ร่วมกันได้ทั้ง Timeline หลัก (ผูกกับ StageTimer) และ Conditional Group (ผูกกับ Event)
    /// ความหมายของ startTime ต่างกันเล็กน้อยตามที่ถูกใช้งาน (ดู tooltip ในแต่ละที่ที่เรียกใช้ list นี้)
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("ต้องตรงกับ Speaker Id ที่ตั้งไว้บน DialogueBoxV1 ของตัวละครนั้น (case-sensitive)")]
        public string speakerId = "Player";

        [TextArea(2, 5)]
        [Tooltip("ข้อความที่จะพิมพ์ทีละตัวอักษร")]
        public string text = "";

        [Tooltip("Timeline หลัก = เวลานับจาก StageTimer ของ StageSpawnerV1 (วินาที)\n" +
                 "Conditional Group = เวลาหน่วงหลังจากเหตุการณ์เกิดขึ้นจริง (วินาที) เพื่อไม่ให้ Text โผล่ทันที")]
        public float startTime = 0f;

        [Tooltip("เวลาที่ข้อความอยู่เฉยๆ เต็มบรรทัด ก่อนจะเริ่ม Fade ตาม Text End Style (นับหลังพิมพ์ครบแล้ว)")]
        public float liveTime = 2f;

        public TextEndStyle endStyle = TextEndStyle.FadeOut;

        [Tooltip("เวลาที่เว้นก่อนจะเล่นบรรทัดถัดไปในคิวเดียวกัน (นับหลังบรรทัดนี้จางหายไปหมดแล้ว)")]
        public float nextTextDelay = 0.3f;

        // ---- runtime (ไม่โชว์ใน Inspector) ----
        [NonSerialized] public bool hasPlayed;
    }

    /// <summary>
    /// กลุ่มบทพูดที่เล่นเมื่อเงื่อนไขพิเศษเกิดขึ้น เช่น Boss เพิ่ง Spawn หรือ Boss เพิ่งตาย
    /// entries เล่นเรียงลำดับกันเองภายในกลุ่ม (เหมือน Timeline หลัก) โดยเริ่มนับ startTime ของแต่ละบรรทัด
    /// จากจังหวะที่เงื่อนไขเกิดขึ้นจริง ไม่ใช่จาก StageTimer
    /// เมื่อเงื่อนไขทำงาน กลุ่มนี้จะ "แทรก" เข้าคิวเล่นก่อน Timeline หลักเสมอ (ดู DialogueManager.Update())
    /// </summary>
    [Serializable]
    public class DialogueConditionalGroup
    {
        [Tooltip("ชื่อกลุ่ม - ใช้แค่ให้อ่านง่ายใน Inspector เท่านั้น")]
        public string groupName = "New Conditional Group";

        [Tooltip("Boss ที่จะ subscribe event ด้วย (ต้องลาก BossV2 ใน Scene มาใส่)")]
        public BossV2 targetBoss;

        public DialogueConditionType condition = DialogueConditionType.OnBossSpawn;

        [Tooltip("รายการบทพูดที่จะเล่นเรียงกันตามลำดับ หลังเงื่อนไขนี้เกิดขึ้นจริง")]
        public List<DialogueLine> entries = new List<DialogueLine>();

        // ---- runtime (ไม่โชว์ใน Inspector) ----
        [NonSerialized] public bool hasTriggered;
        [NonSerialized] public bool isSubscribed;
    }
}
