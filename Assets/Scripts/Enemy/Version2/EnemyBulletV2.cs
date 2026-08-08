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
    /// Timeline starts at 0 immediately when the bullet is spawned from the pool.
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
        }

        private void FixedUpdate()
        {
            UpdateSpeed();

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
            currentSpeed =
                Mathf.Max(0f, newSpeed);

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
        /// This is the zero-time origin of the Speed Timeline.
        /// </summary>
        public void ConfigureMotion(
            bool overrideInitialSpeed,
            float initialSpeed,
            List<SpeedTransition> timeline)
        {
            currentSpeed =
                overrideInitialSpeed
                    ? Mathf.Max(0f, initialSpeed)
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
                    currentSpeed =
                        Mathf.Max(
                            0f,
                            transition.targetSpeed);

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
                        Mathf.Max(
                            0f,
                            transition.targetSpeed),
                        eased);

                if (speedTransitionTimer >=
                    duration)
                {
                    currentSpeed =
                        Mathf.Max(
                            0f,
                            transition.targetSpeed);

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
        }

        public void OnSpawnFromPool()
        {
            ResetMotionToPrefabDefaults();
        }

        public void OnReturnToPool()
        {
            speedTimeline = null;
            speedTimelineActive = false;
            useSpeedTimeline = false;
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
