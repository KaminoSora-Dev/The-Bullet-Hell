using System;
using UnityEngine;
using PoolingSystem;

namespace PlayerSystem
{
    public enum PlayerState
    {
        Alive,
        Dead
    }

    public enum DeathVisualPhase
    {
        None,
        Blinking,
        FadingOut,
        FadingIn
    }

    public class PlayerControllerV1 : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float slowMoveSpeed = 2f;

        [Header("Respawn")]
        [SerializeField] private Transform spawnPoint;

        [Header("Death Visual - Blink")]
        [SerializeField] private int blinkCount = 3;
        [SerializeField] private float blinkInterval = 0.1f;
        [SerializeField] private Color blinkColor = Color.red;

        [Header("Death Visual - Fade")]
        [SerializeField] private float fadeOutDuration = 0.4f;
        [SerializeField] private float fadeInDuration = 0.4f;

        [Header("Invincibility (อมตะหลัง Respawn)")]
        [SerializeField] private float invincibleDuration = 2f;
        [SerializeField] private float invincibleBlinkInterval = 0.1f;
        [SerializeField] private Color invincibleColor = new Color(0.4f, 0.8f, 1f);

        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 10f;

        [Header("Spell / Bomb")]
        [SerializeField] private PlayerShieldV1 shield;
        [SerializeField] private float bombClearRadius = 5f;
        [SerializeField] private float shieldDuration = 2f;

        [Header("References")]
        [SerializeField] private SpriteRenderer bodySprite;
        [SerializeField] private CircleCollider2D hitboxCollider;
        [SerializeField] private SpriteRenderer hitboxVisual;

        [Header("Play Area Border")]
        [SerializeField] private float borderPadding = 0.1f;

        [Header("Play Area Gizmo")]
        [SerializeField] private bool showPlayAreaGizmo = true;
        [SerializeField] private Vector2 gizmoPositionOffset = Vector2.zero;
        [SerializeField] private Vector2 gizmoSizeOffset = Vector2.zero;

        private PlayerState currentState = PlayerState.Alive;

        private Vector2 moveInput;
        private bool isSlowMode;

        private DeathVisualPhase deathPhase = DeathVisualPhase.None;
        private float phaseTimer;
        private int blinksDone;
        private bool blinkIsColored;
        private Color originalColor;

        private bool canRespawnAfterThisDeath = true;

        private float nextFireTime;

        private bool isInvincible;
        private float invincibleTimer;
        private float invincibleBlinkTimer;
        private bool invincibleBlinkOn;

        private Bounds playAreaBounds;
        private bool hasPlayArea;

        // ============================================================
        // เพิ่มใหม่: hook สำหรับ AudioSystem / AnimationSystem แยกไฟล์
        // ============================================================

        /// <summary>อ่านค่า input การเคลื่อนที่ปัจจุบัน (สำหรับ AnimationController ใช้ตัดสิน Idle/Move + ทิศ)</summary>
        public Vector2 CurrentMoveInput => moveInput;

        public bool IsSlowMode => isSlowMode;

        /// <summary>ยิงทุกครั้งที่ผู้เล่นยิงกระสุนสำเร็จ (ผ่าน rate limit แล้ว)</summary>
        public event Action OnShoot;

        /// <summary>ยิงตอน Die() เริ่มทำงาน (ก่อน blink visual ใดๆ)</summary>
        public event Action OnDied;

        /// <summary>ยิงตอนใช้ Spell (Bomb) สำเร็จเท่านั้น (มี resource พอ)</summary>
        public event Action OnBombUsed;

        /// <summary>ยิงตอน death sequence จบและผู้เล่นควบคุมตัวได้อีกครั้ง (จบ FadingIn)</summary>
        public event Action OnRespawned;

        void Awake()
        {
            if (bodySprite != null)
                originalColor = bodySprite.color;
        }

        void Start()
        {
            CachePlayAreaBounds();
        }

        private void CachePlayAreaBounds()
        {
            GameObject[] borderObjs = GameObject.FindGameObjectsWithTag("UIBorder");
            if (borderObjs.Length == 0)
            {
                Debug.LogWarning("PlayerControllerV1: ไม่พบ GameObject ที่มี tag \"UIBorder\" ในฉาก - ผู้เล่นจะเดินออกนอกจอได้");
                return;
            }

            Bounds combined = default;
            bool boundsStarted = false;

            foreach (GameObject obj in borderObjs)
            {
                if (!obj.TryGetComponent<Collider2D>(out var col)) continue;

                if (!boundsStarted)
                {
                    combined = col.bounds;
                    boundsStarted = true;
                }
                else
                {
                    combined.Encapsulate(col.bounds);
                }
            }

            if (!boundsStarted)
            {
                Debug.LogWarning("PlayerControllerV1: เจอ GameObject tag \"UIBorder\" แต่ไม่มีตัวไหนมี Collider2D ติดอยู่เลย - อ่านขอบเขตไม่ได้");
                return;
            }

            playAreaBounds = combined;
            hasPlayArea = true;
        }

        void Update()
        {
            if (currentState == PlayerState.Alive)
            {
                ReadMovementInput();
                HandleActionInput();
                ApplyMovement();

                if (isInvincible)
                    UpdateInvincibility();
            }
            else
            {
                UpdateDeathSequence();
            }
        }

        private void ReadMovementInput()
        {
            float h = 0f;
            float v = 0f;

            if (Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            moveInput = new Vector2(h, v).normalized;

            isSlowMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (hitboxVisual != null)
                hitboxVisual.enabled = isSlowMode;
        }

        private void HandleActionInput()
        {
            if (Input.GetKey(KeyCode.Z) && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + (1f / fireRate);
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                Bomb();
            }
        }

        private void ApplyMovement()
        {
            float speed = isSlowMode ? slowMoveSpeed : moveSpeed;

            Vector3 nextPosition = transform.position + (Vector3)(moveInput * speed * Time.deltaTime);

            if (hasPlayArea)
            {
                nextPosition.x = Mathf.Clamp(nextPosition.x, playAreaBounds.min.x + borderPadding, playAreaBounds.max.x - borderPadding);
                nextPosition.y = Mathf.Clamp(nextPosition.y, playAreaBounds.min.y + borderPadding, playAreaBounds.max.y - borderPadding);
            }

            transform.position = nextPosition;
        }

        private void Shoot()
        {
            if (bulletPrefab == null || firePoint == null) return;

            GameObject bulletObj = PoolManager.Instance.Get(bulletPrefab, firePoint.position, firePoint.rotation);

            if (bulletObj.TryGetComponent<PlayerBulletV1>(out var bullet))
            {
                bullet.SetDirection(Vector2.up);
            }

            OnShoot?.Invoke(); // เพิ่มใหม่
        }

        private void Bomb()
        {
            if (PlayerStatsManager.Instance != null && !PlayerStatsManager.Instance.UseSpell())
            {
                return;
            }

            ClearBulletsInRadius(transform.position, bombClearRadius);

            if (shield != null)
            {
                shield.Activate(shieldDuration);
            }
            else
            {
                Debug.LogWarning("PlayerControllerV1: ไม่ได้ลาก PlayerShieldV1 ไว้ใน field \"shield\" - ใช้ spell ได้ แต่จะไม่มีโล่ขึ้น");
            }

            OnBombUsed?.Invoke(); // เพิ่มใหม่
        }

        private void ClearBulletsInRadius(Vector2 center, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);

            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag("EnemyBullet")) continue;

                // เพิ่มใหม่: ใช้ RequestDestroy() แทน Return() ตรงๆ เพื่อให้เล่น Death Animation ได้ถ้ามีผูกไว้
                if (hit.TryGetComponent<EnemySystem.EnemyBulletV2>(out var bulletV2))
                    bulletV2.RequestDestroy();
                else
                    PoolManager.Instance.Return(hit.gameObject);
            }
        }

        public void Die()
        {
            if (currentState == PlayerState.Dead) return;
            if (isInvincible) return;

            currentState = PlayerState.Dead;

            OnDied?.Invoke();

            if (hitboxCollider != null)
                hitboxCollider.enabled = false;

            if (hitboxVisual != null)
                hitboxVisual.enabled = false;

            // PlayerStatsManager เป็นตัวตัดสินว่า
            // หลังเสีย Life แล้ว Player ยัง Respawn ได้หรือไม่
            if (PlayerStatsManager.Instance != null)
            {
                canRespawnAfterThisDeath =
                    PlayerStatsManager.Instance.LoseLife();
            }
            else
            {
                // Fallback: ถ้าไม่มี Stats Manager
                // ให้ถือว่ายัง Respawn ได้ เพื่อไม่ให้เกิด NullReferenceException
                canRespawnAfterThisDeath = true;
            }

            deathPhase = DeathVisualPhase.Blinking;
            phaseTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;
        }

        private void UpdateDeathSequence()
        {
            phaseTimer += Time.deltaTime;

            switch (deathPhase)
            {
                case DeathVisualPhase.Blinking:
                    UpdateBlinking();
                    break;

                case DeathVisualPhase.FadingOut:
                    UpdateFadeOut();
                    break;

                case DeathVisualPhase.FadingIn:
                    UpdateFadeIn();
                    break;
            }
        }

        private void UpdateBlinking()
        {
            if (phaseTimer < blinkInterval) return;

            phaseTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (bodySprite != null)
                bodySprite.color = blinkIsColored ? blinkColor : originalColor;

            if (!blinkIsColored)
            {
                blinksDone++;
                if (blinksDone >= blinkCount)
                {
                    deathPhase = DeathVisualPhase.FadingOut;
                    phaseTimer = 0f;
                    if (bodySprite != null)
                        bodySprite.color = originalColor;
                }
            }
        }

        private void UpdateFadeOut()
        {
            float t = Mathf.Clamp01(phaseTimer / fadeOutDuration);
            SetAlpha(1f - t);

            if (t >= 1f)
            {
                if (canRespawnAfterThisDeath)
                {
                    RespawnPosition();
                    deathPhase = DeathVisualPhase.FadingIn;
                    phaseTimer = 0f;
                }
                else
                {
                    deathPhase = DeathVisualPhase.None;
                    Debug.Log("PlayerControllerV1: Game Over");
                }
            }
        }

        private void UpdateFadeIn()
        {
            float t = Mathf.Clamp01(phaseTimer / fadeInDuration);
            SetAlpha(t);

            if (t >= 1f)
            {
                SetAlpha(1f);
                if (hitboxCollider != null) hitboxCollider.enabled = true;

                deathPhase = DeathVisualPhase.None;
                currentState = PlayerState.Alive;

                StartInvincibility();

                OnRespawned?.Invoke(); // เพิ่มใหม่
            }
        }

        private void StartInvincibility()
        {
            isInvincible = true;
            invincibleTimer = 0f;
            invincibleBlinkTimer = 0f;
            invincibleBlinkOn = false;
        }

        private void UpdateInvincibility()
        {
            invincibleTimer += Time.deltaTime;

            invincibleBlinkTimer += Time.deltaTime;
            if (invincibleBlinkTimer >= invincibleBlinkInterval)
            {
                invincibleBlinkTimer = 0f;
                invincibleBlinkOn = !invincibleBlinkOn;

                if (bodySprite != null)
                    bodySprite.color = invincibleBlinkOn ? invincibleColor : originalColor;
            }

            if (invincibleTimer >= invincibleDuration)
            {
                EndInvincibility();
            }
        }

        private void EndInvincibility()
        {
            isInvincible = false;

            if (bodySprite != null)
                bodySprite.color = originalColor;
        }

        private void SetAlpha(float alpha)
        {
            if (bodySprite == null) return;
            Color c = bodySprite.color;
            c.a = alpha;
            bodySprite.color = c;
        }

        private void RespawnPosition()
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning("PlayerControllerV1: ไม่ได้กำหนด spawnPoint ไว้ - จะไม่ย้ายตำแหน่งตอน respawn");
                return;
            }

            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        void OnDrawGizmosSelected()
        {
            if (hitboxCollider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(
                    transform.position + (Vector3)hitboxCollider.offset,
                    hitboxCollider.radius);
            }

            if (showPlayAreaGizmo && hasPlayArea)
            {
                Gizmos.color = Color.yellow;

                Vector3 center = playAreaBounds.center;
                center.x += gizmoPositionOffset.x;
                center.y += gizmoPositionOffset.y;

                Vector3 size = playAreaBounds.size;
                size.x += gizmoSizeOffset.x;
                size.y += gizmoSizeOffset.y;

                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
