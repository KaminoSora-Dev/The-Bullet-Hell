#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace EnemySystem
{
    [CustomEditor(typeof(BulletCustomPattern))]
    public class BulletCustomPatternEditor : Editor
    {
        private SerializedProperty patternName;
        private SerializedProperty groups;

        private void OnEnable()
        {
            patternName =
                serializedObject.FindProperty("patternName");

            groups =
                serializedObject.FindProperty("groups");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BulletCustomPattern targetPattern =
                (BulletCustomPattern)target;

            EditorGUILayout.LabelField(
                "Bullet Custom Pattern",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Definition-only Pattern Container\n" +
                "ไม่มี Update และไม่ยิงกระสุนเอง\n" +
                "Boss / Enemy / Burst Trigger จะเป็นผู้เรียกใช้ Pattern นี้",
                MessageType.None);

            EditorGUILayout.PropertyField(
                patternName,
                new GUIContent(
                    "Pattern Name",
                    "ชื่อ Pattern สำหรับอ่านง่ายใน Inspector และ Log"));

            EditorGUILayout.Space(6);

            DrawGroups(targetPattern);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGroups(
            BulletCustomPattern targetPattern)
        {
            EditorGUILayout.LabelField(
                "PATTERN GROUPS",
                EditorStyles.boldLabel);

            for (int i = 0; i < groups.arraySize; i++)
            {
                SerializedProperty group =
                    groups.GetArrayElementAtIndex(i);

                SerializedProperty groupName =
                    group.FindPropertyRelative("groupName");

                string title =
                    string.IsNullOrWhiteSpace(
                        groupName.stringValue)
                        ? $"Group {i + 1}"
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
                    groups.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (group.isExpanded)
                    DrawGroup(group);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Pattern Group"))
            {
                int index = groups.arraySize;

                groups.InsertArrayElementAtIndex(index);

                SerializedProperty group =
                    groups.GetArrayElementAtIndex(index);

                group.FindPropertyRelative("groupName").stringValue =
                    $"Group {index + 1}";

                group.FindPropertyRelative("initialDelay").floatValue = 0f;
                group.FindPropertyRelative("loopPattern").boolValue = true;
                group.FindPropertyRelative("bursts").arraySize = 0;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawGroup(
            SerializedProperty group)
        {
            EditorGUILayout.PropertyField(
                group.FindPropertyRelative("groupName"));

            EditorGUILayout.PropertyField(
                group.FindPropertyRelative("initialDelay"),
                new GUIContent(
                    "Initial Delay",
                    "เวลารอก่อน Group นี้เริ่มทำงาน"));

            EditorGUILayout.PropertyField(
                group.FindPropertyRelative("loopPattern"));

            EditorGUILayout.Space(4);

            DrawBursts(
                group.FindPropertyRelative("bursts"));
        }

        private void DrawBursts(
            SerializedProperty bursts)
        {
            EditorGUILayout.LabelField(
                "BURSTS",
                EditorStyles.boldLabel);

            for (int i = 0; i < bursts.arraySize; i++)
            {
                SerializedProperty burst =
                    bursts.GetArrayElementAtIndex(i);

                SerializedProperty name =
                    burst.FindPropertyRelative("name");

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

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (burst.isExpanded)
                    DrawBurst(burst);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Burst"))
            {
                int index = bursts.arraySize;

                bursts.InsertArrayElementAtIndex(index);

                SerializedProperty burst =
                    bursts.GetArrayElementAtIndex(index);

                burst.FindPropertyRelative("name").stringValue =
                    $"Burst {index + 1}";

                burst.FindPropertyRelative("repeatCount").intValue = 1;
                burst.FindPropertyRelative("repeatInterval").floatValue = 0.1f;
                burst.FindPropertyRelative("delayAfter").floatValue = 0.5f;
                burst.FindPropertyRelative("bulletPrefab").objectReferenceValue = null;
                burst.FindPropertyRelative("bulletCount").intValue = 1;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawBurst(
            SerializedProperty burst)
        {
            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("name"));

            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "TIMING",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("repeatCount"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("repeatInterval"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("delayAfter"));

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                "SPAWN",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("bulletPrefab"));

            EditorGUILayout.PropertyField(
                burst.FindPropertyRelative("bulletCount"));

            SerializedProperty spawn =
                burst.FindPropertyRelative("spawn");

            SerializedProperty spawnMode =
                spawn.FindPropertyRelative("mode");

            EditorGUILayout.PropertyField(spawnMode);

            BulletSpawnMode mode =
                (BulletSpawnMode)spawnMode.enumValueIndex;

            if (mode == BulletSpawnMode.Offset)
            {
                EditorGUILayout.PropertyField(
                    spawn.FindPropertyRelative("offset"));
            }
            else if (mode == BulletSpawnMode.Circle)
            {
                EditorGUILayout.PropertyField(
                    spawn.FindPropertyRelative("circleRadius"));
            }

            EditorGUILayout.Space(4);
            DrawDirection(
                burst.FindPropertyRelative("direction"));

            EditorGUILayout.Space(4);
            DrawMotion(
                burst.FindPropertyRelative("motion"));

            EditorGUILayout.Space(4);
            DrawModifiers(
                burst.FindPropertyRelative("modifiers"));
        }

        private void DrawDirection(
            SerializedProperty direction)
        {
            EditorGUILayout.LabelField(
                "DIRECTION",
                EditorStyles.boldLabel);

            SerializedProperty mode =
                direction.FindPropertyRelative("mode");

            EditorGUILayout.PropertyField(mode);

            BulletDirectionMode directionMode =
                (BulletDirectionMode)mode.enumValueIndex;

            switch (directionMode)
            {
                case BulletDirectionMode.FixedAngle:
                case BulletDirectionMode.RelativeToOwner:
                case BulletDirectionMode.Rotating:
                    EditorGUILayout.PropertyField(
                        direction.FindPropertyRelative("angle"));
                    break;
            }

            EditorGUILayout.PropertyField(
                direction.FindPropertyRelative("spread"));

            if (directionMode ==
                BulletDirectionMode.Rotating)
            {
                EditorGUILayout.PropertyField(
                    direction.FindPropertyRelative(
                        "rotationPerRepeat"));
            }
        }

        private void DrawMotion(
            SerializedProperty motion)
        {
            EditorGUILayout.LabelField(
                "MOTION",
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
                        "ใส่ค่าติดลบได้"));
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

            DrawBurstTimeline(
                motion.FindPropertyRelative(
                    "burstTimeline"));
        }

        private void DrawSpeedTimeline(
            SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Speed Timeline",
                EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
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

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative("targetSpeed"),
                    new GUIContent(
                        "Target Speed"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative("duration"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative("easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                "+ Add Speed Transition"))
            {
                int index = timeline.arraySize;

                timeline.InsertArrayElementAtIndex(index);

                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(index);

                transition.FindPropertyRelative("targetSpeed").floatValue = 8f;
                transition.FindPropertyRelative("duration").floatValue = 1f;
                transition.FindPropertyRelative("easing").enumValueIndex =
                    (int)SpeedEasing.Linear;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawDirectionTimeline(
            SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Direction Timeline / Redirect",
                EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
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

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty mode =
                    transition.FindPropertyRelative("mode");

                EditorGUILayout.PropertyField(mode);

                DirectionRedirectMode redirectMode =
                    (DirectionRedirectMode)mode.enumValueIndex;

                if (redirectMode ==
                    DirectionRedirectMode.RelativeAngle)
                {
                    EditorGUILayout.PropertyField(
                        transition.FindPropertyRelative(
                            "relativeAngle"),
                        new GUIContent(
                            "Relative Angle",
                            "รองรับเกิน 180° สำหรับ Spiral"));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Aim At Player คำนวณครั้งเดียวตอนเริ่ม Redirect",
                        MessageType.None);
                }

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative("duration"));

                EditorGUILayout.PropertyField(
                    transition.FindPropertyRelative("easing"));

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                "+ Add Direction Redirect"))
            {
                int index = timeline.arraySize;

                timeline.InsertArrayElementAtIndex(index);

                SerializedProperty transition =
                    timeline.GetArrayElementAtIndex(index);

                transition.FindPropertyRelative("mode").enumValueIndex =
                    (int)DirectionRedirectMode.RelativeAngle;

                transition.FindPropertyRelative("relativeAngle").floatValue =
                    45f;

                transition.FindPropertyRelative("duration").floatValue =
                    1f;

                transition.FindPropertyRelative("easing").enumValueIndex =
                    (int)SpeedEasing.Linear;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawBurstTimeline(
            SerializedProperty timeline)
        {
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField(
                "Burst Timeline / Pattern Trigger",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Trigger เป็นข้อมูลของ Motion นี้เอง ไม่ต้องสร้าง GameObject แยก\n" +
                "Trigger จะอ้างอิง BulletCustomPattern อื่น และสร้าง Runtime Instance แยกเมื่อถึงเวลา",
                MessageType.None);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty trigger =
                    timeline.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                SerializedProperty name =
                    trigger.FindPropertyRelative("name");

                string title =
                    string.IsNullOrWhiteSpace(name.stringValue)
                        ? $"Trigger {i + 1}"
                        : name.stringValue;

                trigger.isExpanded =
                    EditorGUILayout.Foldout(
                        trigger.isExpanded,
                        title,
                        true);

                if (GUILayout.Button(
                    "X",
                    GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (trigger.isExpanded)
                {
                    EditorGUILayout.PropertyField(
                        name,
                        new GUIContent("Name"));

                    EditorGUILayout.PropertyField(
                        trigger.FindPropertyRelative("time"),
                        new GUIContent(
                            "Time",
                            "เวลานับจากกระสุนตัวนี้ Spawn"));

                    SerializedProperty pattern =
                        trigger.FindPropertyRelative("pattern");

                    EditorGUILayout.PropertyField(
                        pattern,
                        new GUIContent(
                            "BulletCustomPattern"));

                    if (pattern.objectReferenceValue ==
                        target)
                    {
                        EditorGUILayout.HelpBox(
                            "ห้าม Pattern เรียกตัวเองโดยตรง",
                            MessageType.Error);
                    }

                    EditorGUILayout.PropertyField(
                        trigger.FindPropertyRelative(
                            "destroySelfAfterFire"),
                        new GUIContent(
                            "Destroy Self After Fire"));
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button(
                "+ Add Burst Trigger"))
            {
                int index = timeline.arraySize;

                timeline.InsertArrayElementAtIndex(index);

                SerializedProperty trigger =
                    timeline.GetArrayElementAtIndex(index);

                trigger.FindPropertyRelative("name").stringValue =
                    $"Trigger {index + 1}";

                trigger.FindPropertyRelative("time").floatValue =
                    0.5f;

                trigger.FindPropertyRelative(
                    "pattern").objectReferenceValue = null;

                trigger.FindPropertyRelative(
                    "destroySelfAfterFire").boolValue = false;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawModifiers(
            SerializedProperty modifiers)
        {
            EditorGUILayout.LabelField(
                "RANDOM / VARIATION",
                EditorStyles.boldLabel);

            SerializedProperty randomAngle =
                modifiers.FindPropertyRelative("randomAngle");

            SerializedProperty angleEnabled =
                randomAngle.FindPropertyRelative("enabled");

            EditorGUILayout.PropertyField(
                angleEnabled,
                new GUIContent("Random Angle"));

            if (angleEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomAngle.FindPropertyRelative("range"));

                EditorGUILayout.PropertyField(
                    randomAngle.FindPropertyRelative("scope"));
            }

            SerializedProperty randomSpeed =
                modifiers.FindPropertyRelative("randomSpeed");

            SerializedProperty speedEnabled =
                randomSpeed.FindPropertyRelative("enabled");

            EditorGUILayout.PropertyField(
                speedEnabled,
                new GUIContent("Random Speed"));

            if (speedEnabled.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomSpeed.FindPropertyRelative("range"));

                EditorGUILayout.PropertyField(
                    randomSpeed.FindPropertyRelative("scope"));
            }
        }
    }
}

#endif