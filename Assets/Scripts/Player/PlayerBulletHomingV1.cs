using UnityEngine;
using PoolingSystem;
using GameSystem;
using CombatSystem;

namespace PlayerSystem
{
    /// <summary>
    /// Player Homing Bullet used by Power Stage 4-5.
    ///
    /// The bullet acquires the nearest active Enemy/Boss once when it leaves
    /// the pool, then continuously steers toward that target.
    /// If the target becomes inactive, the bullet keeps its last direction
    /// and does not acquire a replacement target.
    ///
    /// The transform rotates to match currentDirection, so the sprite
    /// visually follows the actual curved movement.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerBulletHomingV1 : MonoBehaviour, IPoolable
    {
        [SerializeField] private float speed = 8f;
        [SerializeField] private int damage = 1;

        [Header("Homing")]
        [Tooltip("Maximum turning speed in degrees per second.")]
        [SerializeField] private float turnSpeed = 220f;

        [Tooltip("Maximum radius used to acquire the nearest Enemy/Boss.")]
        [SerializeField] private float acquireRadius = 15f;

        [Header("Target Detection")]
        [Tooltip("Layers containing Enemy/Boss colliders.")]
        [SerializeField] private LayerMask targetLayers = Physics2D.AllLayers;

        [Tooltip("Only accept colliders tagged Enemy or Boss.")]
        [SerializeField] private bool requireEnemyOrBossTag = true;

        [Header("Visual")]
        [Tooltip("Rotate the bullet to face its actual movement direction.")]
        [SerializeField] private bool rotateToFaceDirection = true;

        [Tooltip("Extra rotation if the sprite artwork does not face upward.")]
        [SerializeField] private float spriteAngleOffset = 0f;

        [Header("Score")]
        [Tooltip("Score awarded whenever this bullet hits an Enemy/Boss.")]
        [SerializeField] private int hitScoreValue = 10;

        [Header("Death Animation (Optional)")]
        [Tooltip("If assigned, play this animation before returning to the pool.")]
        [SerializeField] private Animator deathAnimator;

        [Tooltip("Trigger parameter used by the death Animator.")]
        [SerializeField] private string deathTrigger = "Death";

        private Rigidbody2D rb;
        private CircleCollider2D col;

        private Transform target;
        private Vector2 currentDirection = Vector2.up;
        private bool isDying;

        // Reused for every acquisition.
        // This avoids allocating a new Collider2D[] for every homing bullet.
        private readonly Collider2D[] targetResults = new Collider2D[64];

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (isDying)
                return;

            UpdateDirectionTowardTarget();

            Vector2 nextPosition =
                rb.position + currentDirection * speed * Time.fixedDeltaTime;

            rb.MovePosition(nextPosition);

            if (rotateToFaceDirection)
                ApplyVisualRotation();
        }

        /// <summary>
        /// Sets the direction used immediately after spawning.
        /// This is useful for bullets that spawn from the left/right side.
        /// </summary>
        public void SetInitialDirection(Vector2 direction)
        {
            currentDirection =
                direction.sqrMagnitude <= 0.0001f
                    ? Vector2.up
                    : direction.normalized;

            if (rotateToFaceDirection)
                ApplyVisualRotation();
        }

        private void UpdateDirectionTowardTarget()
        {
            // Target was killed/returned to pool.
            // Keep moving using the last known direction.
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                target = null;
                return;
            }

            Vector2 toTarget =
                (Vector2)target.position - rb.position;

            if (toTarget.sqrMagnitude <= 0.0001f)
                return;

            Vector2 desiredDirection = toTarget.normalized;

            float angle =
                Vector2.SignedAngle(currentDirection, desiredDirection);

            float maxRotation =
                turnSpeed * Time.fixedDeltaTime;

            float rotation =
                Mathf.Clamp(angle, -maxRotation, maxRotation);

            currentDirection =
                Quaternion.Euler(0f, 0f, rotation) * currentDirection;

            currentDirection.Normalize();
        }

        private void ApplyVisualRotation()
        {
            if (currentDirection.sqrMagnitude <= 0.0001f)
                return;

            // Sprite is assumed to face upward.
            float angle =
                Mathf.Atan2(currentDirection.y, currentDirection.x)
                * Mathf.Rad2Deg
                - 90f;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle + spriteAngleOffset);
        }

        /// <summary>
        /// Finds the nearest Enemy/Boss using Physics2D.
        ///
        /// This is more suitable for a bullet-hell setup than
        /// FindGameObjectsWithTag because it searches colliders near
        /// the bullet instead of scanning every tagged GameObject.
        /// NonAlloc also avoids creating a new array every shot.
        /// </summary>
        private void AcquireTarget()
        {
            target = null;

            if (acquireRadius <= 0f)
                return;

            Vector2 selfPosition = rb.position;

            int hitCount =
                Physics2D.OverlapCircleNonAlloc(
                    selfPosition,
                    acquireRadius,
                    targetResults,
                    targetLayers);

            float nearestDistanceSqr =
                acquireRadius * acquireRadius;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D candidateCollider = targetResults[i];

                if (candidateCollider == null)
                    continue;

                // The collider may belong to a child object while the
                // Enemy/Boss component and tag live on the root object.
                // Resolve the actual damageable owner first.
                IDamageable damageable =
                    candidateCollider.GetComponentInParent<IDamageable>();

                if (damageable == null)
                    continue;

                Component damageableComponent = damageable as Component;

                if (damageableComponent == null)
                    continue;

                GameObject candidate = damageableComponent.gameObject;

                if (!candidate.activeInHierarchy)
                    continue;

                if (requireEnemyOrBossTag &&
                    !candidate.CompareTag("Enemy") &&
                    !candidate.CompareTag("Boss"))
                {
                    continue;
                }

                Vector2 candidatePosition =
                    candidate.transform.position;

                float distanceSqr =
                    (candidatePosition - selfPosition).sqrMagnitude;

                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    target = candidate.transform;
                }
            }
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

            if (!other.CompareTag("Enemy") &&
                !other.CompareTag("Boss"))
            {
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damage, gameObject);

                if (hitScoreValue != 0 &&
                    ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.AddScore(hitScoreValue);
                }
            }

            RequestDestroy();
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

        /// <summary>
        /// Animation Event on the final frame of the death animation.
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

        public void OnSpawnFromPool()
        {
            // Reset all pooled state.
            currentDirection = Vector2.up;
            target = null;
            isDying = false;

            if (col != null)
                col.enabled = true;

            if (deathAnimator != null)
            {
                deathAnimator.Rebind();
                deathAnimator.Update(0f);
            }

            AcquireTarget();

            if (rotateToFaceDirection)
                ApplyVisualRotation();
        }

        public void OnReturnToPool()
        {
            target = null;
            isDying = false;
            currentDirection = Vector2.up;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                transform.position,
                acquireRadius);
        }
    }
}