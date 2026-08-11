using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;

namespace BackgroundSystem
{
    /// <summary>
    /// ช่วงค่า Min/Max สำหรับสุ่มตัวเลข - แยกออกมาจาก Vector2 โดยเฉพาะ เพราะ Vector2 มี field ชื่อ .x/.y
    /// ติดตัวมาเสมอ ซึ่งทำให้เข้าใจผิดว่าเป็นแกนพิกัด X/Y ได้ง่ายเวลาเอามาใช้แทนค่า Min/Max ทั่วไป
    /// </summary>
    [System.Serializable]
    public struct FloatRange
    {
        public float min;
        public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>สุ่มค่าในช่วง min-max นี้</summary>
        public float Roll() => UnityEngine.Random.Range(min, max);
    }

    /// <summary>
    /// รูปแบบ Ease เฉพาะของระบบ Background - ตั้งชื่อแยกจาก EnemySystem.EaseType / EnemySystem.SpeedEasing
    /// เพื่อกัน namespace ชนกันตาม convention เดิมของโปรเจกต์
    /// </summary>
    public enum BackgroundEaseType
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// 1 จุดของการเปลี่ยนทิศทาง - เริ่มเปลี่ยนทิศตอนเวลา (วินาที) หลัง Spawn ถึง atTime แล้วไล่มุมจาก
    /// ทิศปัจจุบัน ไปยัง targetAngle ภายในเวลา transitionDuration (วินาที) ด้วย easing ที่กำหนด
    /// มุม: -90 = ลงล่าง (ค่า Default), 0 = ขวา, 90 = ขึ้น, 180/-180 = ซ้าย (ตามมาตรฐาน atan2)
    /// </summary>
    [System.Serializable]
    public class BackgroundDirectionKeyframe
    {
        [Tooltip("เริ่มเปลี่ยนทิศตอนกี่วินาทีหลัง Spawn")]
        [Min(0f)]
        public float atTime = 2f;

        [Tooltip("-90 = ลงล่าง, 0 = ขวา, 90 = ขึ้น, 180/-180 = ซ้าย")]
        public float targetAngle = -90f;

        [Tooltip("ใช้เวลากี่วินาทีในการไล่จากทิศเดิมไปยัง targetAngle")]
        [Min(0.01f)]
        public float transitionDuration = 1f;

        public BackgroundEaseType easing = BackgroundEaseType.Linear;
    }

    /// <summary>
    /// Sprite ฉากหลัง 1 ตัว - เคลื่อนที่เป็นเส้นตรงตามทิศทาง (Direction) ด้วยความเร็วคงที่
    /// รองรับเปลี่ยนทิศทางตามเวลาผ่าน Direction Timeline (ตั้งค่าจาก BackgroundSpriteEmitterV1 ตอน Spawn)
    ///
    /// baseSpeed / baseScale บน Component นี้ใช้เป็นค่า Default ของตัว Prefab เอง
    /// ถ้า Pattern จาก Emitter ไม่ได้ Override มา จะใช้ค่าจากตรงนี้ - ตั้งได้อิสระต่อ Prefab แต่ละแบบ
    /// </summary>
    public class BackgroundSpriteFlowV1 : MonoBehaviour, IPoolable
    {
        [Header("Base Motion (ใช้เมื่อ Pattern ไม่ได้ Override มา)")]
        [SerializeField, Min(0f)] private float baseSpeed = 2f;
        [SerializeField, Min(0f)] private float baseScale = 1f;

        [Header("Safety")]
        [Tooltip("เวลาสูงสุดที่ Sprite นี้จะอยู่ได้ก่อนถูกคืน Pool อัตโนมัติ (กัน Pool เต็มถ้าหลุดจอไปแล้วไม่มีใครคืนให้)")]
        [SerializeField, Min(1f)] private float maxLifetime = 15f;

        private Vector2 direction;
        private float currentAngle;
        private float speed;

        private List<BackgroundDirectionKeyframe> directionTimeline;
        private int timelineIndex;
        private bool segmentActive;
        private float segmentStartAngle;

        private float elapsed;
        private float lifeTimer;

        /// <summary>
        /// เรียกโดย BackgroundSpriteEmitterV1 ทันทีหลัง PoolManager.Get() - นี่คือจุดเริ่มต้น (elapsed = 0)
        /// speedOverride / scaleOverride ส่ง null มาได้ถ้าอยากใช้ baseSpeed / baseScale ของ Prefab เอง
        /// </summary>
        public void Configure(
            float initialAngleDegrees,
            List<BackgroundDirectionKeyframe> keyframes,
            float? speedOverride,
            float? scaleOverride)
        {
            currentAngle = initialAngleDegrees;
            direction = AngleToVector(currentAngle);

            speed = speedOverride ?? baseSpeed;

            float scale = scaleOverride ?? baseScale;
            transform.localScale = Vector3.one * scale;

            directionTimeline = keyframes;
            timelineIndex = 0;
            segmentActive = false;
            segmentStartAngle = currentAngle;

            elapsed = 0f;
            lifeTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            lifeTimer += dt;

            UpdateDirectionTimeline(dt);

            transform.position += (Vector3)(direction * speed * dt);

            if (lifeTimer >= maxLifetime)
                ReturnSelfToPool();
        }

        private void UpdateDirectionTimeline(float dt)
        {
            if (directionTimeline == null || timelineIndex >= directionTimeline.Count)
                return;

            BackgroundDirectionKeyframe kf = directionTimeline[timelineIndex];

            if (elapsed < kf.atTime)
                return; // ยังไม่ถึงเวลาที่จะเริ่มเปลี่ยนทิศของจุดนี้

            if (!segmentActive)
            {
                segmentStartAngle = currentAngle;
                segmentActive = true;
            }

            float duration = Mathf.Max(0.01f, kf.transitionDuration);
            float segT = Mathf.Clamp01((elapsed - kf.atTime) / duration);
            float eased = ApplyEase(segT, kf.easing);

            currentAngle = Mathf.LerpAngle(segmentStartAngle, kf.targetAngle, eased);
            direction = AngleToVector(currentAngle);

            if (segT >= 1f)
            {
                timelineIndex++;
                segmentActive = false;
            }
        }

        private void ReturnSelfToPool()
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Return(gameObject);
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        private static float ApplyEase(float t, BackgroundEaseType easing)
        {
            t = Mathf.Clamp01(t);

            switch (easing)
            {
                case BackgroundEaseType.EaseIn:
                    return t * t;

                case BackgroundEaseType.EaseOut:
                    return 1f - (1f - t) * (1f - t);

                case BackgroundEaseType.EaseInOut:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default: // Linear
                    return t;
            }
        }

        // ============================================================
        // IPoolable
        // ============================================================

        public void OnSpawnFromPool()
        {
            // ไม่ต้องทำอะไรเพิ่ม - BackgroundSpriteEmitterV1 จะเรียก Configure() ทันทีหลังจากนี้เสมอ
        }

        public void OnReturnToPool()
        {
            directionTimeline = null;
            timelineIndex = 0;
            segmentActive = false;
        }
    }
}