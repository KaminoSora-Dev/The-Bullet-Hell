using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace EnemySystem
{
    /// <summary>
    /// Enemy bullet V2.
    ///
    /// Base Speed is the prefab default.
    /// Pattern can override the initial speed and can add a Speed Timeline.
    /// Speed Timeline / Target Speed รองรับค่าติดลบ - กระสุนจะเคลื่อนที่ถอยหลัง (สวนทาง Direction ที่ตั้งไว้)
    /// Timeline starts at 0 immediately when the bullet is spawned from the pool.
    ///
    /// Direction Timeline ทำงานคู่ขนานกับ Speed Timeline (คนละแกนกัน) - หมุนทิศทางกระสุนตามเวลา
    /// รองรับทั้งหมุนแบบ Relative Angle (รวม spiral เกิน 180 องศา) และ Aim At Player (เล็งผู้เล่นใหม่ ณ ตอนเริ่ม Redirect นั้นๆ)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyBulletV2 : MonoBehaviour, IPoolable
    {
        [Header("Default Motion")]
        [Tooltip("ความเร็วเริ่มต้นของกระสุน ถ้า Pattern ไม่ Override")]
        [SerializeField, Min(0f)] private float baseSpeed = 6f;

        // Kept only so old code that still calls SetAcceleration can compile.
        // Hidden because V2's normal workflow uses Speed Timeline.
        [SerializeField, HideInInspector] private float baseAcceleration = 0f;
        [SerializeField, HideInInspector] private float baseMinSpeed = 0f;
        [SerializeField, HideInInspector] private float baseMaxSpeed = 50f;

        private Rigidbody2D rb;
        private Vector2 direction = Vector2.down;

        private float currentSpeed;
        private float currentAcceleration;
        private float currentMinSpeed;
        private float currentMaxSpeed;

        private List<SpeedTransition> speedTimeline;
        private int speedTransitionIndex;
        private float speedTransitionTimer;
        private float segmentStartSpeed;
        private bool speedTimelineActive;
        private bool useSpeedTimeline;

        // ---- Direction Timeline state ----
        private List<DirectionTransition> directionTimeline;
        private int directionTransitionIndex;
        private float directionTransitionTimer;
        private float directionSegmentStartAngle;
        private float directionSegmentTargetAngle;
        private bool directionSegmentInitialized;
        private bool directionTimelineActive;
        private bool useDirectionTimeline;

        // ใช้เฉพาะตอน Direction Redirect เป็น Mode "Aim At Player" - cache ไว้ครั้งเดียวตอน Awake (bullet ถูกใช้ซ้ำจาก pool)
        private Transform cachedPlayerTransform;

        public float CurrentSpeed => currentSpeed;
        public Vector2 Direction => direction;
        public float DefaultSpeed => baseSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            CircleCollider2D collider =
                GetComponent<CircleCollider2D>();

            if (collider != null)
                collider.isTrigger = true;

            ResetMotionToPrefabDefaults();
            CachePlayer();
        }

        private void FixedUpdate()
        {
            UpdateSpeed();
            UpdateDirection();

            Vector2 targetPosition =
                rb.position +
                direction *
                currentSpeed *
                Time.fixedDeltaTime;

            rb.MovePosition(targetPosition);
        }

        private void UpdateSpeed()
        {
            if (useSpeedTimeline)
            {
                UpdateSpeedTimeline(Time.fixedDeltaTime);
                return;
            }

            // Legacy behavior only.
            if (Mathf.Abs(currentAcceleration) > 0.000001f)
            {
                currentSpeed +=
                    currentAcceleration *
                    Time.fixedDeltaTime;

                currentSpeed =
                    Mathf.Clamp(
                        currentSpeed,
                        currentMinSpeed,
                        currentMaxSpeed);
            }
        }

        private void UpdateDirection()
        {
            if (useDirectionTimeline)
                UpdateDirectionTimeline(Time.fixedDeltaTime);
        }

        private void CachePlayer()
        {
            if (cachedPlayerTransform != null)
                return;

            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                cachedPlayerTransform = playerObject.transform;
        }

        public void SetDirection(Vector2 newDirection)
        {
            if (newDirection.sqrMagnitude < 0.000001f)
            {
                direction = Vector2.down;
                return;
            }

            direction = newDirection.normalized;
        }

        public void SetSpeed(float newSpeed)
        {
            currentSpeed = newSpeed;

            useSpeedTimeline = false;
            speedTimeline = null;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            speedTimelineActive = false;
            segmentStartSpeed = currentSpeed;
        }

        /// <summary>
        /// Legacy compatibility for old Boss/ShootPatternCore data.
        /// New V2 patterns should use Speed Timeline.
        /// </summary>
        public void SetAcceleration(
            float newAcceleration,
            float newMinSpeed,
            float newMaxSpeed)
        {
            currentAcceleration = newAcceleration;
            currentMinSpeed =
                Mathf.Max(0f, newMinSpeed);

            currentMaxSpeed =
                Mathf.Max(
                    currentMinSpeed,
                    newMaxSpeed);

            useSpeedTimeline = false;
            speedTimeline = null;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            speedTimelineActive = false;
        }

        /// <summary>
        /// Called immediately after PoolManager.Get().
        /// This is the zero-time origin of the Speed Timeline / Direction Timeline.
        /// directionTimeline เป็น optional (default null) เพื่อไม่ให้ caller เดิมที่เรียกแค่ 3 argument พัง
        /// </summary>
        public void ConfigureMotion(
            bool overrideInitialSpeed,
            float initialSpeed,
            List<SpeedTransition> timeline,
            List<DirectionTransition> newDirectionTimeline = null)
        {
            // ไม่ Clamp ค่าติดลบอีกต่อไป - Initial Speed ติดลบได้ กระสุนจะเริ่มเคลื่อนที่ถอยหลังตั้งแต่ spawn
            currentSpeed =
                overrideInitialSpeed
                    ? initialSpeed
                    : baseSpeed;

            // New Timeline owns speed.
            currentAcceleration = 0f;
            currentMinSpeed = 0f;
            currentMaxSpeed = float.MaxValue;

            speedTimeline = timeline;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            segmentStartSpeed = currentSpeed;

            speedTimelineActive =
                speedTimeline != null &&
                speedTimeline.Count > 0;

            useSpeedTimeline = speedTimelineActive;

            // ---- Direction Timeline ----
            directionTimeline = newDirectionTimeline;
            directionTransitionIndex = 0;
            directionTransitionTimer = 0f;
            directionSegmentInitialized = false;

            directionTimelineActive =
                directionTimeline != null &&
                directionTimeline.Count > 0;

            useDirectionTimeline = directionTimelineActive;
        }

        private void UpdateSpeedTimeline(
            float deltaTime)
        {
            if (!speedTimelineActive ||
                speedTimeline == null ||
                speedTimeline.Count == 0)
            {
                speedTimelineActive = false;
                useSpeedTimeline = false;
                return;
            }

            int safety = 0;

            while (speedTransitionIndex <
                       speedTimeline.Count &&
                   safety++ <
                       speedTimeline.Count + 2)
            {
                SpeedTransition transition =
                    speedTimeline[
                        speedTransitionIndex];

                if (transition == null)
                {
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed =
                        currentSpeed;

                    continue;
                }

                float duration =
                    Mathf.Max(
                        0f,
                        transition.duration);

                if (duration <= 0f)
                {
                    // ไม่ Clamp 0f อีกต่อไป - Target Speed ติดลบได้ตามที่ตั้งไว้
                    currentSpeed = transition.targetSpeed;

                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed =
                        currentSpeed;

                    continue;
                }

                speedTransitionTimer += deltaTime;

                float t =
                    Mathf.Clamp01(
                        speedTransitionTimer /
                        duration);

                float eased =
                    ApplyEase(
                        t,
                        transition.easing);

                currentSpeed =
                    Mathf.Lerp(
                        segmentStartSpeed,
                        transition.targetSpeed,
                        eased);

                if (speedTransitionTimer >=
                    duration)
                {
                    currentSpeed = transition.targetSpeed;

                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed =
                        currentSpeed;

                    continue;
                }

                break;
            }

            if (speedTransitionIndex >=
                speedTimeline.Count)
            {
                speedTimelineActive = false;
                useSpeedTimeline = false;
                speedTimeline = null;
            }
        }

        /// <summary>
        /// Direction Timeline - หลักการเหมือน Speed Timeline ทุกประการ แค่หมุนมุม (องศา) แทนการไล่ความเร็ว
        /// targetAngle ของแต่ละ segment คำนวณ "ครั้งเดียว" ตอนเพิ่งเข้า segment นั้น (segmentStart) แล้ว Lerp ไปหา
        /// ไม่ track ตำแหน่งผู้เล่นต่อเนื่องระหว่างทาง (ถ้าอยาก track ต่อเนื่องคือ Homing ซึ่งเป็นฟีเจอร์คนละแบบ)
        /// </summary>
        private void UpdateDirectionTimeline(
            float deltaTime)
        {
            if (!directionTimelineActive ||
                directionTimeline == null ||
                directionTimeline.Count == 0)
            {
                directionTimelineActive = false;
                useDirectionTimeline = false;
                return;
            }

            int safety = 0;

            while (directionTransitionIndex <
                       directionTimeline.Count &&
                   safety++ <
                       directionTimeline.Count + 2)
            {
                DirectionTransition transition =
                    directionTimeline[
                        directionTransitionIndex];

                if (transition == null)
                {
                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                if (!directionSegmentInitialized)
                {
                    directionSegmentStartAngle =
                        VectorToAngle(direction);

                    directionSegmentTargetAngle =
                        CalculateTargetAngle(
                            transition,
                            directionSegmentStartAngle);

                    directionSegmentInitialized = true;
                }

                float duration =
                    Mathf.Max(
                        0f,
                        transition.duration);

                if (duration <= 0f)
                {
                    direction =
                        AngleToVector(
                            directionSegmentTargetAngle);

                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                directionTransitionTimer += deltaTime;

                float t =
                    Mathf.Clamp01(
                        directionTransitionTimer /
                        duration);

                float eased =
                    ApplyEase(
                        t,
                        transition.easing);

                float currentAngle =
                    Mathf.Lerp(
                        directionSegmentStartAngle,
                        directionSegmentTargetAngle,
                        eased);

                direction = AngleToVector(currentAngle);

                if (directionTransitionTimer >=
                    duration)
                {
                    direction =
                        AngleToVector(
                            directionSegmentTargetAngle);

                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                break;
            }

            if (directionTransitionIndex >=
                directionTimeline.Count)
            {
                directionTimelineActive = false;
                useDirectionTimeline = false;
                directionTimeline = null;
            }
        }

        private float CalculateTargetAngle(
            DirectionTransition transition,
            float startAngle)
        {
            if (transition.mode ==
                DirectionRedirectMode.AimAtPlayer)
            {
                float aimAngle =
                    VectorToAngle(
                        DirectionToPlayer());

                // เอาเส้นทางหมุนที่สั้นที่สุดไปหาเป้าหมาย (ไม่ใช่หมุนอ้อมโลก)
                float delta =
                    Mathf.DeltaAngle(
                        startAngle,
                        aimAngle);

                return startAngle + delta;
            }

            // Relative Angle - ไม่ wrap มุม เพื่อรองรับหมุนเกิน 180 องศา (ลาย spiral)
            return startAngle + transition.relativeAngle;
        }

        private Vector2 DirectionToPlayer()
        {
            if (cachedPlayerTransform == null)
                return direction; // ไม่มี Player ให้เล็ง - คงทิศเดิมไว้ ไม่หมุน

            Vector2 result =
                (Vector2)cachedPlayerTransform.position -
                rb.position;

            return result.sqrMagnitude < 0.000001f
                ? direction
                : result.normalized;
        }

        private static float VectorToAngle(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));
        }

        private static float ApplyEase(
            float t,
            SpeedEasing easing)
        {
            t = Mathf.Clamp01(t);

            switch (easing)
            {
                case SpeedEasing.EaseIn:
                    return t * t;

                case SpeedEasing.EaseOut:
                    return 1f -
                           (1f - t) *
                           (1f - t);

                case SpeedEasing.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f -
                          Mathf.Pow(
                              -2f * t + 2f,
                              2f) /
                          2f;

                default:
                    return t;
            }
        }

        private void ResetMotionToPrefabDefaults()
        {
            direction = Vector2.down;

            currentSpeed = baseSpeed;
            currentAcceleration =
                baseAcceleration;

            currentMinSpeed =
                Mathf.Max(
                    0f,
                    baseMinSpeed);

            currentMaxSpeed =
                Mathf.Max(
                    currentMinSpeed,
                    baseMaxSpeed);

            speedTimeline = null;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            segmentStartSpeed = currentSpeed;
            speedTimelineActive = false;
            useSpeedTimeline = false;

            directionTimeline = null;
            directionTransitionIndex = 0;
            directionTransitionTimer = 0f;
            directionSegmentInitialized = false;
            directionTimelineActive = false;
            useDirectionTimeline = false;
        }

        public void OnSpawnFromPool()
        {
            ResetMotionToPrefabDefaults();

            // เผื่อกรณี Player spawn หลัง Bullet ตัวแรกๆ ที่ถูก prewarm ไว้ตอนเริ่มฉาก
            CachePlayer();
        }

        public void OnReturnToPool()
        {
            speedTimeline = null;
            speedTimelineActive = false;
            useSpeedTimeline = false;

            directionTimeline = null;
            directionTimelineActive = false;
            useDirectionTimeline = false;
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (other.CompareTag("HiddenBorder"))
            {
                if (PoolManager.Instance != null)
                    PoolManager.Instance.Return(
                        gameObject);

                return;
            }

            if (!other.CompareTag("Player"))
                return;

            if (other.TryGetComponent<PlayerControllerV1>(
                out var player))
            {
                player.Die();
            }

            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(
                    gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
