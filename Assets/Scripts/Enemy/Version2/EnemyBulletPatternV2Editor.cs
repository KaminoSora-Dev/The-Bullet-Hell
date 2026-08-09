#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace EnemySystem
{
    [CustomEditor(typeof(EnemyBulletPatternV2))]
    public class EnemyBulletPatternV2Editor : Editor
    {
        private SerializedProperty patterns;
        private SerializedProperty useFixedSeed;
        private SerializedProperty randomSeed;

        private void OnEnable()
        {
            patterns =
                serializedObject.FindProperty(
                    "patterns");

            useFixedSeed =
                serializedObject.FindProperty(
                    "useFixedSeed");

            randomSeed =
                serializedObject.FindProperty(
                    "randomSeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Bullet Pattern V2",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Pattern อยู่บน Enemy ตัวนี้โดยตรง ไม่ต้องสร้าง Asset หรือ Runner แยก",
                MessageType.None);

            DrawPatterns();
            DrawRandom();

            serializedObject.ApplyModifiedProperties();
        }

        // ============================================================
        // PATTERNS
        // ============================================================

        private void DrawPatterns()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "PATTERNS",
                EditorStyles.boldLabel);

            for (int i = 0;
                 i < patterns.arraySize;
                 i++)
            {
                SerializedProperty group =
                    patterns.GetArrayElementAtIndex(i);

                SerializedProperty groupName =
                    group.FindPropertyRelative(
                        "groupName");

                string title =
                    string.IsNullOrWhiteSpace(
                        groupName.stringValue)
                        ? $"Pattern {i + 1}"
                        : groupName.stringValue;

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                group.isExpanded =
                    EditorGUILayout.Foldout(
                        group.isExpanded,
                        title,
                        true);

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)))
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

            if (GUILayout.Button(
                "+ Add Pattern"))
            {
                int index =
                    patterns.arraySize;

                patterns.InsertArrayElementAtIndex(
                    index);

                SerializedProperty group =
                    patterns.GetArrayElementAtIndex(
                        index);

                group.FindPropertyRelative(
                    "groupName").stringValue =
                    $"Pattern {index + 1}";

                group.FindPropertyRelative(
                    "initialDelay").floatValue =
                    0f;

                group.FindPropertyRelative(
                    "loopPattern").boolValue =
                    true;

                group.FindPropertyRelative(
                    "bursts").arraySize = 0;
            }
        }

        // ============================================================
        // PATTERN
        // ============================================================

        private void DrawGroup(
            SerializedProperty group)
        {
            EditorGUILayout.PropertyField(
                group.FindPropertyRelative(
                    "groupName"));

            EditorGUILayout.Space(2);

            // --------------------------------------------------------
            // Initial Delay
            //
            // This delay happens only when this Pattern starts.
            // It is NOT repeated when the Pattern loops.
            // --------------------------------------------------------

            SerializedProperty initialDelay =
                group.FindPropertyRelative(
                    "initialDelay");

            EditorGUILayout.PropertyField(
                initialDelay,
                new GUIContent(
                    "Initial Delay",
                    "เวลารอก่อน Pattern นี้เริ่มทำงานครั้งแรก"));

            EditorGUILayout.PropertyField(
                group.FindPropertyRelative(
                    "loopPattern"));

            EditorGUILayout.Space(4);

            DrawBursts(
                group.FindPropertyRelative(
                    "bursts"));
        }

        // ============================================================
        // BURSTS
        // ============================================================

        private void DrawBursts(
            SerializedProperty bursts)
        {
            EditorGUILayout.LabelField(
                "Bursts",
                EditorStyles.boldLabel);

            for (int i = 0;
                 i < bursts.arraySize;
                 i++)
            {
                SerializedProperty burst =
                    bursts.GetArrayElementAtIndex(i);

                SerializedProperty name =
                    burst.FindPropertyRelative(
                        "name");

                string title =
                    string.IsNullOrWhiteSpace(
                        name.stringValue)
                        ? $"Burst {i + 1}"
                        : name.stringValue;

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                burst.isExpanded =
                    EditorGUILayout.Foldout(
                        burst.isExpanded,
                        title,
                        true);

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)))
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

            if (GUILayout.Button(
                "+ Add Burst"))
            {
                int index =
                    bursts.arraySize;

                bursts.InsertArrayElementAtIndex(
                    index);

                SerializedProperty burst =
                    bursts.GetArrayElementAtIndex(
                        index);

                burst.FindPropertyRelative(
                    "name").stringValue =
                    $"Burst {index + 1}";

                burst.FindPropertyRelative(
                    "repeatCount").intValue = 1;

                burst.FindPropertyRelative(
                    "repeatInterval").floatValue =
                    0.1f;

                burst.FindPropertyRelative(
                    "delayAfter").floatValue =
                    0.5f;

                burst.FindPropertyRelative(
                    "bulletCount").intValue = 1;
            }
        }

        private void DrawBurst(
            SerializedProperty burst)
        {
            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "name"));

            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Timing",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "repeatCount"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "repeatInterval"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "delayAfter"));

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField(
                "Spawn",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "bulletPrefab"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative(
                    "bulletCount"));

            SerializedProperty spawn =
                burst.FindPropertyRelative(
                    "spawn");

            SerializedProperty spawnMode =
                spawn.FindPropertyRelative(
                    "mode");

            EditorGUILayout.PropertyField(
                spawnMode);

            BulletSpawnMode mode =
                (BulletSpawnMode)
                    spawnMode.enumValueIndex;

            if (mode ==
                BulletSpawnMode.Offset)
            {
                EditorGUILayout.PropertyField(
                    spawn.FindPropertyRelative(
                        "offset"));
            }
            else if (mode ==
                     BulletSpawnMode.Circle)
            {
                EditorGUILayout.PropertyField(
                    spawn.FindPropertyRelative(
                        "circleRadius"));
            }

            EditorGUILayout.Space(3);

            DrawDirection(
                burst.FindPropertyRelative(
                    "direction"));

            EditorGUILayout.Space(3);

            DrawMotion(
                burst.FindPropertyRelative(
                    "motion"));

            EditorGUILayout.Space(3);

            DrawModifiers(
                burst.FindPropertyRelative(
                    "modifiers"));
        }

        // ============================================================
        // DIRECTION
        // ============================================================

        private void DrawDirection(
            SerializedProperty direction)
        {
            EditorGUILayout.LabelField(
                "Direction",
                EditorStyles.boldLabel);

            SerializedProperty mode =
                direction.FindPropertyRelative(
                    "mode");

            EditorGUILayout.PropertyField(
                mode);

            BulletDirectionMode directionMode =
                (BulletDirectionMode)
                    mode.enumValueIndex;

            switch (directionMode)
            {
                case BulletDirectionMode.FixedAngle:

                case BulletDirectionMode.RelativeToOwner:

                case BulletDirectionMode.Rotating:

                    EditorGUILayout.PropertyField(
                        direction.FindPropertyRelative(
                            "angle"));

                    break;
            }

            EditorGUILayout.PropertyField(
                direction.FindPropertyRelative(
                    "spread"));

            if (directionMode ==
                BulletDirectionMode.Rotating)
            {
                EditorGUILayout.PropertyField(
                    direction.FindPropertyRelative(
                        "rotationPerRepeat"));
            }
        }

        // ============================================================
        // MOTION
        // ============================================================

        private void DrawMotion(
            SerializedProperty motion)
        {
            EditorGUILayout.LabelField(
                "Motion",
                EditorStyles.boldLabel);

            SerializedProperty overrideSpeed =
                motion.FindPropertyRelative(
                    "overrideInitialSpeed");

            EditorGUILayout.PropertyField(
                overrideSpeed);

            if (overrideSpeed.boolValue)
            {
                EditorGUILayout.PropertyField(
                    motion.FindPropertyRelative(
                        "initialSpeed"),
                    new GUIContent(
                        "Initial Speed",
                        "ใส่ค่าติดลบได้ - กระสุนจะเคลื่อนที่ถอยหลัง (สวนทาง Direction) ตั้งแต่ Spawn"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "ใช้ Base Speed จาก EnemyBulletV2 Prefab",
                    MessageType.None);
            }

            DrawSpeedTimeline(
                motion.FindPropertyRelative(
                    "speedTimeline"));

            DrawDirectionTimeline(
                motion.FindPropertyRelative(
                    "directionTimeline"));
        }

        private void DrawSpeedTimeline(
            SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Speed Timeline",
                EditorStyles.boldLabel);

            for (int i = 0;
                 i < timeline.arraySize;
                 i++)
            {
                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(
                    $"Transition {i + 1}",
                    EditorStyles.boldLabel);

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative(
                        "targetSpeed"),
                    new GUIContent(
                        "Target Speed",
                        "ใส่ค่าติดลบได้ - กระสุนจะกลับทิศ (เคลื่อนที่สวนทาง Direction เดิม)"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative(
                        "duration"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative(
                        "easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                "+ Add Speed Transition"))
            {
                int index =
                    timeline.arraySize;

                timeline.InsertArrayElementAtIndex(
                    index);

                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(
                        index);

                transition.FindPropertyRelative(
                    "targetSpeed").floatValue =
                    8f;

                transition.FindPropertyRelative(
                    "duration").floatValue =
                    1f;

                transition.FindPropertyRelative(
                    "easing").enumValueIndex =
                    (int)SpeedEasing.Linear;
            }
        }

        // ============================================================
        // DIRECTION TIMELINE (Redirect)
        // ============================================================

        private void DrawDirectionTimeline(
            SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Direction Timeline",
                EditorStyles.boldLabel);

            for (int i = 0;
                 i < timeline.arraySize;
                 i++)
            {
                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(
                    $"Redirect {i + 1}",
                    EditorStyles.boldLabel);

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty mode =
                    transition.FindPropertyRelative(
                        "mode");

                EditorGUILayout.PropertyField(mode);

                DirectionRedirectMode redirectMode =
                    (DirectionRedirectMode)
                        mode.enumValueIndex;

                if (redirectMode ==
                    DirectionRedirectMode.RelativeAngle)
                {
                    EditorGUILayout.PropertyField(
                        transition.FindPropertyRelative(
                            "relativeAngle"),
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

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative(
                        "duration"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative(
                        "easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                "+ Add Direction Redirect"))
            {
                int index =
                    timeline.arraySize;

                timeline.InsertArrayElementAtIndex(
                    index);

                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(
                        index);

                transition.FindPropertyRelative(
                    "mode").enumValueIndex =
                    (int)DirectionRedirectMode.RelativeAngle;

                transition.FindPropertyRelative(
                    "relativeAngle").floatValue =
                    45f;

                transition.FindPropertyRelative(
                    "duration").floatValue =
                    1f;

                transition.FindPropertyRelative(
                    "easing").enumValueIndex =
                    (int)SpeedEasing.Linear;
            }
        }

        // ============================================================
        // RANDOM / VARIATION
        // ============================================================

        private void DrawModifiers(
            SerializedProperty modifiers)
        {
            EditorGUILayout.LabelField(
                "Variation / Random",
                EditorStyles.boldLabel);

            SerializedProperty randomAngle =
                modifiers.FindPropertyRelative(
                    "randomAngle");

            SerializedProperty angleEnabled =
                randomAngle.FindPropertyRelative(
                    "enabled");

            EditorGUILayout.PropertyField(
                angleEnabled,
                new GUIContent(
                    "Random Angle"));

            if (angleEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomAngle.FindPropertyRelative(
                        "range"));

                EditorGUILayout.PropertyField(
                    randomAngle.FindPropertyRelative(
                        "scope"));
            }

            SerializedProperty randomSpeed =
                modifiers.FindPropertyRelative(
                    "randomSpeed");

            SerializedProperty speedEnabled =
                randomSpeed.FindPropertyRelative(
                    "enabled");

            EditorGUILayout.PropertyField(
                speedEnabled,
                new GUIContent(
                    "Random Speed"));

            if (speedEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomSpeed.FindPropertyRelative(
                        "range"));

                EditorGUILayout.PropertyField(
                    randomSpeed.FindPropertyRelative(
                        "scope"));
            }
        }

        // ============================================================
        // RANDOM SEED
        // ============================================================

        private void DrawRandom()
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField(
                "RANDOM SEED",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                useFixedSeed,
                new GUIContent(
                    "Use Fixed Seed"));

            if (useFixedSeed.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomSeed,
                    new GUIContent(
                        "Seed"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "ใช้ random seed ใหม่เมื่อ executor ถูกสร้างใหม่",
                    MessageType.None);
            }
        }
    }
}
#endif
