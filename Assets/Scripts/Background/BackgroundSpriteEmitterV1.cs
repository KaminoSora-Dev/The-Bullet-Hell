using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;

namespace BackgroundSystem
{
    /// <summary>
    /// 1 Pattern การ Spawn Sprite ฉากหลัง - มี Prefab ของตัวเอง, อัตรา Spawn, ทิศทางเริ่มต้น/เปลี่ยนทิศตามเวลา,
    /// และ Speed/Scale ที่เลือกได้ว่าจะ Override (สุ่มตาม Range) หรือใช้ค่า Base จากตัว Prefab เอง (BackgroundSpriteFlowV1)
    /// </summary>
    [System.Serializable]
    public class BackgroundSpritePattern
    {
        public string patternName = "New Pattern";

        [Tooltip("Prefab ที่จะสุ่มเลือก 1 อันทุกครั้งที่ Spawn - ต้อง Prewarm ไว้ใน PoolManager ล่วงหน้า และมี BackgroundSpriteFlowV1 ติดอยู่")]
        public List<GameObject> spritePrefabs = new List<GameObject>();

        [Header("Spawn Rate")]
        [Tooltip("สุ่มช่วงเวลาระหว่างการ Spawn แต่ละครั้ง (วินาที)")]
        public FloatRange spawnInterval = new FloatRange(0.6f, 1.4f);

        [Header("Speed")]
        [Tooltip("ถ้าไม่ติ๊ก จะใช้ Base Speed จากตัว Prefab เอง (BackgroundSpriteFlowV1) แทน")]
        public bool overrideSpeed = false;
        public FloatRange speed = new FloatRange(1f, 2f);

        [Header("Scale")]
        [Tooltip("ถ้าไม่ติ๊ก จะใช้ Base Scale จากตัว Prefab เอง (BackgroundSpriteFlowV1) แทน")]
        public bool overrideScale = false;
        public FloatRange scale = new FloatRange(0.8f, 1.2f);

        [Header("Direction")]
        [Tooltip("มุมทิศทางตอน Spawn - ค่า Default -90 = ไหลลงล่าง")]
        public float initialAngle = -90f;

        [Tooltip("เปลี่ยนทิศทางตามเวลาหลัง Spawn (ไม่บังคับ) - เว้นว่างไว้ถ้าอยากให้ไหลทิศเดียวตลอด")]
        public List<BackgroundDirectionKeyframe> directionTimeline = new List<BackgroundDirectionKeyframe>();

        [System.NonSerialized] public float spawnTimer;
    }

    /// <summary>
    /// ตัวคุมการ Spawn Sprite ฉากหลัง - อ้างอิงโครงสร้างจาก EnemyBulletPatternV2 (Pattern อยู่บน GameObject นี้ตรงๆ
    /// ไม่ต้องมี ScriptableObject/Runner แยก)
    ///
    /// จุด Spawn สุ่มจาก Bounding Box ของมุมทั้ง 4 (topLeft/topRight/bottomLeft/bottomRight) ตรงๆ
    /// ไม่ต้องพึ่ง Collider เลย - ลาก Transform เปล่าๆ 4 จุดมาวางตามมุมพื้นที่ที่ต้องการให้ Spawn
    /// </summary>
    public class BackgroundSpriteEmitterV1 : MonoBehaviour
    {
        [Header("พื้นที่ Spawn (มุมทั้ง 4 - Transform เปล่าๆ ไม่ต้องมี Collider)")]
        [SerializeField] private Transform topLeft;
        [SerializeField] private Transform topRight;
        [SerializeField] private Transform bottomLeft;
        [SerializeField] private Transform bottomRight;

        [Header("Patterns")]
        [SerializeField] private List<BackgroundSpritePattern> patterns = new List<BackgroundSpritePattern>();

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < patterns.Count; i++)
                TickPattern(patterns[i], dt);
        }

        private void TickPattern(BackgroundSpritePattern pattern, float dt)
        {
            pattern.spawnTimer -= dt;

            if (pattern.spawnTimer <= 0f)
            {
                SpawnSprite(pattern);
                pattern.spawnTimer = pattern.spawnInterval.Roll();
            }
        }

        private void SpawnSprite(BackgroundSpritePattern pattern)
        {
            if (pattern.spritePrefabs == null || pattern.spritePrefabs.Count == 0) return;

            if (topLeft == null || topRight == null || bottomLeft == null || bottomRight == null)
            {
                Debug.LogWarning("BackgroundSpriteEmitterV1: ยังลากมุม Spawn ไม่ครบ 4 มุม - ข้ามการ Spawn");
                return;
            }

            GameObject prefab = pattern.spritePrefabs[UnityEngine.Random.Range(0, pattern.spritePrefabs.Count)];
            if (prefab == null) return;

            Vector3 spawnPosition = RollSpawnPosition();

            GameObject instance = PoolManager.Instance.Get(prefab, spawnPosition, Quaternion.identity);
            if (instance == null) return;

            if (!instance.TryGetComponent<BackgroundSpriteFlowV1>(out var sprite))
            {
                Debug.LogError($"BackgroundSpriteEmitterV1: Prefab '{prefab.name}' ไม่มี BackgroundSpriteFlowV1 ติดอยู่", prefab);
                PoolManager.Instance.Return(instance);
                return;
            }

            float? speedOverride = pattern.overrideSpeed
                ? pattern.speed.Roll()
                : (float?)null;

            float? scaleOverride = pattern.overrideScale
                ? pattern.scale.Roll()
                : (float?)null;

            sprite.Configure(pattern.initialAngle, pattern.directionTimeline, speedOverride, scaleOverride);
        }

        /// <summary>
        /// สุ่มตำแหน่งจาก Bounding Box (Min/Max ของทั้ง X และ Y) ที่ล้อมรอบมุมทั้ง 4 - ไม่สนใจว่ามุมทั้ง 4
        /// จะถูกจัดวางเป็นสี่เหลี่ยมตรงเป๊ะหรือไม่ เพราะใช้แค่ค่า Min/Max ของแต่ละแกนจากทั้ง 4 จุด
        /// </summary>
        private Vector3 RollSpawnPosition()
        {
            float minX = Mathf.Min(topLeft.position.x, topRight.position.x, bottomLeft.position.x, bottomRight.position.x);
            float maxX = Mathf.Max(topLeft.position.x, topRight.position.x, bottomLeft.position.x, bottomRight.position.x);
            float minY = Mathf.Min(topLeft.position.y, topRight.position.y, bottomLeft.position.y, bottomRight.position.y);
            float maxY = Mathf.Max(topLeft.position.y, topRight.position.y, bottomLeft.position.y, bottomRight.position.y);

            float x = UnityEngine.Random.Range(minX, maxX);
            float y = UnityEngine.Random.Range(minY, maxY);

            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// วาดกรอบพื้นที่ Spawn ใน Scene View เพื่อเช็คด้วยตาว่ามุมทั้ง 4 อยู่ตำแหน่งที่ต้องการจริง
        /// (ป้องกันปัญหาแบบ Collider เพี้ยนที่เจอมาก่อนหน้านี้ - ตรงนี้เห็นกรอบตรงๆ เลยไม่ต้องเดา)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (topLeft == null || topRight == null || bottomLeft == null || bottomRight == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(topLeft.position, topRight.position);
            Gizmos.DrawLine(topRight.position, bottomRight.position);
            Gizmos.DrawLine(bottomRight.position, bottomLeft.position);
            Gizmos.DrawLine(bottomLeft.position, topLeft.position);
        }
    }
}