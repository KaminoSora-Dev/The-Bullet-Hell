#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

using EnemySystem;

namespace BossSystem
{
    [CustomEditor(typeof(BossV2))]
    public class BossV2Editor : Editor
    {
        // ============================================================
        // ENTRY
        // ============================================================

        private SerializedProperty entryTargetPoint;
        private SerializedProperty entryDuration;
        private SerializedProperty entryEase;

        // ============================================================
        // PHASES
        // ============================================================

        private SerializedProperty phases;

        // ============================================================
        // PHASE TRANSITION
        // ============================================================

        private SerializedProperty phaseTransitionDuration;
        private SerializedProperty clearBulletsOnPhaseTransition;

        // ============================================================
        // DEATH VISUAL
        // ============================================================

        private SerializedProperty blinkCount;
        private SerializedProperty blinkInterval;
        private SerializedProperty blinkColor;

        // ============================================================
        // SCORE / ITEM DROP
        // ============================================================

        private SerializedProperty scoreValueOnDefeat;
        private SerializedProperty itemDrop;

        // ============================================================
        // REFERENCES
        // ============================================================

        private SerializedProperty bodySprite;
        private SerializedProperty hitCollider;

        // ============================================================
        // ENABLE
        // ============================================================

        private void OnEnable()
        {
            entryTargetPoint =
                serializedObject.FindProperty(
                    "entryTargetPoint"
                );

            entryDuration =
                serializedObject.FindProperty(
                    "entryDuration"
                );

            entryEase =
                serializedObject.FindProperty(
                    "entryEase"
                );

            phases =
                serializedObject.FindProperty(
                    "phases"
                );

            phaseTransitionDuration =
                serializedObject.FindProperty(
                    "phaseTransitionDuration"
                );

            clearBulletsOnPhaseTransition =
                serializedObject.FindProperty(
                    "clearBulletsOnPhaseTransition"
                );

            blinkCount =
                serializedObject.FindProperty(
                    "blinkCount"
                );

            blinkInterval =
                serializedObject.FindProperty(
                    "blinkInterval"
                );

            blinkColor =
                serializedObject.FindProperty(
                    "blinkColor"
                );

            scoreValueOnDefeat =
                serializedObject.FindProperty(
                    "scoreValueOnDefeat"
                );

            itemDrop =
                serializedObject.FindProperty(
                    "itemDrop"
                );

            bodySprite =
                serializedObject.FindProperty(
                    "bodySprite"
                );

            hitCollider =
                serializedObject.FindProperty(
                    "hitCollider"
                );
        }

        // ============================================================
        // INSPECTOR
        // ============================================================

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Boss V2",
                EditorStyles.boldLabel
            );

            EditorGUILayout.HelpBox(
                "แต่ละ Phase มี HP และ BulletCustomPattern ของตัวเอง " +
                "สร้าง BulletCustomPattern แยกเป็น GameObject แล้วลากมาใส่ใน Phase ได้เลย\n\n" +
                "Scene Transition ถูกควบคุมโดย GameFlowManager " +
                "ไม่ใช่ BossV2",
                MessageType.None
            );

            DrawEntry();
            DrawPhases();
            DrawPhaseTransition();
            DrawDeathVisual();
            DrawScoreAndReferences();

            serializedObject.ApplyModifiedProperties();
        }

        // ============================================================
        // ENTRY
        // ============================================================

        private void DrawEntry()
        {
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(
                "ENTRY",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                entryTargetPoint,
                new GUIContent(
                    "Entry Target Point",
                    "เว้นว่างไว้ถ้าอยากให้เริ่ม Phase 1 ทันทีโดยไม่มีช่วง Entry"
                )
            );

            EditorGUILayout.PropertyField(
                entryDuration
            );

            EditorGUILayout.PropertyField(
                entryEase
            );
        }

        // ============================================================
        // PHASES
        // ============================================================

        private void DrawPhases()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "PHASES",
                EditorStyles.boldLabel
            );

            for (int i = 0;
                 i < phases.arraySize;
                 i++)
            {
                SerializedProperty phase =
                    phases.GetArrayElementAtIndex(i);

                SerializedProperty phaseName =
                    phase.FindPropertyRelative(
                        "phaseName"
                    );

                string title =
                    string.IsNullOrWhiteSpace(
                        phaseName.stringValue
                    )
                        ? $"Phase {i + 1}"
                        : phaseName.stringValue;

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox
                );

                EditorGUILayout.BeginHorizontal();

                phase.isExpanded =
                    EditorGUILayout.Foldout(
                        phase.isExpanded,
                        title,
                        true
                    );

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)
                ))
                {
                    phases.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    // Commit การลบทันที แล้วยกเลิก GUI pass ปัจจุบัน
                    // ป้องกันไม่ให้วาด nested array (เช่น movementPath
                    // ของ phase อื่นที่ index shift ไปแล้ว) ต่อในเฟรม
                    // เดียวกันด้วย property tree ที่ยังไม่อัปเดต
                    // -> ต้นเหตุของ ArgumentOutOfRangeException
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (phase.isExpanded)
                    DrawPhase(phase);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button(
                "+ Add Phase"
            ))
            {
                int index =
                    phases.arraySize;

                phases.InsertArrayElementAtIndex(
                    index
                );

                SerializedProperty phase =
                    phases.GetArrayElementAtIndex(
                        index
                    );

                phase.FindPropertyRelative(
                    "phaseName"
                ).stringValue =
                    $"Phase {index + 1}";

                phase.FindPropertyRelative(
                    "phaseHealth"
                ).intValue = 100;

                phase.FindPropertyRelative(
                    "isSpellCard"
                ).boolValue = false;

                phase.FindPropertyRelative(
                    "spellCardName"
                ).stringValue = "";

                phase.FindPropertyRelative(
                    "spellCardTimeLimit"
                ).floatValue = 0f;

                phase.FindPropertyRelative(
                    "transitionTargetPoint"
                ).objectReferenceValue = null;

                phase.FindPropertyRelative(
                    "transitionDuration"
                ).floatValue = 0f;

                phase.FindPropertyRelative(
                    "transitionEase"
                ).enumValueIndex =
                    (int)EaseType.EaseInOut;

                phase.FindPropertyRelative(
                    "movementPath"
                ).arraySize = 0;

                phase.FindPropertyRelative(
                    "loopMovement"
                ).boolValue = true;

                SerializedProperty bulletPattern =
                    phase.FindPropertyRelative(
                        "bulletPattern"
                    );

                bulletPattern.FindPropertyRelative(
                    "pattern"
                ).objectReferenceValue = null;

                bulletPattern.FindPropertyRelative(
                    "startDelay"
                ).floatValue = 0f;

                bulletPattern.FindPropertyRelative(
                    "loop"
                ).boolValue = true;

                // เหตุผลเดียวกับตอนลบ: commit แล้วยกเลิก GUI pass
                // ปัจจุบัน กัน nested array ตัวอื่นวาดต่อด้วย
                // property tree ที่ยังไม่ sync กับ array ใหม่
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawPhase(
            SerializedProperty phase)
        {
            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "phaseName"
                )
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "phaseHealth"
                )
            );

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField(
                "Spell Card / Banner",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "spellCardName"
                ),
                new GUIContent(
                    "Name (Banner)",
                    "ชื่อที่โชว์เป็น Banner ตอนเข้า Phase นี้ - " +
                    "เว้นว่าง = ไม่โชว์ Banner"
                )
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "spellCardTimeLimit"
                ),
                new GUIContent(
                    "Time Limit",
                    "0 = ไม่จำกัดเวลา (ต้องตี HP ให้หมดอย่างเดียว)\n" +
                    "ทำงานไม่ว่าจะติ๊ก Is Spell Card หรือไม่"
                )
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "isSpellCard"
                ),
                new GUIContent(
                    "Counts As Spell Card (สถิติ)",
                    "ติ๊กถ้าอยากให้ Phase นี้นับสถิติ " +
                    "Spell Card Capture\n" +
                    "ไม่ติ๊ก = Banner กับ Timer ยังทำงานปกติ"
                )
            );

            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "transitionTargetPoint"
                )
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "transitionDuration"
                ),
                new GUIContent(
                    "Transition Duration",
                    "0 = ใช้ค่า Default จาก BossV2"
                )
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "transitionEase"
                )
            );

            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "movementPath"
                ),
                new GUIContent(
                    "Movement Path"
                ),
                true
            );

            EditorGUILayout.PropertyField(
                phase.FindPropertyRelative(
                    "loopMovement"
                )
            );

            EditorGUILayout.Space(6);

            DrawPatternTiming(
                phase.FindPropertyRelative(
                    "bulletPattern"
                )
            );
        }

        // ============================================================
        // PHASE TRANSITION
        // ============================================================

        private void DrawPhaseTransition()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "PHASE TRANSITION (Default)",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                phaseTransitionDuration,
                new GUIContent(
                    "Default Transition Duration",
                    "เวลาพักระหว่างเฟสที่ใช้เป็นค่าเริ่มต้น " +
                    "ถ้าเฟสนั้นไม่ได้ระบุแยก"
                )
            );

            EditorGUILayout.PropertyField(
                clearBulletsOnPhaseTransition
            );
        }

        // ============================================================
        // BULLET PATTERN TIMING
        // ============================================================

        private void DrawPatternTiming(
            SerializedProperty schedule)
        {
            EditorGUILayout.LabelField(
                "BULLET PATTERN TIMING",
                EditorStyles.boldLabel);

            if (schedule == null)
            {
                EditorGUILayout.HelpBox(
                    "ไม่พบ Bullet Pattern Schedule SerializedProperty",
                    MessageType.Error);
                return;
            }

            SerializedProperty pattern =
                schedule.FindPropertyRelative("pattern");

            SerializedProperty startDelay =
                schedule.FindPropertyRelative("startDelay");

            SerializedProperty loop =
                schedule.FindPropertyRelative("loop");

            EditorGUILayout.PropertyField(
                pattern,
                new GUIContent(
                    "Bullet Custom Pattern",
                    "ลาก GameObject ที่มี BulletCustomPattern มาใส่ตรงนี้"));

            EditorGUILayout.PropertyField(
                startDelay,
                new GUIContent(
                    "Start Delay",
                    "เวลารอก่อนเริ่ม Pattern หลัง Phase เริ่มยิง"));

            EditorGUILayout.PropertyField(
                loop,
                new GUIContent(
                    "Allow Loop",
                    "เปิด = เคารพการตั้ง Loop ของ Groups ใน BCP\n" +
                    "ปิด = บังคับทุก Group ให้จบครั้งเดียว"));

            if (pattern.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Phase นี้ยังไม่ได้ใส่ BulletCustomPattern",
                    MessageType.Warning);
            }
        }

        // ============================================================
        // DEATH VISUAL
        // ============================================================

        private void DrawDeathVisual()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "DEATH VISUAL",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                blinkCount
            );

            EditorGUILayout.PropertyField(
                blinkInterval
            );

            EditorGUILayout.PropertyField(
                blinkColor
            );
        }

        // ============================================================
        // SCORE / ITEM DROP / REFERENCES
        // ============================================================

        private void DrawScoreAndReferences()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "SCORE",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                scoreValueOnDefeat
            );

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "ITEM DROP",
                EditorStyles.boldLabel
            );

            if (itemDrop != null)
            {
                EditorGUILayout.PropertyField(
                    itemDrop.FindPropertyRelative(
                        "itemPrefab"
                    ),
                    new GUIContent(
                        "Item Prefab",
                        "Prefab ของ ItemDropPickup - " +
                        "Tag ต้องเป็น Life, Spell หรือ Fragment\n" +
                        "เว้นว่างไว้ = Boss ตัวนี้ไม่ดรอปอะไรเลยตอนตาย"
                    )
                );

                EditorGUILayout.PropertyField(
                    itemDrop.FindPropertyRelative(
                        "count"
                    ),
                    new GUIContent(
                        "Count",
                        "จำนวน Item ที่ดรอปตอน Boss ตายครบทุก Phase"
                    )
                );
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "REFERENCES",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                bodySprite
            );

            EditorGUILayout.PropertyField(
                hitCollider
            );
        }
    }
}

#endif