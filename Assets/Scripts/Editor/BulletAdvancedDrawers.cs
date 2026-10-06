#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// Inspector drawers สำหรับ Advanced Bullet Options
    /// ทุก Property ถูก null-guard เพื่อรองรับ Unity List-Add/ข้อมูลเก่าที่ field ใหม่ยังไม่มี
    /// </summary>
    internal static class BulletAdvancedDrawers
    {
        public static void DrawHoming(SerializedProperty homing)
        {
            if (homing == null)
                return;

            EditorGUILayout.LabelField("HOMING (Optional)", EditorStyles.boldLabel);

            SerializedProperty enabled = homing.FindPropertyRelative("enabled");
            if (enabled == null)
                return;

            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
                return;

            EditorGUILayout.PropertyField(homing.FindPropertyRelative("startDelay"));
            EditorGUILayout.PropertyField(homing.FindPropertyRelative("duration"));
            EditorGUILayout.PropertyField(homing.FindPropertyRelative("turnSpeed"));
            EditorGUILayout.PropertyField(homing.FindPropertyRelative("keepSpread"));

            EditorGUILayout.HelpBox(
                "Keep Spread = 0: ทุกนัดจะเลี้ยวเข้าหาผู้เล่นจุดเดียวกันเหมือนเดิม\n" +
                "Keep Spread = 1: แต่ละนัดจะรักษามุมเบี่ยงจาก Spread ไว้ขณะ Homing\n" +
                "ระหว่าง Homing Direction Timeline จะหยุดชั่วคราว แล้วเดินต่อหลัง Homing จบ",
                MessageType.None);
        }

        public static void DrawOrbit(SerializedProperty orbit)
        {
            if (orbit == null)
                return;

            EditorGUILayout.LabelField("ORBIT (Optional)", EditorStyles.boldLabel);

            SerializedProperty enabled = orbit.FindPropertyRelative("enabled");
            if (enabled == null)
                return;

            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
                return;

            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("radius"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("angularSpeed"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("duration"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("radiusBlendTime"));

            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("radiusVary"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("radiusVaryScope"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("startAngleVary"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("startAngleVaryScope"));
            EditorGUILayout.PropertyField(orbit.FindPropertyRelative("releaseMode"));

            EditorGUILayout.HelpBox(
                "Radius Vary = เพิ่ม/ลดรัศมีเริ่มต้นของแต่ละนัด\n" +
                "Start Angle Vary = เพิ่ม/ลดมุมเริ่มต้นของแต่ละนัด\n" +
                "ค่า 0 ทั้งหมด = Orbit เดิม และ Spread ยังคงกำหนดมุมเริ่มของแต่ละนัดได้",
                MessageType.None);
        }

        public static void DrawTelegraph(SerializedProperty telegraph)
        {
            if (telegraph == null)
                return;

            EditorGUILayout.LabelField("DANGER ZONE (Optional)", EditorStyles.boldLabel);

            SerializedProperty enabled = telegraph.FindPropertyRelative("enabled");
            if (enabled == null)
                return;

            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
                return;

            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("duration"));
            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("lineWidth"));
            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("color"));
            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("lineLength"));
            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("sortingOrder"));
            EditorGUILayout.PropertyField(telegraph.FindPropertyRelative("showOncePerPattern"));

            EditorGUILayout.HelpBox(
                "ปิด Show Once = พฤติกรรมเดิม: กระสุน/Stream Session ที่เปิด Telegraph จะแสดงเส้นของตัวเอง\n" +
                "เปิด Show Once = แสดงเส้นหลักเพียงครั้งเดียวต่อ BCP และเฉพาะ Repeat แรก\n" +
                "ถ้าใช้ Path + Spread เส้นหลักอ้างอิงนัดแรก จึงไม่พยายามวาดทุก Lane",
                MessageType.None);
        }

        public static void DrawPathStream(SerializedProperty stream)
        {
            if (stream == null)
                return;

            EditorGUILayout.LabelField("PATH STREAM (Optional)", EditorStyles.boldLabel);

            SerializedProperty enabled = stream.FindPropertyRelative("enabled");
            if (enabled == null)
                return;

            EditorGUILayout.PropertyField(enabled);
            if (!enabled.boolValue)
                return;

            SerializedProperty mode = stream.FindPropertyRelative("mode");
            if (mode != null)
                EditorGUILayout.PropertyField(mode);

            PathStreamMode streamMode =
                mode != null
                    ? (PathStreamMode)mode.enumValueIndex
                    : PathStreamMode.FlowAlongPath;

            SerializedProperty startMode = stream.FindPropertyRelative("startMode");
            if (startMode != null)
                EditorGUILayout.PropertyField(startMode);

            if (startMode != null &&
                (PathStreamStartMode)startMode.enumValueIndex == PathStreamStartMode.CustomTransform)
            {
                EditorGUILayout.PropertyField(stream.FindPropertyRelative("startTransform"));
            }

            EditorGUILayout.PropertyField(stream.FindPropertyRelative("waypoints"), true);
            EditorGUILayout.PropertyField(stream.FindPropertyRelative("useCurve"));
            EditorGUILayout.PropertyField(stream.FindPropertyRelative("streamDuration"));
            EditorGUILayout.PropertyField(stream.FindPropertyRelative("spawnInterval"));
            EditorGUILayout.PropertyField(
                stream.FindPropertyRelative("pathSpeed"),
                new GUIContent(
                    streamMode == PathStreamMode.FlowAlongPath ? "Flow Speed" : "Sweep Speed",
                    streamMode == PathStreamMode.FlowAlongPath
                        ? "ความเร็วสำรองเมื่อ Speed Mode = Path Timing - 0 = ใช้ความเร็วของกระสุน"
                        : "ความเร็วที่จุด Spawn เดินตาม Path - 0 = เดินถึงปลายพอดีเมื่อครบ Stream Duration"));

            SerializedProperty speedMode = stream.FindPropertyRelative("speedMode");
            if (speedMode != null)
            {
                EditorGUILayout.PropertyField(
                    speedMode,
                    new GUIContent(
                        "Speed Mode",
                        "Path Timing = ใช้ Path Speed เดิม | Speed Timeline = ใช้ Speed Timeline ของ Element เดียวกัน"));
            }

            if (streamMode == PathStreamMode.FlowAlongPath)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField("SPREAD COMPATIBILITY", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(stream.FindPropertyRelative("rotateWithSpread"));
                EditorGUILayout.PropertyField(stream.FindPropertyRelative("varyMode"));
                EditorGUILayout.PropertyField(stream.FindPropertyRelative("varyAmount"));
                EditorGUILayout.PropertyField(stream.FindPropertyRelative("varyScope"));

                EditorGUILayout.PropertyField(stream.FindPropertyRelative("endBehavior"));

                EditorGUILayout.HelpBox(
                    "Flow: แต่ละ Spawn จะสร้างสำเนาของ Path ให้กระสุนนัดนั้น\n" +
                    "Speed Timeline: ใช้ Speed Timeline ที่อยู่ใน Motion ของ Element เดียวกัน โดย Target Speed เป็นหน่วย/วินาที\n" +
                    "Rotate With Spread = หมุนเส้นรอบจุดเริ่มตาม Spread\n" +
                    "Vary = เลื่อนเส้นด้านข้างเพื่อไม่ให้หลาย Lane ซ้อนกัน\n" +
                    "Sweep ไม่ใช้สองตัวเลือกนี้ เพราะ Sweep ใช้ Path เป็นตำแหน่ง Spawn แล้วให้กระสุนบินตาม Direction/Spread ปกติ",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Sweep: จุด Spawn เดินไปตาม Path แล้วกระสุนแต่ละนัดถูกสร้าง ณ ตำแหน่งนั้น " +
                    "ก่อนบินต่อด้วย Direction / Spread / Motion ของ Element ตามปกติ\n" +
                    "Spread จึงไม่ถูก Path เขียนทับแบบ Flow",
                    MessageType.None);
            }

            EditorGUILayout.HelpBox(
                "Start Mode: Element Spawn = เริ่มจาก Spawn ของ Element | First Waypoint = Waypoint แรก | " +
                "Custom Transform = Transform ที่ลากมาใส่\n" +
                "Transform ทุกตัวถูกอ่านสดทุกครั้งที่สร้าง Path",
                MessageType.None);
        }
    }
}

#endif
