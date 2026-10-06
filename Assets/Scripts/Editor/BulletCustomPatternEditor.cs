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
            patternName = serializedObject.FindProperty("patternName");
            groups = serializedObject.FindProperty("groups");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BulletCustomPattern targetPattern = target as BulletCustomPattern;

            EditorGUILayout.LabelField("Bullet Custom Pattern", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Definition-only Pattern Container\n" +
                "ไม่มี Update และไม่ยิงกระสุนเอง\n" +
                "Boss / Enemy / Burst Trigger จะเป็นผู้เรียกใช้ Pattern นี้",
                MessageType.None);

            if (patternName != null)
            {
                EditorGUILayout.PropertyField(
                    patternName,
                    new GUIContent("Pattern Name", "ชื่อ Pattern สำหรับอ่านง่ายใน Inspector และ Log"));
            }

            EditorGUILayout.Space(6);

            if (groups != null && targetPattern != null)
                DrawGroups(targetPattern);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGroups(BulletCustomPattern targetPattern)
        {
            EditorGUILayout.LabelField("PATTERN GROUPS", EditorStyles.boldLabel);

            for (int i = 0; i < groups.arraySize; i++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(i);
                if (group == null)
                    continue;

                SerializedProperty groupName = group.FindPropertyRelative("groupName");
                string title = groupName != null && !string.IsNullOrWhiteSpace(groupName.stringValue)
                    ? groupName.stringValue
                    : $"Group {i + 1}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                group.isExpanded = EditorGUILayout.Foldout(group.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
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

                SerializedProperty group = groups.GetArrayElementAtIndex(index);
                SetString(group, "groupName", $"Group {index + 1}");
                SetFloat(group, "initialDelay", 0f);
                SetBool(group, "loopPattern", true);

                SerializedProperty bursts = group.FindPropertyRelative("bursts");
                if (bursts != null)
                    bursts.arraySize = 0;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawGroup(SerializedProperty group)
        {
            PropertyField(group, "groupName");
            PropertyField(group, "initialDelay", new GUIContent("Initial Delay", "เวลารอก่อน Group นี้เริ่มทำงาน"));
            PropertyField(group, "loopPattern");

            EditorGUILayout.Space(4);

            SerializedProperty bursts = group.FindPropertyRelative("bursts");
            if (bursts != null)
                DrawBursts(bursts);
        }

        private void DrawBursts(SerializedProperty bursts)
        {
            EditorGUILayout.LabelField("BURSTS", EditorStyles.boldLabel);

            for (int i = 0; i < bursts.arraySize; i++)
            {
                SerializedProperty burst = bursts.GetArrayElementAtIndex(i);
                if (burst == null)
                    continue;

                SerializedProperty name = burst.FindPropertyRelative("name");
                string title = name != null && !string.IsNullOrWhiteSpace(name.stringValue)
                    ? name.stringValue
                    : $"Burst {i + 1}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                burst.isExpanded = EditorGUILayout.Foldout(burst.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
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

                SerializedProperty burst = bursts.GetArrayElementAtIndex(index);
                SetString(burst, "name", $"Burst {index + 1}");
                SetInt(burst, "repeatCount", 1);
                SetFloat(burst, "repeatInterval", 0.1f);
                SetFloat(burst, "delayAfter", 0.5f);

                SerializedProperty elements = burst.FindPropertyRelative("elements");
                if (elements != null)
                {
                    elements.arraySize = 0;
                    elements.InsertArrayElementAtIndex(0);

                    SerializedProperty firstElement = elements.GetArrayElementAtIndex(0);
                    SetString(firstElement, "elementName", "Element 1");
                    SetInt(firstElement, "bulletCount", 1);
                    firstElement.isExpanded = true;
                }

                burst.isExpanded = true;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawBurst(SerializedProperty burst)
        {
            PropertyField(burst, "name");

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("TIMING (ใช้ร่วมกันทุก Element ข้างล่าง)", EditorStyles.boldLabel);
            PropertyField(burst, "repeatCount");
            PropertyField(burst, "repeatInterval");
            PropertyField(burst, "delayAfter");

            EditorGUILayout.Space(4);

            SerializedProperty elements = burst.FindPropertyRelative("elements");
            if (elements != null)
                DrawElements(elements);
        }

        private void DrawElements(SerializedProperty elements)
        {
            EditorGUILayout.LabelField("ELEMENTS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1 Element = 1 กระสุน/ทิศทาง/ความเร็ว - ยิงพร้อมกันทุก Element ทุกครั้งที่ Burst นี้ทำงาน\n" +
                "เพิ่มมากกว่า 1 Element เพื่อยิงหลาย Pattern พร้อมกัน เช่น Clockwise + Counter Clockwise",
                MessageType.None);

            for (int i = 0; i < elements.arraySize; i++)
            {
                SerializedProperty element = elements.GetArrayElementAtIndex(i);
                if (element == null)
                    continue;

                SerializedProperty elementName = element.FindPropertyRelative("elementName");
                string title = elementName != null && !string.IsNullOrWhiteSpace(elementName.stringValue)
                    ? elementName.stringValue
                    : $"Element {i + 1}";

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    elements.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                if (element.isExpanded)
                    DrawElement(element);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Element"))
            {
                int index = elements.arraySize;
                elements.InsertArrayElementAtIndex(index);

                SerializedProperty element = elements.GetArrayElementAtIndex(index);
                SetString(element, "elementName", $"Element {index + 1}");
                SetInt(element, "bulletCount", 1);
                element.isExpanded = true;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawElement(SerializedProperty element)
        {
            PropertyField(element, "elementName");

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("SPAWN", EditorStyles.boldLabel);
            PropertyField(element, "bulletPrefab");
            PropertyField(element, "bulletCount");

            SerializedProperty spawn = element.FindPropertyRelative("spawn");
            if (spawn != null)
            {
                SerializedProperty spawnMode = spawn.FindPropertyRelative("mode");
                if (spawnMode != null)
                {
                    EditorGUILayout.PropertyField(spawnMode);
                    BulletSpawnMode mode = (BulletSpawnMode)spawnMode.enumValueIndex;

                    if (mode == BulletSpawnMode.Offset)
                        PropertyField(spawn, "offset");
                    else if (mode == BulletSpawnMode.Circle)
                    {
                        PropertyField(spawn, "circleRadius");
                        PropertyField(spawn, "circleDistribution");
                    }
                }
            }

            EditorGUILayout.Space(4);
            DrawDirection(element.FindPropertyRelative("direction"));

            EditorGUILayout.Space(4);
            DrawMotion(element.FindPropertyRelative("motion"));

            EditorGUILayout.Space(4);
            BulletAdvancedDrawers.DrawTelegraph(element.FindPropertyRelative("telegraph"));

            EditorGUILayout.Space(4);
            BulletAdvancedDrawers.DrawPathStream(element.FindPropertyRelative("stream"));

            EditorGUILayout.Space(4);
            DrawModifiers(element.FindPropertyRelative("modifiers"));
        }

        private void DrawDirection(SerializedProperty direction)
        {
            if (direction == null)
                return;

            EditorGUILayout.LabelField("DIRECTION", EditorStyles.boldLabel);

            SerializedProperty mode = direction.FindPropertyRelative("mode");
            if (mode == null)
                return;

            EditorGUILayout.PropertyField(mode);
            BulletDirectionMode directionMode = (BulletDirectionMode)mode.enumValueIndex;

            switch (directionMode)
            {
                case BulletDirectionMode.FixedAngle:
                case BulletDirectionMode.RelativeToOwner:
                case BulletDirectionMode.Rotating:
                    PropertyField(direction, "angle");
                    break;
            }

            PropertyField(direction, "spread");
            PropertyField(direction, "spreadDistribution");
            PropertyField(direction, "spreadShape");

            if (directionMode == BulletDirectionMode.Rotating)
                PropertyField(direction, "rotationPerRepeat");
        }

        private void DrawMotion(SerializedProperty motion)
        {
            if (motion == null)
                return;

            EditorGUILayout.LabelField("MOTION", EditorStyles.boldLabel);

            SerializedProperty overrideSpeed = motion.FindPropertyRelative("overrideInitialSpeed");
            if (overrideSpeed != null)
            {
                EditorGUILayout.PropertyField(overrideSpeed);
                if (overrideSpeed.boolValue)
                    PropertyField(motion, "initialSpeed", new GUIContent("Initial Speed", "ใส่ค่าติดลบได้"));
                else
                    EditorGUILayout.HelpBox("ใช้ Base Speed จาก EnemyBulletV2 Prefab", MessageType.None);
            }

            DrawSpeedTimeline(motion.FindPropertyRelative("speedTimeline"));
            DrawDirectionTimeline(motion.FindPropertyRelative("directionTimeline"));

            PropertyField(
                motion,
                "syncDirectionTimelineToBurst",
                new GUIContent(
                    "Sync Direction Timeline To Burst",
                    "ติ๊ก = ทุกนัดจาก Burst นี้ (ทุก Repeat) Redirect พร้อมกันที่เวลาจริงเดียวกัน"));

            DrawBurstTimeline(motion.FindPropertyRelative("burstTimeline"));

            EditorGUILayout.Space(4);
            DrawPath(motion.FindPropertyRelative("path"));

            EditorGUILayout.Space(4);
            BulletAdvancedDrawers.DrawHoming(motion.FindPropertyRelative("homing"));

            EditorGUILayout.Space(4);
            BulletAdvancedDrawers.DrawOrbit(motion.FindPropertyRelative("orbit"));
        }

        private void DrawPath(SerializedProperty path)
        {
            if (path == null)
                return;

            EditorGUILayout.LabelField("PATH MOTION (Optional)", EditorStyles.boldLabel);

            SerializedProperty enabled = path.FindPropertyRelative("enabled");
            if (enabled == null)
                return;

            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
                return;

            SerializedProperty pointMode = path.FindPropertyRelative("pointMode");
            if (pointMode != null)
            {
                EditorGUILayout.PropertyField(pointMode);
                BulletPathPointMode mode = (BulletPathPointMode)pointMode.enumValueIndex;

                EditorGUILayout.LabelField("End Point");
                if (mode == BulletPathPointMode.Offset)
                    PropertyField(path, "endOffset");
                else
                    PropertyField(path, "endTransform");

                SerializedProperty useCurve = path.FindPropertyRelative("useCurve");
                if (useCurve != null)
                {
                    EditorGUILayout.PropertyField(useCurve);
                    if (useCurve.boolValue)
                    {
                        EditorGUILayout.LabelField("Control Point");
                        if (mode == BulletPathPointMode.Offset)
                            PropertyField(path, "controlOffset");
                        else
                            PropertyField(path, "controlTransform");
                    }
                }
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("SPREAD COMPATIBILITY", EditorStyles.boldLabel);
            PropertyField(path, "rotateWithSpread");
            PropertyField(path, "varyMode");
            PropertyField(path, "varyAmount");
            PropertyField(path, "varyScope");

            EditorGUILayout.HelpBox(
                "Rotate With Spread = หมุน End/Control Point ตามมุม Spread ของแต่ละนัด\n" +
                "Vary = แยกเส้นทางด้านข้างเพื่อไม่ให้หลาย Spread Shot ซ้อนกัน\n" +
                "ทั้งหมด default ปิด/0 จึงไม่เปลี่ยน Path Motion เดิม",
                MessageType.None);

            SerializedProperty speedMode = path.FindPropertyRelative("speedMode");
            if (speedMode != null)
            {
                EditorGUILayout.PropertyField(
                    speedMode,
                    new GUIContent(
                        "Speed Mode",
                        "Path Timing = ใช้ Duration + Easing ของ Path | Speed Timeline = ใช้ Speed Timeline ของ Element นี้ควบคุมความเร็วจริง"));

                EditorGUILayout.HelpBox(
                    speedMode.enumValueIndex == (int)PathSpeedMode.SpeedTimeline
                        ? "Speed Timeline Mode: ระบบจะใช้ Speed Timeline ด้านบนของ Element นี้โดยตรง (Target Speed / Duration / Easing) เป็นความเร็วของการเดินตาม Path\n" +
                          "Duration / Easing ด้านล่างจะเป็นค่าของ Path Timing และจะไม่เป็นตัวขับความเร็วเมื่อเลือก Speed Timeline"
                        : "Path Timing Mode: ใช้ Duration + Easing ด้านล่างแบบเดิม 100% และ Speed Timeline จะไม่ถูกใช้ระหว่าง Path",
                    MessageType.None);
            }

            PropertyField(
                path,
                "duration",
                new GUIContent(
                    "Path Duration",
                    "ใช้เมื่อ Speed Mode = Path Timing เท่านั้น"));
            PropertyField(
                path,
                "easing",
                new GUIContent(
                    "Path Easing",
                    "ใช้เมื่อ Speed Mode = Path Timing เท่านั้น"));
            PropertyField(path, "endBehavior");
        }

        private void DrawSpeedTimeline(SerializedProperty timeline)
        {
            if (timeline == null)
                return;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Speed Timeline", EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty transition = timeline.GetArrayElementAtIndex(i);
                if (transition == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Transition {i + 1}", EditorStyles.boldLabel);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
                PropertyField(transition, "targetSpeed", new GUIContent("Target Speed"));
                PropertyField(transition, "duration");
                PropertyField(transition, "easing");
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Speed Transition"))
            {
                int index = timeline.arraySize;
                timeline.InsertArrayElementAtIndex(index);
                SerializedProperty transition = timeline.GetArrayElementAtIndex(index);
                SetFloat(transition, "targetSpeed", 8f);
                SetFloat(transition, "duration", 1f);
                SetEnum(transition, "easing", (int)SpeedEasing.Linear);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawDirectionTimeline(SerializedProperty timeline)
        {
            if (timeline == null)
                return;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Direction Timeline / Redirect", EditorStyles.boldLabel);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty transition = timeline.GetArrayElementAtIndex(i);
                if (transition == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Redirect {i + 1}", EditorStyles.boldLabel);

                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    timeline.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();

                SerializedProperty mode = transition.FindPropertyRelative("mode");
                if (mode != null)
                {
                    EditorGUILayout.PropertyField(mode);
                    DirectionRedirectMode redirectMode = (DirectionRedirectMode)mode.enumValueIndex;

                    if (redirectMode == DirectionRedirectMode.RelativeAngle)
                    {
                        PropertyField(
                            transition,
                            "relativeAngle",
                            new GUIContent("Relative Angle", "รองรับเกิน 180° สำหรับ Spiral"));
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Aim At Player คำนวณครั้งเดียวตอนเริ่ม Redirect", MessageType.None);
                    }
                }

                PropertyField(transition, "duration");
                PropertyField(transition, "easing");
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Direction Redirect"))
            {
                int index = timeline.arraySize;
                timeline.InsertArrayElementAtIndex(index);
                SerializedProperty transition = timeline.GetArrayElementAtIndex(index);
                SetEnum(transition, "mode", (int)DirectionRedirectMode.RelativeAngle);
                SetFloat(transition, "relativeAngle", 45f);
                SetFloat(transition, "duration", 1f);
                SetEnum(transition, "easing", (int)SpeedEasing.Linear);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawBurstTimeline(SerializedProperty timeline)
        {
            if (timeline == null)
                return;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Burst Timeline / Pattern Trigger", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Trigger เป็นข้อมูลของ Motion นี้เอง ไม่ต้องสร้าง GameObject แยก\n" +
                "Trigger จะอ้างอิง BulletCustomPattern อื่น และสร้าง Runtime Instance แยกเมื่อถึงเวลา",
                MessageType.None);

            for (int i = 0; i < timeline.arraySize; i++)
            {
                SerializedProperty trigger = timeline.GetArrayElementAtIndex(i);
                if (trigger == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                SerializedProperty name = trigger.FindPropertyRelative("name");
                string title = name != null && !string.IsNullOrWhiteSpace(name.stringValue)
                    ? name.stringValue
                    : $"Trigger {i + 1}";

                trigger.isExpanded = EditorGUILayout.Foldout(trigger.isExpanded, title, true);

                if (GUILayout.Button("X", GUILayout.Width(24)))
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
                    PropertyField(trigger, "name", new GUIContent("Name"));
                    PropertyField(trigger, "time", new GUIContent("Time", "เวลานับจากกระสุนตัวนี้ Spawn"));

                    SerializedProperty pattern = trigger.FindPropertyRelative("pattern");
                    if (pattern != null)
                    {
                        EditorGUILayout.PropertyField(
                            pattern,
                            new GUIContent("BulletCustomPattern"));

                        if (pattern.objectReferenceValue == target)
                        {
                            EditorGUILayout.HelpBox(
                                "ห้าม Pattern เรียกตัวเองโดยตรง",
                                MessageType.Error);
                        }
                    }

                    PropertyField(
                        trigger,
                        "destroySelfAfterFire",
                        new GUIContent("Destroy Self After Fire"));
                }

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Burst Trigger"))
            {
                int index = timeline.arraySize;
                timeline.InsertArrayElementAtIndex(index);
                SerializedProperty trigger = timeline.GetArrayElementAtIndex(index);
                SetString(trigger, "name", $"Trigger {index + 1}");
                SetFloat(trigger, "time", 0.5f);
                SetObject(trigger, "pattern", null);
                SetBool(trigger, "destroySelfAfterFire", false);
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawModifiers(SerializedProperty modifiers)
        {
            if (modifiers == null)
                return;

            EditorGUILayout.LabelField("RANDOM / VARIATION", EditorStyles.boldLabel);

            SerializedProperty randomAngle = modifiers.FindPropertyRelative("randomAngle");
            if (randomAngle != null)
            {
                SerializedProperty angleEnabled = randomAngle.FindPropertyRelative("enabled");
                if (angleEnabled != null)
                {
                    EditorGUILayout.PropertyField(angleEnabled, new GUIContent("Random Angle"));
                    if (angleEnabled.boolValue)
                    {
                        PropertyField(randomAngle, "range");
                        PropertyField(randomAngle, "scope");
                    }
                }
            }

            SerializedProperty randomSpeed = modifiers.FindPropertyRelative("randomSpeed");
            if (randomSpeed != null)
            {
                SerializedProperty speedEnabled = randomSpeed.FindPropertyRelative("enabled");
                if (speedEnabled != null)
                {
                    EditorGUILayout.PropertyField(speedEnabled, new GUIContent("Random Speed"));
                    if (speedEnabled.boolValue)
                    {
                        PropertyField(randomSpeed, "range");
                        PropertyField(randomSpeed, "scope");
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // Small serialization helpers.
        // These are intentionally null-safe because Unity can create
        // list elements without running the nested field initializers.
        // ------------------------------------------------------------

        private static SerializedProperty Find(SerializedProperty parent, string relative)
        {
            return parent == null ? null : parent.FindPropertyRelative(relative);
        }

        private static void PropertyField(SerializedProperty parent, string relative)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                EditorGUILayout.PropertyField(property);
        }

        private static void PropertyField(
            SerializedProperty parent,
            string relative,
            GUIContent label)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                EditorGUILayout.PropertyField(property, label);
        }

        private static void SetString(SerializedProperty parent, string relative, string value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetFloat(SerializedProperty parent, string relative, float value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetInt(SerializedProperty parent, string relative, int value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.intValue = value;
        }

        private static void SetBool(SerializedProperty parent, string relative, bool value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetEnum(SerializedProperty parent, string relative, int value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.enumValueIndex = value;
        }

        private static void SetObject(SerializedProperty parent, string relative, Object value)
        {
            SerializedProperty property = Find(parent, relative);
            if (property != null)
                property.objectReferenceValue = value;
        }
    }
}

#endif
