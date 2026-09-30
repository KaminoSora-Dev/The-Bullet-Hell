using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace EnemySystem
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyBulletV2 : MonoBehaviour, IPoolable
    {
        [Header("Default Motion")]
        [SerializeField, Min(0f)] private float baseSpeed = 6f;

        [SerializeField, HideInInspector] private float baseAcceleration = 0f;
        [SerializeField, HideInInspector] private float baseMinSpeed = 0f;
        [SerializeField, HideInInspector] private float baseMaxSpeed = 50f;

        [Header("Death Animation (Optional)")]
        [Tooltip(
            "ถ้าลากมาใส่ - ตอนกระสุนถูกทำลาย (ไม่นับ HiddenBorder) จะเล่นแอนิเมชันนี้ก่อนค่อยกลับ Pool จริง " +
            "ถ้าไม่ใส่ไว้ กระสุนจะกลับ Pool ทันทีเหมือนพฤติกรรมเดิมทุกประการ"
        )]
        [SerializeField] private Animator deathAnimator;

        [Tooltip("ชื่อ Trigger parameter บน Animator ที่จะสั่งตอนกระสุนถูกทำลาย")]
        [SerializeField] private string deathTrigger = "Death";

        [Header("Spawn SFX (Optional)")]
        [Tooltip(
            "AudioSource สำหรับเสียงตอนกระสุนนี้ Spawn " +
            "สามารถกำหนด AudioClip และ AudioMixerGroup ของกระสุนแต่ละชนิดแยกกันได้"
        )]
        [SerializeField] private AudioSource spawnSfx;

        [Header("Visual Rotation (Optional)")]
        [Tooltip(
            "ติ๊กถ้าอยากให้กระสุนหมุนหันไปตามทิศทางที่กำลังเคลื่อนที่อยู่ " +
            "(อัปเดตทุกครั้งที่ทิศเปลี่ยน เช่นจาก Direction Timeline, Redirect, หรือ Path Motion)\n" +
            "ไม่ติ๊ก = กระสุนไม่หมุน"
        )]
        [SerializeField] private bool rotateToFaceDirection = false;

        [Tooltip(
            "มุมชดเชย Sprite - ถ้า Sprite วาดหันขึ้นด้านบนอยู่แล้วให้ใส่ -90, " +
            "ถ้าวาดหันขวาอยู่แล้วใส่ 0"
        )]
        [SerializeField] private float spriteAngleOffset = -90f;

        // เปลี่ยนจาก CircleCollider2D -> Collider2D (base class)
        // เพื่อรองรับกระสุนหลายทรง (Box, Polygon, Capsule, ฯลฯ)
        // โค้ดในคลาสนี้ใช้แค่ .isTrigger กับ .enabled ซึ่งเป็นของ Collider2D
        // อยู่แล้ว ไม่จำเป็นต้องผูกกับ CircleCollider2D โดยเฉพาะ
        private Collider2D col;
        private Rigidbody2D rb;

        private bool isDying;

        // สำคัญสำหรับ Object Pool:
        // RequestDestroy() อาจ Return เข้า Pool ระหว่าง UpdateBurstTimeline()
        // ดังนั้นต้องรู้ว่า object ถูกคืน Pool ไปแล้ว แม้ isDying จะถูก reset
        // กลับเป็น false ใน OnReturnToPool()
        private bool returnedToPool;

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

        private List<DirectionTransition> directionTimeline;
        private int directionTransitionIndex;
        private float directionTransitionTimer;
        private float directionSegmentStartAngle;
        private float directionSegmentTargetAngle;
        private bool directionSegmentInitialized;
        private bool directionTimelineActive;
        private bool useDirectionTimeline;

        private List<BulletBurstTrigger> burstTimeline;
        private IEnumerable<BulletCustomPattern> patternCallChain;
        private bool[] burstTriggerFired;
        private float burstTimelineClock;

        private readonly List<BulletPatternV2Executor> activeBurstExecutors =
            new List<BulletPatternV2Executor>();

        // ============================================================
        // PATH MOTION
        // ============================================================
        private bool usingPathMotion;
        private Vector3 pathStart;
        private Vector3 pathEnd;
        private Vector3 pathControl;
        private bool pathHasCurve;
        private float pathDuration;
        private SpeedEasing pathEasing;
        private PathEndBehavior pathEndBehavior;
        private float pathTimer;

        private Transform cachedPlayerTransform;

        public float CurrentSpeed => currentSpeed;
        public Vector2 Direction => direction;
        public float DefaultSpeed => baseSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            // ดึง Collider2D แบบไหนก็ได้ที่ติดอยู่บน GameObject นี้
            // (Circle, Box, Polygon, Capsule ฯลฯ) แทนการบังคับ Circle อย่างเดียว
            col = GetComponent<Collider2D>();

            if (col != null)
                col.isTrigger = true;
            else
                Debug.LogWarning(
                    $"EnemyBulletV2 บน '{gameObject.name}' ไม่มี Collider2D ติดอยู่ - กระสุนจะไม่ชนอะไรเลย",
                    gameObject);

            ResetMotionToPrefabDefaults();
            CachePlayer();
        }

        private void FixedUpdate()
        {
            if (isDying || returnedToPool)
                return;

            if (usingPathMotion)
            {
                UpdatePathMotion(Time.fixedDeltaTime);
                return;
            }

            UpdateSpeed();
            UpdateDirection();

            Vector2 targetPosition =
                rb.position +
                direction *
                currentSpeed *
                Time.fixedDeltaTime;

            rb.MovePosition(targetPosition);

            if (rotateToFaceDirection)
                UpdateFacingRotation();
        }

        private void Update()
        {
            if (isDying || returnedToPool)
                return;

            UpdateBurstTimeline(Time.deltaTime);

            if (isDying || returnedToPool)
                return;

            TickActiveBurstExecutors(Time.deltaTime);
        }

        // ============================================================
        // PATH MOTION UPDATE
        // ============================================================

        private void UpdatePathMotion(float dt)
        {
            pathTimer += dt;

            float t = Mathf.Clamp01(pathTimer / Mathf.Max(0.0001f, pathDuration));
            float eased = ApplyEase(t, pathEasing);

            Vector3 newPos;
            Vector3 tangent;

            if (pathHasCurve)
            {
                newPos = QuadraticBezierPoint(pathStart, pathControl, pathEnd, eased);
                tangent = QuadraticBezierTangent(pathStart, pathControl, pathEnd, eased);
            }
            else
            {
                newPos = Vector3.LerpUnclamped(pathStart, pathEnd, eased);
                tangent = pathEnd - pathStart;
            }

            rb.MovePosition(newPos);

            if (tangent.sqrMagnitude > 0.000001f)
                direction = ((Vector2)tangent).normalized;

            if (rotateToFaceDirection)
                UpdateFacingRotation();

            if (t >= 1f)
            {
                usingPathMotion = false;

                switch (pathEndBehavior)
                {
                    case PathEndBehavior.Destroy:
                        RequestDestroy();
                        break;

                    case PathEndBehavior.Stop:
                        currentSpeed = 0f;
                        useSpeedTimeline = false;
                        break;

                    // ContinueFlying: currentSpeed/direction ที่ตั้งไว้ตอน ConfigureMotion
                    // (หรือค่าที่ Speed/Direction Timeline พักไว้ระหว่าง Path) ใช้ต่อได้เลย
                    case PathEndBehavior.ContinueFlying:
                    default:
                        break;
                }
            }
        }

        private static Vector3 QuadraticBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector3 QuadraticBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }

        private void UpdateFacingRotation()
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg;

            rb.MoveRotation(angle + spriteAngleOffset);
        }

        private void UpdateSpeed()
        {
            if (useSpeedTimeline)
            {
                UpdateSpeedTimeline(Time.fixedDeltaTime);
                return;
            }

            if (Mathf.Abs(currentAcceleration) > 0.000001f)
            {
                currentSpeed +=
                    currentAcceleration *
                    Time.fixedDeltaTime;

                currentSpeed = Mathf.Clamp(
                    currentSpeed,
                    currentMinSpeed,
                    currentMaxSpeed
                );
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

        public void SetAcceleration(
            float newAcceleration,
            float newMinSpeed,
            float newMaxSpeed)
        {
            currentAcceleration = newAcceleration;
            currentMinSpeed = Mathf.Max(0f, newMinSpeed);
            currentMaxSpeed =
                Mathf.Max(currentMinSpeed, newMaxSpeed);

            useSpeedTimeline = false;
            speedTimeline = null;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            speedTimelineActive = false;
        }

        /// <summary>
        /// pathMotion: ถ้าไม่ null และ enabled มา จะให้กระสุนวิ่งตาม Path (เส้นตรง/โค้ง) แทน Direction/Speed
        /// ปกติชั่วคราว จนกว่าจะถึงปลาย Path แล้วค่อยตัดสินใจตาม endBehavior
        ///
        /// directionTimelineCatchUpSeconds: ใช้กับ Sync Redirect - ถ้า > 0 จะเรียก UpdateDirectionTimeline
        /// ทันทีหนึ่งครั้งด้วย delta ก้อนนี้ เพื่อ "ไล่ตาม" ให้ Redirect ตรงกับกระสุนนัดอื่นที่ Spawn ก่อนหน้า
        /// ในจังหวะเดียวกัน (ใช้ Logic เดิมของ UpdateDirectionTimeline ที่รองรับ delta ก้อนใหญ่อยู่แล้ว)
        /// </summary>
        public void ConfigureMotion(
            bool overrideInitialSpeed,
            float initialSpeed,
            List<SpeedTransition> timeline,
            List<DirectionTransition> newDirectionTimeline = null,
            List<BulletBurstTrigger> newBurstTimeline = null,
            IEnumerable<BulletCustomPattern> newPatternCallChain = null,
            ResolvedPathMotion pathMotion = null,
            float directionTimelineCatchUpSeconds = 0f)
        {
            returnedToPool = false;
            isDying = false;

            currentSpeed =
                overrideInitialSpeed
                    ? initialSpeed
                    : baseSpeed;

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

            directionTimeline = newDirectionTimeline;
            directionTransitionIndex = 0;
            directionTransitionTimer = 0f;
            directionSegmentInitialized = false;

            directionTimelineActive =
                directionTimeline != null &&
                directionTimeline.Count > 0;

            useDirectionTimeline = directionTimelineActive;

            burstTimeline = newBurstTimeline;
            patternCallChain = newPatternCallChain;
            burstTimelineClock = 0f;
            activeBurstExecutors.Clear();

            int triggerCount =
                burstTimeline != null
                    ? burstTimeline.Count
                    : 0;

            burstTriggerFired =
                triggerCount > 0
                    ? new bool[triggerCount]
                    : null;

            usingPathMotion = pathMotion != null;

            if (usingPathMotion)
            {
                pathStart = rb.position;
                pathEnd = pathMotion.endPosition;
                pathHasCurve = pathMotion.hasCurve;
                pathControl = pathMotion.controlPosition;
                pathDuration = Mathf.Max(0.01f, pathMotion.duration);
                pathEasing = pathMotion.easing;
                pathEndBehavior = pathMotion.endBehavior;
                pathTimer = 0f;
            }

            // Sync Redirect: ไล่ตาม Direction Timeline ทันทีหนึ่งครั้งด้วย delta ก้อนใหญ่
            // (ปลอดภัยเพราะ UpdateDirectionTimeline มี safety loop รองรับ delta เกิน Segment เดียวอยู่แล้ว)
            if (useDirectionTimeline && directionTimelineCatchUpSeconds > 0f)
                UpdateDirectionTimeline(directionTimelineCatchUpSeconds);
        }

        private void UpdateSpeedTimeline(float deltaTime)
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

            while (
                speedTransitionIndex < speedTimeline.Count &&
                safety++ < speedTimeline.Count + 2)
            {
                SpeedTransition transition =
                    speedTimeline[speedTransitionIndex];

                if (transition == null)
                {
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed = currentSpeed;
                    continue;
                }

                float duration =
                    Mathf.Max(0f, transition.duration);

                if (duration <= 0f)
                {
                    currentSpeed = transition.targetSpeed;
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed = currentSpeed;
                    continue;
                }

                speedTransitionTimer += deltaTime;

                float t =
                    Mathf.Clamp01(
                        speedTransitionTimer / duration
                    );

                float eased =
                    ApplyEase(t, transition.easing);

                currentSpeed =
                    Mathf.Lerp(
                        segmentStartSpeed,
                        transition.targetSpeed,
                        eased
                    );

                if (speedTransitionTimer >= duration)
                {
                    currentSpeed = transition.targetSpeed;
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed = currentSpeed;
                    continue;
                }

                break;
            }

            if (speedTransitionIndex >= speedTimeline.Count)
            {
                speedTimelineActive = false;
                useSpeedTimeline = false;
                speedTimeline = null;
            }
        }

        private void UpdateDirectionTimeline(float deltaTime)
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

            while (
                directionTransitionIndex < directionTimeline.Count &&
                safety++ < directionTimeline.Count + 2)
            {
                DirectionTransition transition =
                    directionTimeline[directionTransitionIndex];

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
                            directionSegmentStartAngle
                        );

                    directionSegmentInitialized = true;
                }

                float duration =
                    Mathf.Max(0f, transition.duration);

                if (duration <= 0f)
                {
                    direction =
                        AngleToVector(
                            directionSegmentTargetAngle
                        );

                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                directionTransitionTimer += deltaTime;

                float t =
                    Mathf.Clamp01(
                        directionTransitionTimer / duration
                    );

                float eased =
                    ApplyEase(t, transition.easing);

                float currentAngle =
                    Mathf.Lerp(
                        directionSegmentStartAngle,
                        directionSegmentTargetAngle,
                        eased
                    );

                direction = AngleToVector(currentAngle);

                if (directionTransitionTimer >= duration)
                {
                    direction =
                        AngleToVector(
                            directionSegmentTargetAngle
                        );

                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                break;
            }

            if (directionTransitionIndex >= directionTimeline.Count)
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
            if (transition.mode == DirectionRedirectMode.AimAtPlayer)
            {
                float aimAngle =
                    VectorToAngle(DirectionToPlayer());

                float delta =
                    Mathf.DeltaAngle(
                        startAngle,
                        aimAngle
                    );

                return startAngle + delta;
            }

            return startAngle + transition.relativeAngle;
        }

        private Vector2 DirectionToPlayer()
        {
            if (cachedPlayerTransform == null)
                return direction;

            Vector2 result =
                (Vector2)cachedPlayerTransform.position -
                rb.position;

            return result.sqrMagnitude < 0.000001f
                ? direction
                : result.normalized;
        }

        private static float VectorToAngle(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) *
                   Mathf.Rad2Deg;
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float radians =
                degrees * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians)
            );
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
                              2f
                          ) / 2f;

                default:
                    return t;
            }
        }

        private void UpdateBurstTimeline(float dt)
        {
            if (returnedToPool ||
                !isActiveAndEnabled)
            {
                return;
            }

            if (burstTimeline == null ||
                burstTimeline.Count == 0 ||
                burstTriggerFired == null)
            {
                return;
            }

            // Defensive guard:
            // ป้องกันกรณี List ถูกเปลี่ยนจำนวนหลัง ConfigureMotion()
            // จน bool[] มีขนาดไม่ตรงกับ Timeline
            if (burstTriggerFired.Length < burstTimeline.Count)
            {
                bool[] resized =
                    new bool[burstTimeline.Count];

                System.Array.Copy(
                    burstTriggerFired,
                    resized,
                    burstTriggerFired.Length
                );

                burstTriggerFired = resized;
            }

            burstTimelineClock += dt;

            for (int i = 0; i < burstTimeline.Count; i++)
            {
                // RequestDestroy() จาก FireBurstTrigger() สามารถ
                // Return object เข้า Pool ได้ทันที
                // ดังนั้นต้องเช็ค lifecycle ทุกครั้งหลัง FireBurstTrigger()
                if (returnedToPool ||
                    !isActiveAndEnabled ||
                    burstTimeline == null ||
                    burstTriggerFired == null)
                {
                    return;
                }

                if (i >= burstTriggerFired.Length)
                    return;

                if (burstTriggerFired[i])
                    continue;

                BulletBurstTrigger trigger =
                    burstTimeline[i];

                if (trigger == null)
                {
                    burstTriggerFired[i] = true;
                    continue;
                }

                if (burstTimelineClock < trigger.time)
                    continue;

                burstTriggerFired[i] = true;

                FireBurstTrigger(trigger);

                // สำคัญมาก:
                // FireBurstTrigger() อาจเรียก RequestDestroy()
                // และ RequestDestroy() อาจ ReturnToPoolNow()
                // ซึ่ง OnReturnToPool() จะล้าง burstTimeline และ
                // burstTriggerFired ทันที
                if (returnedToPool ||
                    !isActiveAndEnabled ||
                    burstTimeline == null ||
                    burstTriggerFired == null)
                {
                    return;
                }
            }
        }

        private void FireBurstTrigger(
            BulletBurstTrigger trigger)
        {
            if (returnedToPool || isDying || trigger == null)
                return;

            BulletCustomPattern targetPattern = trigger.pattern;

            if (targetPattern != null)
            {
                bool recursive =
                    patternCallChain != null &&
                    patternCallChain.Contains(targetPattern);

                if (recursive)
                {
                    Debug.LogWarning(
                        $"EnemyBulletV2: Burst Trigger '{trigger.name}' skipped because Pattern '{targetPattern.DisplayName}' is already in the current Pattern Call Chain.",
                        gameObject);
                }
                else
                {
                    List<BulletPatternGroup> clonedGroups =
                        targetPattern.CloneGroups();

                    if (clonedGroups.Count > 0)
                    {
                        var nextChain =
                            patternCallChain != null
                                ? new HashSet<BulletCustomPattern>(patternCallChain)
                                : new HashSet<BulletCustomPattern>();

                        nextChain.Add(targetPattern);

                        if (returnedToPool || isDying)
                            return;

                        BulletPatternV2Executor executor =
                            new BulletPatternV2Executor(
                                clonedGroups,
                                transform,
                                () => cachedPlayerTransform,
                                nextChain);

                        // Execute the first frame immediately so a zero-delay
                        // first Burst behaves the same as the main Pattern.
                        executor.Tick(0f);

                        if (!trigger.destroySelfAfterFire &&
                            !returnedToPool &&
                            !isDying)
                        {
                            activeBurstExecutors.Add(executor);
                        }
                    }
                }
            }

            if (trigger.destroySelfAfterFire)
                RequestDestroy();
        }

        private void TickActiveBurstExecutors(float dt)
        {
            if (returnedToPool ||
                isDying ||
                activeBurstExecutors.Count == 0)
            {
                return;
            }

            for (int i = 0;
                 i < activeBurstExecutors.Count;
                 i++)
            {
                if (returnedToPool || isDying)
                    return;

                activeBurstExecutors[i].Tick(dt);
            }
        }

        private void ResetMotionToPrefabDefaults()
        {
            direction = Vector2.down;

            currentSpeed = baseSpeed;
            currentAcceleration = baseAcceleration;
            currentMinSpeed = Mathf.Max(0f, baseMinSpeed);
            currentMaxSpeed =
                Mathf.Max(
                    currentMinSpeed,
                    baseMaxSpeed
                );

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

            burstTimeline = null;
            patternCallChain = null;
            burstTriggerFired = null;
            burstTimelineClock = 0f;
            activeBurstExecutors.Clear();

            usingPathMotion = false;
            pathTimer = 0f;
        }

        public void OnSpawnFromPool()
        {
            returnedToPool = false;

            ResetMotionToPrefabDefaults();

            isDying = false;

            if (col != null)
                col.enabled = true;

            if (deathAnimator != null)
            {
                deathAnimator.Rebind();
                deathAnimator.Update(0f);
            }

            CachePlayer();

            PlaySpawnSfx();
        }

        private void PlaySpawnSfx()
        {
            if (spawnSfx == null ||
                spawnSfx.clip == null)
                return;

            spawnSfx.PlayOneShot(spawnSfx.clip);
        }

        public void OnReturnToPool()
        {
            // ตั้ง flag ก่อนล้าง state เพื่อให้ method ที่กำลังทำงานอยู่
            // (เช่น UpdateBurstTimeline -> FireBurstTrigger -> RequestDestroy)
            // รู้ทันทีว่า object ถูกคืน Pool ไปแล้ว
            returnedToPool = true;

            speedTimeline = null;
            speedTimelineActive = false;
            useSpeedTimeline = false;

            directionTimeline = null;
            directionTimelineActive = false;
            useDirectionTimeline = false;

            burstTimeline = null;
            patternCallChain = null;
            burstTriggerFired = null;
            activeBurstExecutors.Clear();

            usingPathMotion = false;

            isDying = false;
        }

        public void RequestDestroy()
        {
            if (isDying || returnedToPool)
                return;

            if (deathAnimator == null)
            {
                ReturnToPoolNow();
                return;
            }

            isDying = true;

            if (col != null)
                col.enabled = false;

            deathAnimator.SetTrigger(deathTrigger);
        }

        public void OnDeathAnimationComplete()
        {
            if (returnedToPool)
                return;

            ReturnToPoolNow();
        }

        private void ReturnToPoolNow()
        {
            if (returnedToPool)
                return;

            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
            {
                returnedToPool = true;
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDying || returnedToPool)
                return;

            if (other.CompareTag("HiddenBorder"))
            {
                ReturnToPoolNow();
                return;
            }

            if (!other.CompareTag("Player"))
                return;

            if (other.TryGetComponent<PlayerControllerV1>(
                    out var player))
            {
                player.Die();
            }

            RequestDestroy();
        }
    }
}