#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EnemySystem; 

namespace BossSystem
{
    [CustomEditor(typeof(BossV2))]
    public class BossV2Editor : Editor
    {
        // ---- Entry ----
        private SerializedProperty entryTargetPoint;
        private SerializedProperty entryDuration;
        private SerializedProperty entryEase;

        // ---- Phases ----
        private SerializedProperty phases;

        // ---- Phase Transition ----
        private SerializedProperty phaseTransitionDuration;
        private SerializedProperty clearBulletsOnPhaseTransition;

        // ---- Death Visual ----
        private SerializedProperty blinkCount;
        private SerializedProperty blinkInterval;
        private SerializedProperty blinkColor;

        // ---- Score / References ----
        private SerializedProperty scoreValueOnDefeat;
        private SerializedProperty bodySprite;
        private SerializedProperty hitCollider;

        private void OnEnable()
        {
            entryTargetPoint = serializedObject.FindProperty("entryTargetPoint");
            entryDuration = serializedObject.FindProperty("entryDuration");
            entryEase = serializedObject.FindProperty("entryEase");

            phases = serializedObject.FindProperty("phases");

            phaseTransitionDuration = serializedObject.FindProperty("phaseTransitionDuration");
            clearBulletsOnPhaseTransition = serializedObject.FindProperty("clearBulletsOnPhaseTransition");

            blinkCount = serializedObject.FindProperty("blinkCount");
            blinkInterval = serializedObject.FindProperty("blinkInterval");
            blinkColor = serializedObject.FindProperty("blinkColor");

            scoreValueOnDefeat = serializedObject.FindProperty("scoreValueOnDefeat");
            bodySprite = serializedObject.FindProperty("bodySprite");
            hitCollider = serializedObject.FindProperty("hitCollider");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Boss V2", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "แต่ละ Phase มี HP และ Pattern ของตัวเอง กด + Add Phase เพื่อเพิ่ม Phase ใหม่ " +
                "แล้วกด + Add Pattern ข้างในเพื่อออกแบบลายกระสุนของ Phase นั้น",
                MessageType.None);

            DrawEntry();
            DrawPhases();
            DrawPhaseTransition();
            DrawDeathVisual();
            DrawScoreAndReferences();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEntry()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ENTRY", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                entryTargetPoint,
                new GUIContent("Entry Target Point", "เว้นว่างไว้ถ้าอยากให้เริ่ม Phase 1 ทันทีโดยไม่มีช่วง Entry"));

            EditorGUILayout.PropertyField(entryDuration);
            EditorGUILayout.PropertyField(entryEase);
        }

        private void DrawPhases()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("PHASES", EditorStyles.boldLabel);

            for (int i = 0; i < phases.arraySize; i++)
            {
                SerializedProperty phase = phases.GetArrayElementAtIndex(i);
                SerializedProperty phaseName = phase.FindPropertyRelative("phaseName");

                string title = string.IsNullOrWhiteSpace(phaseName.stringValue)
                    ? $"Phase {i + 1}"
                    : phaseName.stringValue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                phase.isExpanded = EditorGUILayout.Foldout(phase.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    phases.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (phase.isExpanded)
                    DrawPhase(phase);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("+ Add Phase"))
            {
                int index = phases.arraySize;
                phases.InsertArrayElementAtIndex(index);

                SerializedProperty phase = phases.GetArrayElementAtIndex(index);

                phase.FindPropertyRelative("phaseName").stringValue = $"Phase {index + 1}";
                phase.FindPropertyRelative("phaseHealth").intValue = 100;
                phase.FindPropertyRelative("isSpellCard").boolValue = false;
                phase.FindPropertyRelative("spellCardName").stringValue = "";
                phase.FindPropertyRelative("spellCardTimeLimit").floatValue = 0f;
                
                // Initialize default values for new phase
                phase.FindPropertyRelative("transitionTargetPoint").objectReferenceValue = null;
                phase.FindPropertyRelative("transitionDuration").floatValue = 0f;
                phase.FindPropertyRelative("transitionEase").enumValueIndex = (int)EaseType.EaseInOut;
                
                phase.FindPropertyRelative("movementPath").arraySize = 0;
                phase.FindPropertyRelative("loopMovement").boolValue = true;

                phase.FindPropertyRelative("patterns").arraySize = 0;
            }
        }

        private void DrawPhase(SerializedProperty phase)
        {
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("phaseName"));
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("phaseHealth"));

            EditorGUILayout.Space(3);

            SerializedProperty isSpellCard = phase.FindPropertyRelative("isSpellCard");
            EditorGUILayout.PropertyField(isSpellCard, new GUIContent("Is Spell Card"));

            if (isSpellCard.boolValue)
            {
                EditorGUILayout.PropertyField(phase.FindPropertyRelative("spellCardName"));

                EditorGUILayout.PropertyField(
                    phase.FindPropertyRelative("spellCardTimeLimit"),
                    new GUIContent("Time Limit", "0 = ไม่จำกัดเวลา (ต้องตี HP ให้หมดอย่างเดียว)"));
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("transitionTargetPoint"));
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("transitionDuration"), new GUIContent("Transition Duration", "0 = ใช้ค่า Default จาก BossV2"));
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("transitionEase"));

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("movementPath"), new GUIContent("Movement Path"), true);
            EditorGUILayout.PropertyField(phase.FindPropertyRelative("loopMovement"));

            EditorGUILayout.Space(6);

            DrawPatterns(phase.FindPropertyRelative("patterns"));
        }

        private void DrawPhaseTransition()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("PHASE TRANSITION (Default)", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                phaseTransitionDuration, 
                new GUIContent("Default Transition Duration", "เวลาพักระหว่างเฟสที่ใช้เป็นค่าเริ่มต้น ถ้าเฟสนั้นไม่ได้ระบุแยก"));
            EditorGUILayout.PropertyField(clearBulletsOnPhaseTransition);
        }

        private void DrawPatterns(SerializedProperty patterns)
        {
            EditorGUILayout.LabelField("Patterns", EditorStyles.boldLabel);

            for (int i = 0; i < patterns.arraySize; i++)
            {
                SerializedProperty group = patterns.GetArrayElementAtIndex(i);
                SerializedProperty groupName = group.FindPropertyRelative("groupName");

                string title = string.IsNullOrWhiteSpace(groupName.stringValue)
                    ? $"Pattern {i + 1}"
                    : groupName.stringValue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    patterns.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (group.isExpanded)
                    DrawGroup(group);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("+ Add Pattern"))
            {
                int index = patterns.arraySize;
                patterns.InsertArrayElementAtIndex(index);

                SerializedProperty group = patterns.GetArrayElementAtIndex(index);

                group.FindPropertyRelative("groupName").stringValue = $"Pattern {index + 1}";
                group.FindPropertyRelative("initialDelay").floatValue = 0f;
                group.FindPropertyRelative("loopPattern").boolValue = true;
                group.FindPropertyRelative("bursts").arraySize = 0;
            }
        }

        private void DrawGroup(SerializedProperty group)
        {
            EditorGUILayout.PropertyField(group.FindPropertyRelative("groupName"));

            EditorGUILayout.Space(2);

            SerializedProperty initialDelay = group.FindPropertyRelative("initialDelay");

            EditorGUILayout.PropertyField(
                initialDelay,
                new GUIContent("Initial Delay", "เวลารอก่อน Pattern นี้เริ่มทำงานครั้งแรก"));

            EditorGUILayout.PropertyField(group.FindPropertyRelative("loopPattern"));

            EditorGUILayout.Space(4);

            DrawBursts(group.FindPropertyRelative("bursts"));
        }

        private void DrawBursts(SerializedProperty bursts)
        {
            EditorGUILayout.LabelField("Bursts", EditorStyles.boldLabel);

            for (int i = 0; i < bursts.arraySize; i++)
            {
                SerializedProperty burst = bursts.GetArrayElementAtIndex(i);
                SerializedProperty name = burst.FindPropertyRelative("name");

                string title = string.IsNullOrWhiteSpace(name.stringValue)
                    ? $"Burst {i + 1}"
                    : name.stringValue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                burst.isExpanded = EditorGUILayout.Foldout(burst.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    bursts.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (burst.isExpanded)
                    DrawBurst(burst);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(2);

            if (GUILayout.Button("+ Add Burst"))
            {
                int index = bursts.arraySize;
                bursts.InsertArrayElementAtIndex(index);

                SerializedProperty burst = bursts.GetArrayElementAtIndex(index);

                burst.FindPropertyRelative("name").stringValue = $"Burst {index + 1}";
                burst.FindPropertyRelative("repeatCount").intValue = 1;
                burst.FindPropertyRelative("repeatInterval").floatValue = 0.1f;
                burst.FindPropertyRelative("delayAfter").floatValue = 0.5f;
                burst.FindPropertyRelative("bulletCount").intValue = 1;
            }
        }

        private void DrawBurst(SerializedProperty burst)
        {
            EditorGUILayout.PropertyField(burst.FindPropertyRelative("name"));

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(burst.FindPropertyRelative("repeatCount"));
            EditorGUILayout.PropertyField(burst.FindPropertyRelative("repeatInterval"));
            EditorGUILayout.PropertyField(burst.FindPropertyRelative("delayAfter"));

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Spawn", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(burst.FindPropertyRelative("bulletPrefab"));
            EditorGUILayout.PropertyField(burst.FindPropertyRelative("bulletCount"));

            SerializedProperty spawn = burst.FindPropertyRelative("spawn");
            SerializedProperty spawnMode = spawn.FindPropertyRelative("mode");

            EditorGUILayout.PropertyField(spawnMode);

            BulletSpawnMode mode = (BulletSpawnMode)spawnMode.enumValueIndex;

            if (mode == BulletSpawnMode.Offset)
            {
                EditorGUILayout.PropertyField(spawn.FindPropertyRelative("offset"));
            }
            else if (mode == BulletSpawnMode.Circle)
            {
                EditorGUILayout.PropertyField(spawn.FindPropertyRelative("circleRadius"));
            }

            EditorGUILayout.Space(3);
            DrawDirection(burst.FindPropertyRelative("direction"));

            EditorGUILayout.Space(3);
            DrawMotion(burst.FindPropertyRelative("motion"));

            EditorGUILayout.Space(3);
            DrawModifiers(burst.FindPropertyRelative("modifiers"));
        }

        private void DrawDirection(SerializedProperty direction)
        {
            EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);

            SerializedProperty mode = direction.FindPropertyRelative("mode");
            EditorGUILayout.PropertyField(mode);

            BulletDirectionMode directionMode = (BulletDirectionMode)mode.enumValueIndex;

            switch (directionMode)
            {
                case BulletDirectionMode.FixedAngle:
                case BulletDirectionMode.RelativeToOwner:
                case BulletDirectionMode.Rotating:
                    EditorGUILayout.PropertyField(direction.FindPropertyRelative("angle"));
                    break;
            }

            EditorGUILayout.PropertyField(direction.FindPropertyRelative("spread"));

            if (directionMode == BulletDirectionMode.Rotating)
            {
                EditorGUILayout.PropertyField(direction.FindPropertyRelative("rotationPerRepeat"));
            }
        }

        private void DrawMotion(SerializedProperty motion)
        {
            EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);

            SerializedProperty overrideSpeed = motion.FindPropertyRelative("overrideInitialSpeed");
            EditorGUILayout.PropertyField(overrideSpeed);

            if (overrideSpeed.boolValue)
            {
                EditorGUILayout.PropertyField(
                    motion.FindPropertyRelative("initialSpeed"),
                    new GUIContent("Initial Speed", "ใส่ค่าติดลบได้ - กระสุนจะเคลื่อนที่ถอยหลัง (สวนทาง Direction) ตั้งแต่ Spawn"));
            }
            else
            {
                EditorGUILayout.HelpBox("ใช้ Base Speed จาก EnemyBulletV2 Prefab", MessageType.None);
            }

            DrawSpeedTimeline(motion.FindPropertyRelative("speedTimeline"));
            DrawDirectionTimeline(motion.FindPropertyRelative("directionTimeline"));
        }

        private void DrawSpeedTimeline(SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Speed Timeline", EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty transition = timeline.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"Transition {i + 1}", EditorStyles.boldLabel);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(transition.FindPropertyRelative("targetSpeed"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Speed Transition"))
            {
                int index = timeline.arraySize;
                timeline.InsertArrayElementAtIndex(index);

                SerializedProperty transition = timeline.GetArrayElementAtIndex(index);

                transition.FindPropertyRelative("targetSpeed").floatValue = 8f;
                transition.FindPropertyRelative("duration").floatValue = 1f;
                transition.FindPropertyRelative("easing").enumValueIndex = (int)SpeedEasing.Linear;
            }
        }

        private void DrawDirectionTimeline(SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Direction Timeline", EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty transition = timeline.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"Redirect {i + 1}", EditorStyles.boldLabel);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty mode = transition.FindPropertyRelative("mode");
                EditorGUILayout.PropertyField(mode);

                DirectionRedirectMode redirectMode = (DirectionRedirectMode)mode.enumValueIndex;

                if (redirectMode == DirectionRedirectMode.RelativeAngle)
                {
                    EditorGUILayout.PropertyField(
                        transition.FindPropertyRelative("relativeAngle"),
                        new GUIContent(
                            "Relative Angle",
                            "หมุนจากทิศตอนเริ่ม Redirect นี้ไปกี่องศา (+ = ทวนเข็ม, - = ตามเข็ม) รองรับเกิน 180° สำหรับลาย spiral"));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "หมุนเข้าหาผู้เล่น ณ ตำแหน่งตอนเริ่ม Redirect นี้เท่านั้น (คำนวณครั้งเดียว ไม่ Track ตามตลอดเวลา)",
                        MessageType.None);
                }

                EditorGUILayout.PropertyField(transition.FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(transition.FindPropertyRelative("easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Direction Redirect"))
            {
                int index = timeline.arraySize;
                timeline.InsertArrayElementAtIndex(index);

                SerializedProperty transition = timeline.GetArrayElementAtIndex(index);

                transition.FindPropertyRelative("mode").enumValueIndex = (int)DirectionRedirectMode.RelativeAngle;
                transition.FindPropertyRelative("relativeAngle").floatValue = 45f;
                transition.FindPropertyRelative("duration").floatValue = 1f;
                transition.FindPropertyRelative("easing").enumValueIndex = (int)SpeedEasing.Linear;
            }
        }

        private void DrawModifiers(SerializedProperty modifiers)
        {
            EditorGUILayout.LabelField("Variation / Random", EditorStyles.boldLabel);

            SerializedProperty randomAngle = modifiers.FindPropertyRelative("randomAngle");
            SerializedProperty angleEnabled = randomAngle.FindPropertyRelative("enabled");

            EditorGUILayout.PropertyField(angleEnabled, new GUIContent("Random Angle"));

            if (angleEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(randomAngle.FindPropertyRelative("range"));
                EditorGUILayout.PropertyField(randomAngle.FindPropertyRelative("scope"));
            }

            SerializedProperty randomSpeed = modifiers.FindPropertyRelative("randomSpeed");
            SerializedProperty speedEnabled = randomSpeed.FindPropertyRelative("enabled");

            EditorGUILayout.PropertyField(speedEnabled, new GUIContent("Random Speed"));

            if (speedEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(randomSpeed.FindPropertyRelative("range"));
                EditorGUILayout.PropertyField(randomSpeed.FindPropertyRelative("scope"));
            }
        }

        private void DrawDeathVisual()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("DEATH VISUAL", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(blinkCount);
            EditorGUILayout.PropertyField(blinkInterval);
            EditorGUILayout.PropertyField(blinkColor);
        }

        private void DrawScoreAndReferences()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("SCORE", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(scoreValueOnDefeat);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("REFERENCES", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(bodySprite);
            EditorGUILayout.PropertyField(hitCollider);
        }
    }
}
#endif