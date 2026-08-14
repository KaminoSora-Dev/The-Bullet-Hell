using UnityEngine;
using PoolingSystem;
using GameSystem;
using CombatSystem;

namespace PlayerSystem
{
    /// <summary>
    /// Player Homing Bullet used by Power Stage 4-5.
    ///
    /// Target detection:
    /// - Does NOT depend on LayerMask.
    /// - Searches all 2D colliders using Physics2D.AllLayers.
    /// - Filters targets using Enemy/Boss tags.
    /// - Requires an IDamageable component.
    /// - Supports Enemy/Boss colliders placed on child objects.
    ///
    /// Homing behavior:
    /// - If no target is found, the bullet periodically searches again.
    /// - Once a target is found, it keeps that target.
    /// - If that target becomes inactive, the bullet searches for another one.
    ///
    /// Performance:
    /// - Uses OverlapCircleNonAlloc().
    /// - Reuses a fixed Collider2D buffer.
    /// - Does NOT scan every FixedUpdate.
    /// - Search frequency is controlled by targetSearchInterval.
    ///
    /// Debug:
    /// - Can report target acquisition and homing state.
    /// - Can draw a line from the bullet to its target.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerBulletHomingV1 : MonoBehaviour, IPoolable
    {
        // =========================================================
        // MOVEMENT
        // =========================================================

        [Header("Movement")]

        [SerializeField] private float speed = 8f;

        [SerializeField] private int damage = 1;


        // =========================================================
        // HOMING
        // =========================================================

        [Header("Homing")]

        [Tooltip("Maximum turning speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 220f;

        [Tooltip("Maximum radius used to search for Enemy/Boss.")]
        [SerializeField] private float acquireRadius = 15f;

        [Tooltip(
            "How often the bullet searches for a target when it currently has none.\n" +
            "Lower values = faster target acquisition but more Physics2D queries.")]
        [SerializeField] private float targetSearchInterval = 0.05f;


        // =========================================================
        // TARGET DETECTION
        // =========================================================

        [Header("Target Detection")]

        [Tooltip("Only accept objects tagged Enemy or Boss.")]
        [SerializeField] private bool requireEnemyOrBossTag = true;


        // =========================================================
        // DEBUG
        // =========================================================

        [Header("Debug")]

        [Tooltip("Enable detailed homing debug messages.")]
        [SerializeField] private bool enableDebugLogs = true;

        [Tooltip("Draw a line from bullet to target while homing.")]
        [SerializeField] private bool enableDebugLine = true;


        // =========================================================
        // VISUAL
        // =========================================================

        [Header("Visual")]

        [Tooltip("Rotate the bullet to face its actual movement direction.")]
        [SerializeField] private bool rotateToFaceDirection = true;

        [Tooltip("Extra rotation if the sprite artwork does not face upward.")]
        [SerializeField] private float spriteAngleOffset = 0f;


        // =========================================================
        // SCORE
        // =========================================================

        [Header("Score")]

        [Tooltip("Score awarded whenever this bullet hits an Enemy/Boss.")]
        [SerializeField] private int hitScoreValue = 10;


        // =========================================================
        // DEATH ANIMATION
        // =========================================================

        [Header("Death Animation (Optional)")]

        [SerializeField] private Animator deathAnimator;

        [SerializeField] private string deathTrigger = "Death";


        // =========================================================
        // SFX
        // =========================================================

        [Header("SFX (Optional)")]

        [SerializeField] private AudioClip deathSFX;


        // =========================================================
        // INTERNAL REFERENCES
        // =========================================================

        private Rigidbody2D rb;

        private CircleCollider2D col;


        // =========================================================
        // HOMING STATE
        // =========================================================

        private Transform target;

        private Vector2 currentDirection = Vector2.up;

        private bool isDying;

        // Used so the first target search happens after the bullet
        // has been positioned by the spawner/pool system.
        private bool waitingForInitialTarget;

        // Counts down until the next search is allowed.
        private float targetSearchTimer;

        // Prevents repeated "HOMING ACTIVE" messages.
        private bool hasLoggedHoming;


        // =========================================================
        // PHYSICS BUFFER
        // =========================================================

        // Reused for every search.
        //
        // This is important for Bullet Hell because allocating a new
        // Collider2D[] every search would create unnecessary GC pressure.
        private readonly Collider2D[] targetResults =
            new Collider2D[64];


        // =========================================================
        // UNITY
        // =========================================================

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            rb.bodyType = RigidbodyType2D.Kinematic;

            col = GetComponent<CircleCollider2D>();

            col.isTrigger = true;
        }


        // =========================================================
        // FIXED UPDATE
        // =========================================================

        private void FixedUpdate()
        {
            if (isDying)
                return;


            // -----------------------------------------------------
            // INITIAL SEARCH
            // -----------------------------------------------------
            //
            // Wait one FixedUpdate after spawning.
            //
            // This gives the firing system time to place the bullet
            // at its actual spawn position.
            // -----------------------------------------------------

            if (waitingForInitialTarget)
            {
                waitingForInitialTarget = false;

                // Search immediately on the first valid physics tick.
                targetSearchTimer = 0f;
            }


            // -----------------------------------------------------
            // TARGET SEARCH TIMER
            // -----------------------------------------------------

            targetSearchTimer -= Time.fixedDeltaTime;


            // -----------------------------------------------------
            // SEARCH ONLY WHEN WE DON'T HAVE A TARGET
            // -----------------------------------------------------
            //
            // This is important for performance.
            //
            // We do NOT search every FixedUpdate while already
            // homing toward a target.
            // -----------------------------------------------------

            if (target == null &&
                targetSearchTimer <= 0f)
            {
                targetSearchTimer =
                    Mathf.Max(
                        0.01f,
                        targetSearchInterval);

                AcquireTarget();

                if (target != null)
                {
                    DebugLog(
                        "🟡 TARGET FOUND | " +
                        $"Target={target.name} | " +
                        $"Tag={target.tag} | ");
                }
                else
                {
                    DebugLog(
                        "🔴 NO TARGET FOUND | " +
                        $"Position={transform.position} | " +
                        $"Radius={acquireRadius}");
                }
            }


            // -----------------------------------------------------
            // HOMING
            // -----------------------------------------------------

            UpdateDirectionTowardTarget();


            // -----------------------------------------------------
            // MOVEMENT
            // -----------------------------------------------------

            Vector2 nextPosition =
                rb.position +
                currentDirection *
                speed *
                Time.fixedDeltaTime;

            rb.MovePosition(nextPosition);


            // -----------------------------------------------------
            // VISUAL
            // -----------------------------------------------------

            if (rotateToFaceDirection)
                ApplyVisualRotation();
        }


        // =========================================================
        // INITIAL DIRECTION
        // =========================================================

        public void SetInitialDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                currentDirection = Vector2.up;
            }
            else
            {
                currentDirection =
                    direction.normalized;
            }

            DebugLog(
                $"Initial direction set to {currentDirection}");

            if (rotateToFaceDirection)
                ApplyVisualRotation();
        }


        // =========================================================
        // HOMING
        // =========================================================

        private void UpdateDirectionTowardTarget()
        {
            // -----------------------------------------------------
            // No target.
            //
            // FixedUpdate will continue searching periodically.
            // -----------------------------------------------------

            if (target == null)
                return;


            // -----------------------------------------------------
            // Target became inactive.
            //
            // Do NOT keep steering toward a dead pooled object.
            // Clear the target so FixedUpdate can acquire another one.
            // -----------------------------------------------------

            if (!target.gameObject.activeInHierarchy)
            {
                DebugLog(
                    $"🔴 TARGET LOST | " +
                    $"{target.name} became inactive.");

                target = null;

                hasLoggedHoming = false;

                // Search again immediately on the next FixedUpdate.
                targetSearchTimer = 0f;

                return;
            }


            // -----------------------------------------------------
            // Calculate direction to target.
            // -----------------------------------------------------

            Vector2 toTarget =
                (Vector2)target.position -
                rb.position;


            if (toTarget.sqrMagnitude <= 0.0001f)
                return;


            Vector2 desiredDirection =
                toTarget.normalized;


            // -----------------------------------------------------
            // Calculate angular difference.
            // -----------------------------------------------------

            float angle =
                Vector2.SignedAngle(
                    currentDirection,
                    desiredDirection);


            // -----------------------------------------------------
            // Calculate maximum rotation this physics tick.
            // -----------------------------------------------------

            float maxRotation =
                turnSpeed *
                Time.fixedDeltaTime;


            float rotation =
                Mathf.Clamp(
                    angle,
                    -maxRotation,
                    maxRotation);


            Vector2 previousDirection =
                currentDirection;


            // -----------------------------------------------------
            // Rotate actual movement direction.
            // -----------------------------------------------------

            currentDirection =
                Quaternion.Euler(
                    0f,
                    0f,
                    rotation) *
                currentDirection;

            currentDirection.Normalize();


            // -----------------------------------------------------
            // HOMING DEBUG
            // -----------------------------------------------------

            if (!hasLoggedHoming)
            {
                DebugLog(
                    "🟢 HOMING ACTIVE | " +
                    $"Target={target.name} | " +
                    $"AngleToTarget={angle:F2}° | " +
                    $"TurnThisFrame={rotation:F2}° | " +
                    $"CurrentDirection={currentDirection}");

                hasLoggedHoming = true;
            }


#if UNITY_EDITOR

            if (enableDebugLine)
            {
                Debug.DrawLine(
                    transform.position,
                    target.position,
                    Color.green);
            }

#endif


            // -----------------------------------------------------
            // SAFETY CHECK
            // -----------------------------------------------------

            if (Mathf.Abs(rotation) > 0.01f &&
                Vector2.Distance(
                    previousDirection,
                    currentDirection) < 0.00001f)
            {
                DebugLog(
                    "⚠ HOMING WARNING | " +
                    "A rotation was requested, but " +
                    "currentDirection did not change.");
            }
        }


        // =========================================================
        // VISUAL ROTATION
        // =========================================================

        private void ApplyVisualRotation()
        {
            if (currentDirection.sqrMagnitude <= 0.0001f)
                return;


            // Sprite artwork is assumed to face UP.
            float angle =
                Mathf.Atan2(
                    currentDirection.y,
                    currentDirection.x) *
                Mathf.Rad2Deg -
                90f;


            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle +
                    spriteAngleOffset);
        }


        // =========================================================
        // TARGET ACQUISITION
        // =========================================================

        private void AcquireTarget()
        {
            target = null;


            if (acquireRadius <= 0f)
                return;


            Vector2 selfPosition =
                rb.position;


            // -----------------------------------------------------
            // SEARCH ALL LAYERS
            // -----------------------------------------------------
            //
            // We intentionally use AllLayers.
            //
            // Layer is NOT used to determine whether something is
            // an Enemy/Boss.
            //
            // Tag is used for that.
            // -----------------------------------------------------

            int hitCount =
                Physics2D.OverlapCircleNonAlloc(
                    selfPosition,
                    acquireRadius,
                    targetResults,
                    Physics2D.AllLayers);


            float nearestDistanceSqr =
                float.PositiveInfinity;


            // -----------------------------------------------------
            // INSPECT EVERY COLLIDER
            // -----------------------------------------------------

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D candidateCollider =
                    targetResults[i];


                if (candidateCollider == null)
                    continue;


                // -------------------------------------------------
                // Resolve IDamageable.
                //
                // Collider may be on a child object.
                // -------------------------------------------------

                IDamageable damageable =
                    candidateCollider
                        .GetComponentInParent<IDamageable>();


                if (damageable == null)
                    continue;


                Component damageableComponent =
                    damageable as Component;


                if (damageableComponent == null)
                    continue;


                GameObject candidate =
                    damageableComponent.gameObject;


                if (!candidate.activeInHierarchy)
                    continue;


                // -------------------------------------------------
                // TAG FILTER
                // -------------------------------------------------
                //
                // This is now the actual Enemy/Boss filter.
                // -------------------------------------------------

                if (requireEnemyOrBossTag)
                {
                    bool isEnemy =
                        candidate.CompareTag("Enemy");

                    bool isBoss =
                        candidate.CompareTag("Boss");


                    if (!isEnemy && !isBoss)
                        continue;
                }


                // -------------------------------------------------
                // DISTANCE TO ACTUAL COLLIDER
                // -------------------------------------------------
                //
                // IMPORTANT:
                //
                // Do not rely only on candidate.transform.position.
                //
                // An Enemy's pivot might be outside the search radius
                // while its Collider2D is actually inside the radius.
                //
                // ClosestPoint() gives us the closest point on the
                // actual Collider2D.
                // -------------------------------------------------

                Vector2 closestPoint =
                    candidateCollider.ClosestPoint(
                        selfPosition);


                float distanceSqr =
                    (closestPoint - selfPosition)
                    .sqrMagnitude;


                // -------------------------------------------------
                // Keep nearest candidate.
                // -------------------------------------------------

                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr =
                        distanceSqr;

                    target =
                        candidate.transform;
                }
            }


            // -----------------------------------------------------
            // DEBUG RESULT
            // -----------------------------------------------------

#if UNITY_EDITOR

            if (target != null)
            {
                float actualDistance =
                    Mathf.Sqrt(
                        nearestDistanceSqr);


                Debug.Log(
                    $"[HomingBullet] TARGET FOUND: " +
                    $"{target.name} | " +
                    $"Tag={target.tag} | " +
                    $"ColliderDistance={actualDistance:F2}",
                    this);


                if (enableDebugLine)
                {
                    Debug.DrawLine(
                        transform.position,
                        target.position,
                        Color.green,
                        0.1f);
                }
            }

#endif
        }


        // =========================================================
        // COLLISION
        // =========================================================

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (isDying)
                return;


            // -----------------------------------------------------
            // Hidden Border
            // -----------------------------------------------------

            if (other.CompareTag("HiddenBorder"))
            {
                ReturnToPoolNow();

                return;
            }


            // -----------------------------------------------------
            // Find IDamageable.
            //
            // Supports child colliders.
            // -----------------------------------------------------

            IDamageable damageable =
                other.GetComponentInParent<IDamageable>();


            if (damageable == null)
                return;


            Component damageableComponent =
                damageable as Component;


            if (damageableComponent == null)
                return;


            GameObject damageableObject =
                damageableComponent.gameObject;


            // -----------------------------------------------------
            // Enemy / Boss tag
            // -----------------------------------------------------

            bool isEnemyOrBoss =
                damageableObject.CompareTag("Enemy") ||
                damageableObject.CompareTag("Boss");


            if (!isEnemyOrBoss)
                return;


            // -----------------------------------------------------
            // DAMAGE
            // -----------------------------------------------------

            damageable.TakeDamage(
                damage,
                gameObject);


            // -----------------------------------------------------
            // SCORE
            // -----------------------------------------------------

            if (hitScoreValue != 0 &&
                ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(
                    hitScoreValue);
            }


            // -----------------------------------------------------
            // DESTROY
            // -----------------------------------------------------

            RequestDestroy();
        }


        // =========================================================
        // DEATH
        // =========================================================

        public void RequestDestroy()
        {
            if (isDying)
                return;


            // -----------------------------------------------------
            // No death animation.
            // -----------------------------------------------------

            if (deathAnimator == null)
            {
                ReturnToPoolNow();

                return;
            }


            isDying = true;


            if (col != null)
                col.enabled = false;


            deathAnimator.SetTrigger(
                deathTrigger);
        }


        // =========================================================
        // DEATH ANIMATION EVENT
        // =========================================================

        public void OnDeathAnimationComplete()
        {
            ReturnToPoolNow();
        }


        // =========================================================
        // RETURN TO POOL
        // =========================================================

        private void ReturnToPoolNow()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Return(
                    gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }


        // =========================================================
        // POOL: SPAWN
        // =========================================================

        public void OnSpawnFromPool()
        {
            // -----------------------------------------------------
            // Reset movement.
            // -----------------------------------------------------

            currentDirection =
                Vector2.up;


            // -----------------------------------------------------
            // Reset target.
            // -----------------------------------------------------

            target = null;


            // -----------------------------------------------------
            // Reset death state.
            // -----------------------------------------------------

            isDying = false;


            // -----------------------------------------------------
            // First target search.
            // -----------------------------------------------------

            waitingForInitialTarget = true;

            targetSearchTimer = 0f;


            // -----------------------------------------------------
            // Reset debug state.
            // -----------------------------------------------------

            hasLoggedHoming = false;


            // -----------------------------------------------------
            // Enable collider.
            // -----------------------------------------------------

            if (col != null)
                col.enabled = true;


            // -----------------------------------------------------
            // Reset death animation.
            // -----------------------------------------------------

            if (deathAnimator != null)
            {
                deathAnimator.Rebind();

                deathAnimator.Update(0f);
            }


            DebugLog(
                $"🔵 SPAWNED | " +
                $"Position={transform.position}");
        }


        // =========================================================
        // POOL: RETURN
        // =========================================================

        public void OnReturnToPool()
        {
            target = null;

            isDying = false;

            waitingForInitialTarget = false;

            targetSearchTimer = 0f;

            hasLoggedHoming = false;

            currentDirection =
                Vector2.up;
        }


        // =========================================================
        // DEBUG LOG
        // =========================================================

        private void DebugLog(
            string message)
        {
            if (!enableDebugLogs)
                return;


            Debug.Log(
                $"[HomingBullet] {message}",
                this);
        }


        // =========================================================
        // GIZMOS
        // =========================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                transform.position,
                acquireRadius);
        }
    }
}