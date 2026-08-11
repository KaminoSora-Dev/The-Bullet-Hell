using System;
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

        // ============================================================
        // เพิ่มใหม่: Death Animation (Optional) + Spawn SFX (Optional)
        // ============================================================
        [Header("Death Animation (Optional)")]
        [Tooltip("ถ้าลากมาใส่ - ตอนกระสุนถูกทำลาย (ไม่นับ HiddenBorder) จะเล่นแอนิเมชันนี้ก่อนค่อยกลับ Pool จริง " +
                 "ถ้าไม่ใส่ไว้ กระสุนจะกลับ Pool ทันทีเหมือนพฤติกรรมเดิมทุกประการ")]
        [SerializeField] private Animator deathAnimator;
        [Tooltip("ชื่อ Trigger parameter บน Animator ที่จะสั่งตอนกระสุนถูกทำลาย")]
        [SerializeField] private string deathTrigger = "Death";

        [Header("Spawn SFX (Optional)")]
        [Tooltip("เสียงตอนกระสุนนี้ spawn (ยิงออกมา) - ปล่อยว่างไว้ถ้าไม่ต้องการเสียง")]
        [SerializeField] private AudioClip spawnSfx;

        private CircleCollider2D col;
        private bool isDying;

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

        private List<DirectionTransition> directionTimeline;
        private int directionTransitionIndex;
        private float directionTransitionTimer;
        private float directionSegmentStartAngle;
        private float directionSegmentTargetAngle;
        private bool directionSegmentInitialized;
        private bool directionTimelineActive;
        private bool useDirectionTimeline;

        private Transform cachedPlayerTransform;

        public float CurrentSpeed => currentSpeed;
        public Vector2 Direction => direction;
        public float DefaultSpeed => baseSpeed;

        /// <summary>
        /// เพิ่มใหม่: static event - AudioManager ฟังตัวนี้ตัวเดียว ไม่ต้องรู้จักกระสุนทีละ instance
        /// ยิงทุกครั้งที่กระสุนที่มี spawnSfx ผูกไว้ถูกดึงจาก Pool มาใช้
        /// </summary>
        public static event Action<AudioClip> OnAnyBulletSpawnSfx;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            col = GetComponent<CircleCollider2D>();
            if (col != null) col.isTrigger = true;

            ResetMotionToPrefabDefaults();
            CachePlayer();
        }

        private void FixedUpdate()
        {
            // เพิ่มใหม่: หยุดขยับระหว่างเล่น Death Animation
            if (isDying) return;

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

            if (Mathf.Abs(currentAcceleration) > 0.000001f)
            {
                currentSpeed += currentAcceleration * Time.fixedDeltaTime;
                currentSpeed = Mathf.Clamp(currentSpeed, currentMinSpeed, currentMaxSpeed);
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

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

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

        public void SetAcceleration(float newAcceleration, float newMinSpeed, float newMaxSpeed)
        {
            currentAcceleration = newAcceleration;
            currentMinSpeed = Mathf.Max(0f, newMinSpeed);
            currentMaxSpeed = Mathf.Max(currentMinSpeed, newMaxSpeed);

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
            List<DirectionTransition> newDirectionTimeline = null)
        {
            currentSpeed = overrideInitialSpeed ? initialSpeed : baseSpeed;

            currentAcceleration = 0f;
            currentMinSpeed = 0f;
            currentMaxSpeed = float.MaxValue;

            speedTimeline = timeline;
            speedTransitionIndex = 0;
            speedTransitionTimer = 0f;
            segmentStartSpeed = currentSpeed;

            speedTimelineActive = speedTimeline != null && speedTimeline.Count > 0;
            useSpeedTimeline = speedTimelineActive;

            directionTimeline = newDirectionTimeline;
            directionTransitionIndex = 0;
            directionTransitionTimer = 0f;
            directionSegmentInitialized = false;

            directionTimelineActive = directionTimeline != null && directionTimeline.Count > 0;
            useDirectionTimeline = directionTimelineActive;
        }

        private void UpdateSpeedTimeline(float deltaTime)
        {
            if (!speedTimelineActive || speedTimeline == null || speedTimeline.Count == 0)
            {
                speedTimelineActive = false;
                useSpeedTimeline = false;
                return;
            }

            int safety = 0;

            while (speedTransitionIndex < speedTimeline.Count && safety++ < speedTimeline.Count + 2)
            {
                SpeedTransition transition = speedTimeline[speedTransitionIndex];

                if (transition == null)
                {
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed = currentSpeed;
                    continue;
                }

                float duration = Mathf.Max(0f, transition.duration);

                if (duration <= 0f)
                {
                    currentSpeed = transition.targetSpeed;
                    speedTransitionIndex++;
                    speedTransitionTimer = 0f;
                    segmentStartSpeed = currentSpeed;
                    continue;
                }

                speedTransitionTimer += deltaTime;

                float t = Mathf.Clamp01(speedTransitionTimer / duration);
                float eased = ApplyEase(t, transition.easing);

                currentSpeed = Mathf.Lerp(segmentStartSpeed, transition.targetSpeed, eased);

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
            if (!directionTimelineActive || directionTimeline == null || directionTimeline.Count == 0)
            {
                directionTimelineActive = false;
                useDirectionTimeline = false;
                return;
            }

            int safety = 0;

            while (directionTransitionIndex < directionTimeline.Count && safety++ < directionTimeline.Count + 2)
            {
                DirectionTransition transition = directionTimeline[directionTransitionIndex];

                if (transition == null)
                {
                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                if (!directionSegmentInitialized)
                {
                    directionSegmentStartAngle = VectorToAngle(direction);
                    directionSegmentTargetAngle = CalculateTargetAngle(transition, directionSegmentStartAngle);
                    directionSegmentInitialized = true;
                }

                float duration = Mathf.Max(0f, transition.duration);

                if (duration <= 0f)
                {
                    direction = AngleToVector(directionSegmentTargetAngle);
                    directionTransitionIndex++;
                    directionTransitionTimer = 0f;
                    directionSegmentInitialized = false;
                    continue;
                }

                directionTransitionTimer += deltaTime;

                float t = Mathf.Clamp01(directionTransitionTimer / duration);
                float eased = ApplyEase(t, transition.easing);

                float currentAngle = Mathf.Lerp(directionSegmentStartAngle, directionSegmentTargetAngle, eased);
                direction = AngleToVector(currentAngle);

                if (directionTransitionTimer >= duration)
                {
                    direction = AngleToVector(directionSegmentTargetAngle);
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

        private float CalculateTargetAngle(DirectionTransition transition, float startAngle)
        {
            if (transition.mode == DirectionRedirectMode.AimAtPlayer)
            {
                float aimAngle = VectorToAngle(DirectionToPlayer());
                float delta = Mathf.DeltaAngle(startAngle, aimAngle);
                return startAngle + delta;
            }

            return startAngle + transition.relativeAngle;
        }

        private Vector2 DirectionToPlayer()
        {
            if (cachedPlayerTransform == null)
                return direction;

            Vector2 result = (Vector2)cachedPlayerTransform.position - rb.position;

            return result.sqrMagnitude < 0.000001f ? direction : result.normalized;
        }

        private static float VectorToAngle(Vector2 v)
        {
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float ApplyEase(float t, SpeedEasing easing)
        {
            t = Mathf.Clamp01(t);

            switch (easing)
            {
                case SpeedEasing.EaseIn:
                    return t * t;
                case SpeedEasing.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case SpeedEasing.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }

        private void ResetMotionToPrefabDefaults()
        {
            direction = Vector2.down;

            currentSpeed = baseSpeed;
            currentAcceleration = baseAcceleration;
            currentMinSpeed = Mathf.Max(0f, baseMinSpeed);
            currentMaxSpeed = Mathf.Max(currentMinSpeed, baseMaxSpeed);

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

            // เพิ่มใหม่: reset สถานะตาย + เปิด collider กลับ + rebind Animator กันค้าง state เดิม
            isDying = false;
            if (col != null) col.enabled = true;

            if (deathAnimator != null)
            {
                deathAnimator.Rebind();
                deathAnimator.Update(0f);
            }

            CachePlayer();

            if (spawnSfx != null)
                OnAnyBulletSpawnSfx?.Invoke(spawnSfx);
        }

        public void OnReturnToPool()
        {
            speedTimeline = null;
            speedTimelineActive = false;
            useSpeedTimeline = false;

            directionTimeline = null;
            directionTimelineActive = false;
            useDirectionTimeline = false;

            isDying = false; // เพิ่มใหม่
        }

        /// <summary>
        /// เพิ่มใหม่: เรียกจุดนี้แทน PoolManager.Instance.Return(gameObject) ตรงๆ ทุกที่ที่ต้อง "ทำลาย" กระสุนนี้
        /// (ยกเว้นตอนชน HiddenBorder ที่ยัง Return ตรงๆ เหมือนเดิม เพราะเป็นการหายออกนอกจอ ไม่ใช่ถูกทำลาย)
        /// มี deathAnimator ผูกไว้ -> เล่นแอนิเมชันก่อน แล้วรอ Animation Event เรียก OnDeathAnimationComplete()
        /// ไม่มี deathAnimator -> Return ทันทีเหมือนพฤติกรรมเดิม (กระสุนที่ไม่ได้ผูกแอนิเมชันไว้ไม่พัง)
        /// </summary>
        public void RequestDestroy()
        {
            if (isDying) return;

            if (deathAnimator == null)
            {
                ReturnToPoolNow();
                return;
            }

            isDying = true;
            if (col != null) col.enabled = false;

            deathAnimator.SetTrigger(deathTrigger);
        }

        /// <summary>
        /// ผูกกับ Animation Event ที่เฟรมสุดท้ายของคลิป Death ใน Animator
        /// (เปิด Animation clip -> คลิกเฟรมสุดท้าย -> Add Event -> เลือกฟังก์ชันนี้)
        /// </summary>
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
            if (isDying) return; // เพิ่มใหม่: กำลังเล่น Death Animation อยู่ (collider ปิดไปแล้วด้วยปกติ)

            if (other.CompareTag("HiddenBorder"))
            {
                ReturnToPoolNow(); // หายนอกจอ ไม่ใช่ถูกทำลาย - ไม่เล่นแอนิเมชัน
                return;
            }

            if (!other.CompareTag("Player"))
                return;

            if (other.TryGetComponent<PlayerControllerV1>(out var player))
            {
                player.Die();
            }

            RequestDestroy(); // เปลี่ยนจาก Return() ตรงๆ
        }
    }
}
