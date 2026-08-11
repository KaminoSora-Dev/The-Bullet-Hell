using System;
using System.Collections.Generic;
using UnityEngine;
using StageSystem;
using BossSystem;

namespace DialogueSystem
{
    /// <summary>
    /// ตัวกลางควบคุมบทพูดทั้งฉาก - เล่นทีละบรรทัดเดียวทั่วทั้งเกม (ไม่ว่าตัวละครไหนพูดก็ตาม)
    /// เพื่อไม่ให้กล่องข้อความหลายกล่องขึ้นทับซ้อนกันพร้อมกันจนอ่านไม่ทัน
    ///
    /// ไม่ Block เกม (ไม่แตะ Time.timeScale เลย) - ผู้เล่นเล่นต่อได้ปกติระหว่างบทพูดกำลังเล่น
    ///
    /// มี 2 แหล่งคิว:
    /// - Timeline หลัก: เล่นตามเวลาที่ตั้งไว้ อ้างอิงจาก StageSpawnerV1.StageTimer
    /// - Conditional Group: เล่นทันทีที่เงื่อนไขพิเศษเกิดขึ้น (เช่น Boss Spawn/Defeat) - จะ "แทรก" เข้าคิวก่อน Timeline หลักเสมอ
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("Stage Timer Reference")]
        [Tooltip("ลาก StageSpawnerV1 ของฉากนี้มาใส่ - Timeline หลักจะอ่านเวลาจาก StageTimer ตัวนี้ " +
                 "ถ้าไม่ใส่ไว้จะ fallback ไปใช้ Time.time แทน")]
        [SerializeField] private StageSpawnerV1 stageSpawner;

        [Header("Timeline หลัก (อิง StageTimer)")]
        [Tooltip("เรียงตามลำดับที่ควรเล่น - แต่ละบรรทัดจะเล่นจริงตอน StageTimer ถึง Start Time ของมัน " +
                 "หรือหลังบรรทัดก่อนหน้าจบ + Next Text Delay ของบรรทัดก่อนหน้า (เอาเวลาไหนถึงทีหลังเป็นตัวตัดสิน)")]
        [SerializeField] private List<DialogueLine> timeline = new List<DialogueLine>();

        [Header("Conditional Groups")]
        [SerializeField] private List<DialogueConditionalGroup> conditionalGroups = new List<DialogueConditionalGroup>();

        // ---- registered boxes ----
        private readonly Dictionary<string, DialogueBoxV1> boxes = new Dictionary<string, DialogueBoxV1>();

        // ---- main timeline runtime ----
        private int timelineIndex;
        private float mainQueueReadyTime;

        // ---- conditional runtime ----
        private readonly List<PendingConditionalLine> pendingConditionalLines = new List<PendingConditionalLine>();
        private readonly List<Action> conditionalUnsubscribers = new List<Action>();

        private bool isPlayingLine;

        private struct PendingConditionalLine
        {
            public DialogueLine line;
            public float earliestTime; // StageTimer ตอนที่พร้อมเล่นได้จริง (รวม chain delay ภายในกลุ่มแล้ว)
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            RegisterExistingDialogueBoxes();
            SubscribeConditionalGroups();
        }

        private void RegisterExistingDialogueBoxes()
        {
            DialogueBoxV1[] existingBoxes =
                FindObjectsByType<DialogueBoxV1>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            for (int i = 0; i < existingBoxes.Length; i++)
            {
                RegisterBox(existingBoxes[i]);
            }
        }
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            UnsubscribeConditionalGroups();
        }

        // ============================================================
        // BOX REGISTRATION
        // ============================================================

        public void RegisterBox(DialogueBoxV1 box)
        {
            if (box == null || string.IsNullOrEmpty(box.SpeakerId)) return;

            if (boxes.ContainsKey(box.SpeakerId))
                Debug.LogWarning($"DialogueManager: มี Speaker Id '{box.SpeakerId}' ซ้ำกันมากกว่า 1 กล่อง - จะใช้ตัวล่าสุดที่ลงทะเบียน");

            boxes[box.SpeakerId] = box;
        }

        public void UnregisterBox(DialogueBoxV1 box)
        {
            if (box == null) return;

            if (boxes.TryGetValue(box.SpeakerId, out var current) && current == box)
                boxes.Remove(box.SpeakerId);
        }

        // ============================================================
        // CONDITIONAL SUBSCRIPTION
        // ============================================================

        private void SubscribeConditionalGroups()
        {
            for (int i = 0; i < conditionalGroups.Count; i++)
            {
                DialogueConditionalGroup group = conditionalGroups[i];

                if (group == null || group.targetBoss == null || group.isSubscribed)
                    continue;

                Action handler = () => OnConditionTriggered(group);

                switch (group.condition)
                {
                    case DialogueConditionType.OnBossSpawn:

                        // BossV2 ประกาศ event ตอน Activate()
                        // ซึ่งถูกเรียกหลัง Boss ถูก Spawn จาก Pool แล้ว
                        BossV2.OnAnyBossActivated += handler;

                        conditionalUnsubscribers.Add(
                            () => BossV2.OnAnyBossActivated -= handler
                        );

                        break;

                    case DialogueConditionType.OnBossDefeat:

                        BossV2.OnAnyBossDefeatedAny += handler;

                        conditionalUnsubscribers.Add(
                            () => BossV2.OnAnyBossDefeatedAny -= handler
                        );

                        break;
                }

                group.isSubscribed = true;
            }
        }

        private void UnsubscribeConditionalGroups()
        {
            for (int i = 0; i < conditionalUnsubscribers.Count; i++)
                conditionalUnsubscribers[i]?.Invoke();

            conditionalUnsubscribers.Clear();
        }

        private void OnConditionTriggered(DialogueConditionalGroup group)
        {
            if (group.hasTriggered) return;
            group.hasTriggered = true;

            float baseTime = CurrentStageTime();
            float readyTime = baseTime;

            for (int i = 0; i < group.entries.Count; i++)
            {
                DialogueLine line = group.entries[i];
                if (line == null) continue;

                readyTime = Mathf.Max(baseTime + line.startTime, readyTime);

                pendingConditionalLines.Add(new PendingConditionalLine
                {
                    line = line,
                    earliestTime = readyTime
                });

                readyTime += line.nextTextDelay;
            }
        }

        // ============================================================
        // UPDATE LOOP
        // ============================================================

        private void Update()
        {
            if (isPlayingLine) return;

            float stageTime = CurrentStageTime();

            if (TryPlayNextConditionalLine(stageTime)) return;
            TryPlayNextTimelineLine(stageTime);
        }

        private float CurrentStageTime()
        {
            return stageSpawner != null ? stageSpawner.stageTimer : Time.time;
        }

        private bool TryPlayNextConditionalLine(float stageTime)
        {
            if (pendingConditionalLines.Count == 0) return false;

            PendingConditionalLine next = pendingConditionalLines[0];
            if (stageTime < next.earliestTime) return false;

            pendingConditionalLines.RemoveAt(0);
            PlayLine(next.line, isMainQueueLine: false);
            return true;
        }

        private void TryPlayNextTimelineLine(float stageTime)
        {
            if (timelineIndex >= timeline.Count) return;

            DialogueLine line = timeline[timelineIndex];
            if (line == null)
            {
                timelineIndex++;
                return;
            }

            float earliest = Mathf.Max(line.startTime, mainQueueReadyTime);
            if (stageTime < earliest) return;

            timelineIndex++;
            PlayLine(line, isMainQueueLine: true);
        }

        // ============================================================
        // PLAYBACK
        // ============================================================

        private void PlayLine(DialogueLine line, bool isMainQueueLine)
        {
            if (!boxes.TryGetValue(line.speakerId, out DialogueBoxV1 box) || box == null)
            {
                Debug.LogWarning($"DialogueManager: ไม่พบกล่องข้อความของ Speaker Id '{line.speakerId}' - ข้ามบรรทัดนี้ไป");

                if (isMainQueueLine)
                    mainQueueReadyTime = CurrentStageTime() + line.nextTextDelay;

                return;
            }

            isPlayingLine = true;
            line.hasPlayed = true;

            box.PlayLine(line.text, line.liveTime, line.endStyle, () =>
            {
                isPlayingLine = false;

                if (isMainQueueLine)
                    mainQueueReadyTime = CurrentStageTime() + line.nextTextDelay;
            });
        }
    }
}
