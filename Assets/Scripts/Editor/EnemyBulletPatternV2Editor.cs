#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace EnemySystem
{
    [CustomEditor(typeof(EnemyBulletPatternV2))]
    public class EnemyBulletPatternV2Editor : Editor
    {
        private SerializedProperty patternSchedule;
        private SerializedProperty useFixedSeed;
        private SerializedProperty randomSeed;

        private void OnEnable()
        {
            patternSchedule =
                serializedObject.FindProperty("patternSchedule");

            useFixedSeed =
                serializedObject.FindProperty("useFixedSeed");

            randomSeed =
                serializedObject.FindProperty("randomSeed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Bullet Pattern V2 - Enemy",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Enemy คุมเฉพาะ Pattern ที่ใช้, เวลาเริ่ม และการ Loop\n" +
                "รายละเอียดกระสุนทั้งหมดอยู่ใน BulletCustomPattern",
                MessageType.None);

            DrawPatternTiming();
            DrawRandom();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPatternTiming()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                "PATTERN TIMING",
                EditorStyles.boldLabel);

            if (patternSchedule == null)
            {
                EditorGUILayout.HelpBox(
                    "ไม่พบ Pattern Schedule",
                    MessageType.Error);
                return;
            }

            SerializedProperty pattern =
                patternSchedule.FindPropertyRelative("pattern");
            SerializedProperty startDelay =
                patternSchedule.FindPropertyRelative("startDelay");
            SerializedProperty loop =
                patternSchedule.FindPropertyRelative("loop");

            EditorGUILayout.PropertyField(
                pattern,
                new GUIContent(
                    "Bullet Custom Pattern",
                    "ลาก GameObject ที่มี BulletCustomPattern มาใส่ตรงนี้"));

            EditorGUILayout.PropertyField(
                startDelay,
                new GUIContent(
                    "Start Delay",
                    "เวลารอก่อนเริ่ม Pattern หลังเปิดช่วงยิง"));

            EditorGUILayout.PropertyField(
                loop,
                new GUIContent(
                    "Allow Loop",
                    "เปิด = ใช้ Loop ที่ตั้งไว้ใน BCP\n" +
                    "ปิด = บังคับทุก Group ให้จบครั้งเดียว"));

            if (pattern.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "ยังไม่ได้ใส่ BulletCustomPattern",
                    MessageType.Warning);
            }
        }

        private void DrawRandom()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "RANDOM SEED",
                EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(
                useFixedSeed,
                new GUIContent("Use Fixed Seed"));

            if (useFixedSeed.boolValue)
            {
                EditorGUILayout.PropertyField(
                    randomSeed,
                    new GUIContent("Seed"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "ใช้ random seed ใหม่เมื่อ Executor ถูกสร้างหรือรีเซ็ต",
                    MessageType.None);
            }
        }
    }
}

#endif