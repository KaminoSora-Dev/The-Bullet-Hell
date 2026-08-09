using System;
using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;

namespace EnemySystem
{
    // ============================================================
    // V2 PATTERN DATA
    // ============================================================

    public enum BulletDirectionMode
    {
        AimAtPlayer,
        FixedAngle,
        RelativeToOwner,
        TowardPlayer,
        AwayFromPlayer,
        Random,
        Rotating
    }

    public enum BulletSpawnMode
    {
        OwnerCenter,
        Offset,
        Circle
    }

    public enum SpeedEasing
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    public enum RandomScope
    {
        PerPattern,
        PerBurst,
        PerShot
    }

    /// <summary>
    /// วิธีคำนวณทิศทางเป้าหมายของ Direction Redirect แต่ละจุดใน Direction Timeline
    /// RelativeAngle = หมุนจากทิศตอนเริ่ม Redirect นี้ไปกี่องศา (รองรับเกิน 180 องศาสำหรับลาย spiral)
    /// AimAtPlayer   = หมุนเข้าหาตำแหน่งผู้เล่น ณ ตอนเริ่ม Redirect นี้เท่านั้น (คำนวณครั้งเดียว ไม่ track ตามตลอด)
    /// </summary>
    public enum DirectionRedirectMode
    {
        RelativeAngle,
        AimAtPlayer
    }

    [Serializable]
    public class SpeedTransition
    {
        [Tooltip("ความเร็วเป้าหมายของช่วงนี้ - ใส่ค่าติดลบได้ กระสุนจะเคลื่อนที่ถอยหลัง (สวนทาง Direction ที่ตั้งไว้)")]
        public float targetSpeed = 8f;

        [Min(0f)] public float duration = 1f;
        public SpeedEasing easing = SpeedEasing.Linear;
    }

    /// <summary>
    /// 1 จุดของ Direction Timeline - หมุนทิศทางกระสุนจากค่าปัจจุบัน ไปยังเป้าหมายภายใน Duration ที่กำหนด
    /// ทำงานคู่ขนานไปกับ Speed Timeline (คนละ Timeline กัน ควบคุมคนละแกน)
    /// </summary>
    [Serializable]
    public class DirectionTransition
    {
        [Tooltip("Relative Angle = หมุนจากทิศปัจจุบันไปกี่องศา | Aim At Player = หมุนเข้าหาผู้เล่น ณ ตอนเริ่ม Redirect นี้")]
        public DirectionRedirectMode mode = DirectionRedirectMode.RelativeAngle;

        [Tooltip("ใช้กับ Relative Angle เท่านั้น - องศาที่จะหมุนเพิ่มจากทิศปัจจุบัน (+ = ทวนเข็มนาฬิกา, - = ตามเข็มนาฬิกา) " +
                 "ใส่เกิน 180 หรือติดลบเกิน -180 ได้ตามปกติ เพื่อทำลาย spiral")]
        public float relativeAngle = 0f;

        [Min(0f)] public float duration = 1f;
        public SpeedEasing easing = SpeedEasing.Linear;
    }

    [Serializable]
    public class BulletMotionSettings
    {
        [Tooltip("ใช้ความเร็ว default จาก EnemyBulletV2 prefab ถ้าไม่ติ๊ก Override")]
        public bool overrideInitialSpeed = false;

        [Tooltip("ความเร็วเริ่มต้น - ใส่ค่าติดลบได้ กระสุนจะเริ่มเคลื่อนที่ถอยหลัง (สวนทาง Direction) ตั้งแต่ Spawn")]
        public float initialSpeed = 6f;

        [Tooltip("เปลี่ยนความเร็วจากค่าปัจจุบันไป Target ภายใน Duration ตั้งแต่กระสุน Spawn - Target Speed ใส่ค่าติดลบได้ " +
                 "เพื่อให้กระสุนกลับทิศ (เคลื่อนที่สวนทาง Direction เดิม)")]
        public List<SpeedTransition> speedTimeline = new List<SpeedTransition>();

        [Tooltip("เปลี่ยนทิศทางกระสุนตามเวลา ตั้งแต่กระสุน Spawn - ทำงานคู่ขนานไปกับ Speed Timeline คนละแกนกัน")]
        public List<DirectionTransition> directionTimeline = new List<DirectionTransition>();
    }

    [Serializable]
    public class BulletSpawnSettings
    {
        public BulletSpawnMode mode = BulletSpawnMode.OwnerCenter;

        [Tooltip("ใช้เมื่อ Spawn Mode = Offset")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("ใช้เมื่อ Spawn Mode = Circle")]
        [Min(0f)] public float circleRadius = 0f;
    }

    [Serializable]
    public class BulletDirectionSettings
    {
        public BulletDirectionMode mode = BulletDirectionMode.AimAtPlayer;

        [Tooltip("ใช้กับ Fixed Angle / Relative To Owner / Rotating")]
        public float angle = -90f;

        [Tooltip("มุมรวมของการกระจายกระสุน เช่น 45 = -22.5 ถึง +22.5")]
        public float spread = 0f;

        [Tooltip("ใช้กับ Rotating: หมุนเพิ่มกี่องศาต่อ Repeat")]
        public float rotationPerRepeat = 0f;
    }

    [Serializable]
    public class RandomAngleModifier
    {
        public bool enabled = false;
        [Min(0f)] public float range = 0f;
        public RandomScope scope = RandomScope.PerShot;
    }

    [Serializable]
    public class RandomSpeedModifier
    {
        public bool enabled = false;
        [Min(0f)] public float range = 0f;
        public RandomScope scope = RandomScope.PerShot;
    }

    [Serializable]
    public class BulletPatternModifiers
    {
        public RandomAngleModifier randomAngle = new RandomAngleModifier();
        public RandomSpeedModifier randomSpeed = new RandomSpeedModifier();

        public bool HasAnyRandom =>
            randomAngle != null && randomAngle.enabled ||
            randomSpeed != null && randomSpeed.enabled;
    }

    [Serializable]
    public class BulletBurstV2
    {
        public string name = "Burst";

        [Min(1)]
        public int repeatCount = 1;

        [Min(0f)]
        public float repeatInterval = 0.1f;

        [Min(0f)]
        public float delayAfter = 0.5f;

        [Tooltip("กระสุนที่จะยิงใน Burst นี้")]
        public GameObject bulletPrefab;

        [Min(1)]
        public int bulletCount = 1;

        public BulletSpawnSettings spawn = new BulletSpawnSettings();
        public BulletDirectionSettings direction = new BulletDirectionSettings();
        public BulletMotionSettings motion = new BulletMotionSettings();
        public BulletPatternModifiers modifiers = new BulletPatternModifiers();

        [NonSerialized] public int currentRepeat;
        [NonSerialized] public float repeatTimer;
        [NonSerialized] public float delayTimer;
        [NonSerialized] public bool fired;

        public void ResetRuntime()
        {
            currentRepeat = 0;
            repeatTimer = 0f;
            delayTimer = 0f;
            fired = false;
        }
    }

    [Serializable]
    public class BulletPatternGroup
    {
        public string groupName = "New Pattern";

        [Header("Timing")]
        [Tooltip("เวลารอก่อน Pattern นี้เริ่มทำงานครั้งแรก")]
        [Min(0f)]
        public float initialDelay = 0f;

        [Tooltip("แต่ละ Group มี timeline แยกจาก Group อื่น")]
        public List<BulletBurstV2> bursts = new List<BulletBurstV2>();

        public bool loopPattern = true;

        [NonSerialized] public int currentBurst;
        [NonSerialized] public bool finished;
        [NonSerialized] public float initialDelayTimer;

        public void ResetRuntime()
        {
            currentBurst = 0;
            finished = false;
            initialDelayTimer = initialDelay;

            if (bursts == null) return;
            for (int i = 0; i < bursts.Count; i++)
                bursts[i].ResetRuntime();
        }
    }

    // ============================================================
    // V2 RUNTIME
    // ============================================================

    public sealed class BulletPatternV2Executor
    {
        private readonly List<BulletPatternGroup> groups;
        private readonly Transform owner;
        private readonly Func<Transform> playerProvider;

        private System.Random random;

        // Same modifier object = same random value for PerPattern.
        private readonly Dictionary<object, float> perPatternRandomValues =
            new Dictionary<object, float>();

        public BulletPatternV2Executor(
            List<BulletPatternGroup> groups,
            Transform owner,
            Func<Transform> playerProvider)
        {
            this.groups = groups;
            this.owner = owner;
            this.playerProvider = playerProvider;
            ResetAllGroups();
        }

        public void ResetAllGroups()
        {
            if (groups == null) return;

            for (int i = 0; i < groups.Count; i++)
                groups[i]?.ResetRuntime();

            ResetRandom();
        }

        public void ResetRandom()
        {
            random = new System.Random(Environment.TickCount);
            perPatternRandomValues.Clear();
        }

        public void SetSeed(int seed)
        {
            random = new System.Random(seed);
            perPatternRandomValues.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (groups == null || owner == null || PoolManager.Instance == null)
                return;

            Transform player = playerProvider != null ? playerProvider() : null;

            for (int i = 0; i < groups.Count; i++)
                TickGroup(groups[i], deltaTime, player);
        }

        private void TickGroup(BulletPatternGroup group, float dt, Transform player)
        {
            if (group == null || group.finished || group.bursts == null || group.bursts.Count == 0)
                return;

            if (group.initialDelayTimer > 0f)
            {
                group.initialDelayTimer -= dt;
                if (group.initialDelayTimer > 0f)
                    return;
            }
            
            if (group.currentBurst < 0 || group.currentBurst >= group.bursts.Count)
            {
                group.finished = true;
                return;
            }

            BulletBurstV2 burst = group.bursts[group.currentBurst];
            if (burst == null)
            {
                AdvanceBurst(group);
                return;
            }

            int repeatCount = Mathf.Max(1, burst.repeatCount);

            if (!burst.fired)
            {
                FireBurst(burst, 0, player);
                burst.fired = true;
                burst.currentRepeat = 0;
                burst.repeatTimer = 0f;
                burst.delayTimer = 0f;
                return;
            }

            if (burst.currentRepeat < repeatCount - 1)
            {
                float interval = Mathf.Max(0f, burst.repeatInterval);

                if (interval <= 0f)
                {
                    burst.currentRepeat++;
                    FireBurst(burst, burst.currentRepeat, player);
                    return;
                }

                burst.repeatTimer += dt;

                while (burst.repeatTimer >= interval &&
                       burst.currentRepeat < repeatCount - 1)
                {
                    burst.repeatTimer -= interval;
                    burst.currentRepeat++;
                    FireBurst(burst, burst.currentRepeat, player);
                }

                return;
            }

            burst.delayTimer += dt;
            if (burst.delayTimer >= Mathf.Max(0f, burst.delayAfter))
                AdvanceBurst(group);
        }

        private void AdvanceBurst(BulletPatternGroup group)
        {
            group.currentBurst++;

            if (group.currentBurst >= group.bursts.Count)
            {
                if (group.loopPattern)
                    group.currentBurst = 0;
                else
                {
                    group.currentBurst = group.bursts.Count - 1;
                    group.finished = true;
                }
            }

            if (!group.finished &&
                group.currentBurst >= 0 &&
                group.currentBurst < group.bursts.Count)
            {
                group.bursts[group.currentBurst].ResetRuntime();
            }
        }

        private void FireBurst(BulletBurstV2 burst, int repeatIndex, Transform player)
        {
            if (burst.bulletPrefab == null)
            {
                Debug.LogWarning(
                    $"BulletPatternV2: Burst '{burst.name}' ไม่มี Bullet Prefab - ข้ามการยิง");
                return;
            }

            int count = Mathf.Max(1, burst.bulletCount);
            Vector3 baseSpawn = CalculateBaseSpawnPosition(burst.spawn);

            float burstRandomAngle = 0f;
            float burstRandomSpeed = 0f;

            if (burst.modifiers != null)
            {
                if (burst.modifiers.randomAngle != null &&
                    burst.modifiers.randomAngle.enabled)
                {
                    RandomAngleModifier modifier = burst.modifiers.randomAngle;

                    burstRandomAngle =
                        modifier.scope == RandomScope.PerPattern
                            ? GetPerPatternRandom(modifier, modifier.range)
                            : modifier.scope == RandomScope.PerBurst
                                ? RandomRange(modifier.range)
                                : 0f;
                }

                if (burst.modifiers.randomSpeed != null &&
                    burst.modifiers.randomSpeed.enabled)
                {
                    RandomSpeedModifier modifier = burst.modifiers.randomSpeed;

                    burstRandomSpeed =
                        modifier.scope == RandomScope.PerPattern
                            ? GetPerPatternRandom(modifier, modifier.range)
                            : modifier.scope == RandomScope.PerBurst
                                ? RandomRange(modifier.range)
                                : 0f;
                }
            }

            Vector2 centerDirection = CalculateCenterDirection(
                burst.direction,
                baseSpawn,
                player,
                repeatIndex);

            for (int i = 0; i < count; i++)
            {
                float angle = CalculateSpreadOffset(i, count, burst.direction.spread);

                if (burst.modifiers != null &&
                    burst.modifiers.randomAngle != null &&
                    burst.modifiers.randomAngle.enabled &&
                    burst.modifiers.randomAngle.scope == RandomScope.PerShot)
                {
                    angle += RandomRange(burst.modifiers.randomAngle.range);
                }

                angle += burstRandomAngle;

                Vector2 direction = Rotate(centerDirection, angle);

                Vector3 spawnPosition = baseSpawn;

                if (burst.spawn.mode == BulletSpawnMode.Circle)
                {
                    float circleAngle = count == 1 ? 0f : 360f * i / count;
                    Vector2 offset =
                        Rotate(Vector2.right, circleAngle) *
                        burst.spawn.circleRadius;

                    spawnPosition += (Vector3)offset;
                }

                GameObject bulletObject =
                    PoolManager.Instance.Get(
                        burst.bulletPrefab,
                        spawnPosition,
                        Quaternion.identity);

                if (bulletObject == null)
                    continue;

                EnemyBulletV2 bullet =
                    bulletObject.GetComponent<EnemyBulletV2>();

                if (bullet == null)
                {
                    Debug.LogError(
                        $"BulletPatternV2: '{burst.bulletPrefab.name}' ไม่มี EnemyBulletV2",
                        burst.bulletPrefab);

                    PoolManager.Instance.Return(bulletObject);
                    continue;
                }

                float speedOffset = burstRandomSpeed;

                if (burst.modifiers != null &&
                    burst.modifiers.randomSpeed != null &&
                    burst.modifiers.randomSpeed.enabled &&
                    burst.modifiers.randomSpeed.scope == RandomScope.PerShot)
                {
                    speedOffset +=
                        RandomRange(burst.modifiers.randomSpeed.range);
                }

                bool usePatternSpeed = burst.motion.overrideInitialSpeed;
                float initialSpeed = burst.motion.initialSpeed;

                // Random Speed must also work when the Pattern itself does not
                // override the initial speed: use the bullet prefab's default.
                if (Mathf.Abs(speedOffset) > 0.000001f)
                {
                    initialSpeed =
                        (usePatternSpeed
                            ? initialSpeed
                            : bullet.DefaultSpeed) +
                        speedOffset;

                    usePatternSpeed = true;
                }

                bullet.SetDirection(direction);

                bullet.ConfigureMotion(
                    usePatternSpeed,
                    initialSpeed,
                    burst.motion.speedTimeline,
                    burst.motion.directionTimeline);
            }
        }

        private Vector3 CalculateBaseSpawnPosition(BulletSpawnSettings spawn)
        {
            switch (spawn.mode)
            {
                case BulletSpawnMode.Offset:
                    return owner.position + (Vector3)spawn.offset;

                default:
                    return owner.position;
            }
        }

        private Vector2 CalculateCenterDirection(
            BulletDirectionSettings settings,
            Vector3 spawnPosition,
            Transform player,
            int repeatIndex)
        {
            Vector2 result;

            switch (settings.mode)
            {
                case BulletDirectionMode.AimAtPlayer:
                case BulletDirectionMode.TowardPlayer:
                    result = DirectionToPlayer(spawnPosition, player);
                    break;

                case BulletDirectionMode.AwayFromPlayer:
                    result = -DirectionToPlayer(spawnPosition, player);
                    break;

                case BulletDirectionMode.Random:
                    result = Rotate(Vector2.right, RandomRange(360f));
                    break;

                case BulletDirectionMode.Rotating:
                    result = AngleToVector(
                        settings.angle +
                        settings.rotationPerRepeat * repeatIndex);
                    break;

                case BulletDirectionMode.RelativeToOwner:
                case BulletDirectionMode.FixedAngle:
                default:
                    result = AngleToVector(settings.angle);
                    break;
            }

            if (result.sqrMagnitude < 0.000001f)
                result = Vector2.down;

            return result.normalized;
        }

        private Vector2 DirectionToPlayer(
            Vector3 spawnPosition,
            Transform player)
        {
            if (player == null)
                return Vector2.down;

            Vector2 result =
                (Vector2)player.position -
                (Vector2)spawnPosition;

            if (result.sqrMagnitude < 0.000001f)
                return Vector2.down;

            return result.normalized;
        }

        private float CalculateSpreadOffset(
            int index,
            int count,
            float spread)
        {
            if (count <= 1 || Mathf.Abs(spread) <= 0.0001f)
                return 0f;

            if (Mathf.Abs(Mathf.Abs(spread) - 360f) < 0.0001f)
                return (spread / count) * index;

            float step = spread / (count - 1);
            return -spread * 0.5f + step * index;
        }

        private float GetPerPatternRandom(object key, float range)
        {
            if (perPatternRandomValues.TryGetValue(key, out float value))
                return value;

            value = RandomRange(range);
            perPatternRandomValues[key] = value;
            return value;
        }

        private float RandomRange(float range)
        {
            if (random == null)
                ResetRandom();

            return (float)(random.NextDouble() * 2.0 - 1.0) *
                   Mathf.Abs(range);
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }
    }

    // ============================================================
    // LEGACY COMPATIBILITY DATA
    // BossV1 can keep using these types.
    // EnemyBulletPatternV2 does NOT use them.
    // ============================================================

    [Serializable]
    public class ShootElement
    {
        public GameObject bulletPrefab;
        public int bulletCount = 1;
        public float spreadAngle = 0f;
        public bool aimAtPlayer = false;
        public Vector2 baseDirection = Vector2.down;
        public Vector2 firePointOffset = Vector2.zero;

        public bool overrideSpeed = false;
        public float speed = 6f;

        public bool overrideAcceleration = false;
        public float acceleration = 0f;
        public float minSpeed = 0f;
        public float maxSpeed = 50f;
    }

    [Serializable]
    public class ShootBurst
    {
        public List<ShootElement> elements =
            new List<ShootElement>();

        public float delayAfter = 0.5f;
        public int repeatCount = 1;
        public float repeatInterval = 0.1f;
        public float angleOffsetPerRepeat = 0f;
    }

    [Serializable]
    public class ShootGroup
    {
        public string groupName = "New Group";
        public List<ShootBurst> pattern =
            new List<ShootBurst>();

        public bool loopPattern = true;

        [NonSerialized] public int currentBurstIndex;
        [NonSerialized] public float delayTimer;
        [NonSerialized] public bool hasFiredCurrentBurst;
        [NonSerialized] public bool patternFinished;
        [NonSerialized] public int currentRepeatIndex;
        [NonSerialized] public float repeatTimer;

        public void ResetState()
        {
            currentBurstIndex = 0;
            delayTimer = 0f;
            hasFiredCurrentBurst = false;
            patternFinished = false;
            currentRepeatIndex = 0;
            repeatTimer = 0f;
        }
    }

    /// <summary>
    /// Compatibility executor for BossV1.
    /// The old data API stays available, but bullets are now EnemyBulletV2.
    /// </summary>
    public class ShootPatternExecutor
    {
        private readonly List<ShootGroup> groups;

        public ShootPatternExecutor(List<ShootGroup> groups)
        {
            this.groups = groups;
        }

        public void Tick(
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            if (groups == null) return;

            for (int g = 0; g < groups.Count; g++)
                UpdateGroup(
                    groups[g],
                    ownerPosition,
                    playerTransform);
        }

        public void ResetAllGroups()
        {
            if (groups == null) return;

            for (int i = 0; i < groups.Count; i++)
                groups[i]?.ResetState();
        }

        private void UpdateGroup(
            ShootGroup group,
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            if (group == null ||
                group.pattern == null ||
                group.pattern.Count == 0 ||
                group.patternFinished)
                return;

            ShootBurst burst =
                group.pattern[group.currentBurstIndex];

            int totalRepeats =
                Mathf.Max(burst.repeatCount, 1);

            if (!group.hasFiredCurrentBurst)
            {
                FireBurst(
                    burst,
                    0,
                    ownerPosition,
                    playerTransform);

                group.hasFiredCurrentBurst = true;
                group.currentRepeatIndex = 0;
                group.repeatTimer = 0f;
                group.delayTimer = 0f;
                return;
            }

            if (group.currentRepeatIndex < totalRepeats - 1)
            {
                float interval =
                    Mathf.Max(0f, burst.repeatInterval);

                if (interval <= 0f)
                {
                    group.currentRepeatIndex++;

                    FireBurst(
                        burst,
                        group.currentRepeatIndex,
                        ownerPosition,
                        playerTransform);

                    return;
                }

                group.repeatTimer += Time.deltaTime;

                while (group.repeatTimer >= interval &&
                       group.currentRepeatIndex < totalRepeats - 1)
                {
                    group.repeatTimer -= interval;
                    group.currentRepeatIndex++;

                    FireBurst(
                        burst,
                        group.currentRepeatIndex,
                        ownerPosition,
                        playerTransform);
                }

                return;
            }

            group.delayTimer += Time.deltaTime;

            if (group.delayTimer >=
                Mathf.Max(0f, burst.delayAfter))
            {
                group.currentBurstIndex++;
                group.hasFiredCurrentBurst = false;
                group.currentRepeatIndex = 0;
                group.repeatTimer = 0f;

                if (group.currentBurstIndex >=
                    group.pattern.Count)
                {
                    if (group.loopPattern)
                        group.currentBurstIndex = 0;
                    else
                        group.patternFinished = true;
                }
            }
        }

        private void FireBurst(
            ShootBurst burst,
            int repeatIndex,
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            for (int i = 0;
                 i < burst.elements.Count;
                 i++)
            {
                ShootElement element =
                    burst.elements[i];

                if (element == null ||
                    element.bulletPrefab == null)
                    continue;

                Vector3 firePosition =
                    ownerPosition +
                    (Vector3)element.firePointOffset;

                Vector2 center =
                    element.aimAtPlayer
                        ? GetDirectionToPlayer(
                            firePosition,
                            playerTransform)
                        : SafeDirection(
                            element.baseDirection);

                center =
                    Rotate(
                        center,
                        burst.angleOffsetPerRepeat *
                        repeatIndex);

                int count =
                    Mathf.Max(1, element.bulletCount);

                for (int b = 0; b < count; b++)
                {
                    float offset =
                        CalculateAngleOffset(
                            b,
                            count,
                            element.spreadAngle);

                    Vector2 direction =
                        Rotate(center, offset);

                    GameObject obj =
                        PoolManager.Instance.Get(
                            element.bulletPrefab,
                            firePosition,
                            Quaternion.identity);

                    if (obj == null)
                        continue;

                    EnemyBulletV2 bullet =
                        obj.GetComponent<EnemyBulletV2>();

                    if (bullet == null)
                    {
                        PoolManager.Instance.Return(obj);
                        continue;
                    }

                    bullet.SetDirection(direction);

                    if (element.overrideSpeed)
                        bullet.SetSpeed(element.speed);

                    if (element.overrideAcceleration)
                    {
                        bullet.SetAcceleration(
                            element.acceleration,
                            element.minSpeed,
                            element.maxSpeed);
                    }
                }
            }
        }

        private Vector2 GetDirectionToPlayer(
            Vector3 firePosition,
            Transform playerTransform)
        {
            if (playerTransform == null)
                return Vector2.down;

            Vector2 result =
                (Vector2)playerTransform.position -
                (Vector2)firePosition;

            return result.sqrMagnitude < 0.000001f
                ? Vector2.down
                : result.normalized;
        }

        private static Vector2 SafeDirection(
            Vector2 direction)
        {
            return direction.sqrMagnitude < 0.000001f
                ? Vector2.down
                : direction.normalized;
        }

        private static float CalculateAngleOffset(
            int index,
            int count,
            float spreadAngle)
        {
            if (count <= 1)
                return 0f;

            bool fullCircle =
                Mathf.Abs(
                    Mathf.Abs(spreadAngle) - 360f) <
                0.01f;

            if (fullCircle)
                return spreadAngle / count * index;

            float step =
                spreadAngle / (count - 1);

            return -spreadAngle / 2f +
                   step * index;
        }

        private static Vector2 Rotate(
            Vector2 value,
            float degrees)
        {
            float radians =
                degrees * Mathf.Deg2Rad;

            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }
    }
}
