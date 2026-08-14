using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace EnemySystem
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
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
            "(อัปเดตทุกครั้งที่ทิศเปลี่ยน เช่นจาก Direction Timeline หรือ Redirect)\n" +
            "ไม่ติ๊ก = กระสุนไม่หมุน"
        )]
        [SerializeField] private bool rotateToFaceDirection = false;

        [Tooltip(
            "มุมชดเชย Sprite - ถ้า Sprite วาดหันขึ้นด้านบนอยู่แล้วให้ใส่ -90, " +
            "ถ้าวาดหันขวาอยู่แล้วใส่ 0"
        )]
        [SerializeField] private float spriteAngleOffset = -90f;

        private CircleCollider2D col;
        private Rigidbody2D rb;

        private bool isDying;

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
        private bool[] burstTriggerFired;
        private float burstTimelineClock;

        private readonly List<BulletPatternV2Executor> activeBurstExecutors =
            new List<BulletPatternV2Executor>();

        private Transform cachedPlayerTransform;

        public float CurrentSpeed => currentSpeed;
        public Vector2 Direction => direction;
        public float DefaultSpeed => baseSpeed;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            col = GetComponent<CircleCollider2D>();

            if (col != null)
                col.isTrigger = true;

            ResetMotionToPrefabDefaults();
            CachePlayer();
        }

        private void FixedUpdate()
        {
            if (isDying)
                return;

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
            if (isDying)
                return;

            UpdateBurstTimeline(Time.deltaTime);
            TickActiveBurstExecutors(Time.deltaTime);
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

        public void ConfigureMotion(
            bool overrideInitialSpeed,
            float initialSpeed,
            List<SpeedTransition> timeline,
            List<DirectionTransition> newDirectionTimeline = null,
            List<BulletBurstTrigger> newBurstTimeline = null)
        {
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
            if (burstTimeline == null ||
                burstTimeline.Count == 0)
                return;

            burstTimelineClock += dt;

            for (int i = 0; i < burstTimeline.Count; i++)
            {
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

                if (isDying)
                    return;
            }
        }

        private void FireBurstTrigger(
            BulletBurstTrigger trigger)
        {
            if (trigger.pattern != null &&
                trigger.pattern.Count > 0)
            {
                List<BulletPatternGroup> clonedGroups =
                    new List<BulletPatternGroup>(
                        trigger.pattern.Count
                    );

                for (int i = 0;
                     i < trigger.pattern.Count;
                     i++)
                {
                    BulletPatternGroup source =
                        trigger.pattern[i];

                    if (source != null)
                        clonedGroups.Add(source.Clone());
                }

                BulletPatternV2Executor executor =
                    new BulletPatternV2Executor(
                        clonedGroups,
                        transform,
                        () => cachedPlayerTransform
                    );

                executor.Tick(0f);

                if (!trigger.destroySelfAfterFire)
                    activeBurstExecutors.Add(executor);
            }

            if (trigger.destroySelfAfterFire)
                RequestDestroy();
        }

        private void TickActiveBurstExecutors(float dt)
        {
            if (activeBurstExecutors.Count == 0)
                return;

            for (int i = 0;
                 i < activeBurstExecutors.Count;
                 i++)
            {
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
            burstTriggerFired = null;
            burstTimelineClock = 0f;
            activeBurstExecutors.Clear();
        }

        public void OnSpawnFromPool()
        {
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
            speedTimeline = null;
            speedTimelineActive = false;
            useSpeedTimeline = false;

            directionTimeline = null;
            directionTimelineActive = false;
            useDirectionTimeline = false;

            burstTimeline = null;
            burstTriggerFired = null;
            activeBurstExecutors.Clear();

            isDying = false;
        }

        public void RequestDestroy()
        {
            if (isDying)
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
            ReturnToPoolNow();
        }

        private void ReturnToPoolNow()
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDying)
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