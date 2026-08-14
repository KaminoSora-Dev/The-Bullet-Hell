using System;
using UnityEngine;
using PoolingSystem;
using GameFlowSystem;

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

    public class PlayerControllerV1 : MonoBehaviour, IPlayerRespawnable
    {
        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 10f;

        [Header("Power Shooting")]
        [SerializeField] private GameObject bulletPrefabPowerUp;
        [SerializeField] private float bulletSpacing = 0.15f;
        [SerializeField] private float angledSpawnOffset = 0.12f;
        [SerializeField] private float bulletAngleSpread = 12f;
        [SerializeField] private float homingSpawnOffset = 0.35f;
        [SerializeField] private GameObject homingBulletPrefab;
        [SerializeField] private GameObject homingBulletPrefabUpgraded;

private float nextFireTime;
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

        [Header("Spell")]
        [SerializeField] private PlayerShieldV1 shield;
        [SerializeField] private float spellClearRadius = 5f;
        [SerializeField] private float shieldDuration = 2f;

        [Header("SFX (Optional)")]
        [SerializeField] private AudioSource shootSfx;
        [SerializeField] private AudioSource spellSfx;
        [SerializeField] private AudioSource deathSfx;


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
        private Vector3 lastDeathPosition;

        private bool isInvincible;
        private float invincibleTimer;
        private float invincibleBlinkTimer;
        private bool invincibleBlinkOn;

        private Bounds playAreaBounds;
        private bool hasPlayArea;

        public Vector2 CurrentMoveInput => moveInput;
        public bool IsSlowMode => isSlowMode;

        public event Action OnDied;
        public event Action OnSpellUsed;
        public event Action OnRespawned;

        public bool SuppressBuiltInDeathVisual { get; set; }

        private void Awake()
        {
            if (bodySprite != null)
                originalColor = bodySprite.color;
        }

        private void Start()
        {
            CachePlayAreaBounds();
        }

        private void CachePlayAreaBounds()
        {
            GameObject[] borderObjs = GameObject.FindGameObjectsWithTag("UIBorder");

            if (borderObjs.Length == 0)
            {
                Debug.LogWarning(
                    "PlayerControllerV1: ไม่พบ GameObject ที่มี tag \"UIBorder\" ในฉาก - ผู้เล่นจะเดินออกนอกจอได้"
                );
                return;
            }

            Bounds combined = default;
            bool boundsStarted = false;

            foreach (GameObject obj in borderObjs)
            {
                if (!obj.TryGetComponent<Collider2D>(out var col))
                    continue;

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
                Debug.LogWarning(
                    "PlayerControllerV1: เจอ GameObject tag \"UIBorder\" แต่ไม่มีตัวไหนมี Collider2D ติดอยู่เลย - อ่านขอบเขตไม่ได้"
                );
                return;
            }

            playAreaBounds = combined;
            hasPlayArea = true;
        }

        private void Update()
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

            if (Input.GetKey(KeyCode.LeftArrow))
                h -= 1f;

            if (Input.GetKey(KeyCode.RightArrow))
                h += 1f;

            if (Input.GetKey(KeyCode.UpArrow))
                v += 1f;

            if (Input.GetKey(KeyCode.DownArrow))
                v -= 1f;

            moveInput = new Vector2(h, v).normalized;

            isSlowMode =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            if (hitboxVisual != null)
                hitboxVisual.enabled = isSlowMode;
        }

        private void HandleActionInput()
        {
            // ช่วง Boss Defeat Delay ล็อกเฉพาะ Action
            // แต่ยังอนุญาตให้ผู้เล่นขยับได้ตามระบบเดิม
            bool actionLocked =
                GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsPlayerActionLocked;

            if (actionLocked)
                return;

            if (Input.GetKey(KeyCode.Z) && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + (1f / fireRate);
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                Spell();
            }
        }

        private void Shoot()
        {
            if (bulletPrefab == null || firePoint == null)
                return;

            int powerStage = PlayerStatsManager.Instance != null
                ? Mathf.Clamp(PlayerStatsManager.Instance.CurrentPowerStage, 0, 5)
                : 0;

            GameObject mainPrefabToUse =
                powerStage == 5 && bulletPrefabPowerUp != null
                    ? bulletPrefabPowerUp
                    : bulletPrefab;

            switch (powerStage)
            {
                case 0:
                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position);
                    break;

                case 1:
                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.left * (bulletSpacing * 0.5f));

                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.right * (bulletSpacing * 0.5f));
                    break;

                case 2:
                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position);

                    FireAngled(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.left * angledSpawnOffset,
                        -bulletAngleSpread);

                    FireAngled(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.right * angledSpawnOffset,
                        bulletAngleSpread);
                    break;

                case 3:
                case 4:
                case 5:
                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.left * (bulletSpacing * 0.5f));

                    FireStraight(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.right * (bulletSpacing * 0.5f));

                    FireAngled(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.left * angledSpawnOffset,
                        -bulletAngleSpread);

                    FireAngled(
                        mainPrefabToUse,
                        firePoint.position +
                        Vector3.right * angledSpawnOffset,
                        bulletAngleSpread);
                    break;
            }

            if (powerStage >= 4)
            {
                GameObject homingPrefabToUse =
                    powerStage == 5 && homingBulletPrefabUpgraded != null
                        ? homingBulletPrefabUpgraded
                        : homingBulletPrefab;

                FireHoming(
                    firePoint.position +
                    Vector3.left * homingSpawnOffset,
                    homingPrefabToUse,
                    Vector2.up);

                FireHoming(
                    firePoint.position +
                    Vector3.right * homingSpawnOffset,
                    homingPrefabToUse,
                    Vector2.up);
            }

            if (shootSfx != null && shootSfx.clip != null)
                shootSfx.PlayOneShot(shootSfx.clip);
        }

        private void FireStraight(
            GameObject prefab,
            Vector3 spawnPosition)
        {
            if (prefab == null || PoolManager.Instance == null)
                return;

            GameObject bulletObj =
                PoolManager.Instance.Get(
                    prefab,
                    spawnPosition,
                    firePoint.rotation);

            if (bulletObj != null &&
                bulletObj.TryGetComponent<PlayerBulletV1>(
                    out var bullet))
            {
                bullet.SetDirection(Vector2.up);
            }
        }

        private void FireAngled(
            GameObject prefab,
            Vector3 spawnPosition,
            float angleDegrees)
        {
            if (prefab == null || PoolManager.Instance == null)
                return;

            GameObject bulletObj =
                PoolManager.Instance.Get(
                    prefab,
                    spawnPosition,
                    firePoint.rotation);

            if (bulletObj != null &&
                bulletObj.TryGetComponent<PlayerBulletV1>(
                    out var bullet))
            {
                Vector2 angledDirection =
                    Quaternion.Euler(
                        0f,
                        0f,
                        angleDegrees) * Vector2.up;

                bullet.SetDirection(angledDirection);
            }
        }

        private void FireHoming(
            Vector3 spawnPosition,
            GameObject homingPrefab,
            Vector2 initialDirection)
        {
            if (homingPrefab == null)
            {
                Debug.LogError(
                    "PlayerControllerV1: Homing Bullet Prefab is missing. " +
                    "Assign homingBulletPrefab for Power 4 and " +
                    "homingBulletPrefabUpgraded for Power 5.");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogError(
                    "PlayerControllerV1: PoolManager.Instance is null. " +
                    "Cannot spawn Homing Bullet.");
                return;
            }

            GameObject bulletObj =
                PoolManager.Instance.Get(
                    homingPrefab,
                    spawnPosition,
                    firePoint.rotation);

            if (bulletObj != null &&
                bulletObj.TryGetComponent<PlayerBulletHomingV1>(
                    out var homing))
            {
                homing.SetInitialDirection(initialDirection);
            }
            else if (bulletObj != null)
            {
                Debug.LogError(
                    $"PlayerControllerV1: Prefab '{homingPrefab.name}' " +
                    "does not contain PlayerBulletHomingV1.");
            }
        }

        private void ApplyMovement()
        {
            float speed = isSlowMode ? slowMoveSpeed : moveSpeed;

            Vector3 nextPosition =
                transform.position +
                (Vector3)(moveInput * speed * Time.deltaTime);

            if (hasPlayArea)
            {
                nextPosition.x = Mathf.Clamp(
                    nextPosition.x,
                    playAreaBounds.min.x + borderPadding,
                    playAreaBounds.max.x - borderPadding
                );

                nextPosition.y = Mathf.Clamp(
                    nextPosition.y,
                    playAreaBounds.min.y + borderPadding,
                    playAreaBounds.max.y - borderPadding
                );
            }

            transform.position = nextPosition;
        }

        private void Spell()
        {
            if (shield != null && shield.IsActive)
                return;

            if (PlayerStatsManager.Instance != null &&
                !PlayerStatsManager.Instance.UseSpell())
            {
                return;
            }

            ClearBulletsInRadius(
                transform.position,
                spellClearRadius
            );

            if (shield != null)
            {
                shield.Activate(shieldDuration);
            }
            else
            {
                Debug.LogWarning(
                    "PlayerControllerV1: ไม่ได้ลาก PlayerShieldV1 ไว้ใน field \"shield\" - ใช้ Spell ได้ แต่จะไม่มีโล่ขึ้น"
                );
            }

            if (spellSfx != null && spellSfx.clip != null)
                spellSfx.PlayOneShot(spellSfx.clip);

            OnSpellUsed?.Invoke();
        }

        private void ClearBulletsInRadius(Vector2 center, float radius)
        {
            Collider2D[] hits =
                Physics2D.OverlapCircleAll(center, radius);

            foreach (Collider2D hit in hits)
            {
                if (!hit.CompareTag("EnemyBullet"))
                    continue;

                if (hit.TryGetComponent<EnemySystem.EnemyBulletV2>(
                        out var bulletV2))
                {
                    bulletV2.RequestDestroy();
                }
                else if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Return(hit.gameObject);
                }
            }
        }

        public void Die()
        {
            if (currentState == PlayerState.Dead)
                return;

            if (isInvincible)
                return;

            if (shield != null && shield.IsActive)
                return;

            currentState = PlayerState.Dead;

            lastDeathPosition = transform.position;

            SuppressBuiltInDeathVisual = false;

            if (deathSfx != null && deathSfx.clip != null)
                deathSfx.PlayOneShot(deathSfx.clip);

            OnDied?.Invoke();

            if (hitboxCollider != null)
                hitboxCollider.enabled = false;

            if (hitboxVisual != null)
                hitboxVisual.enabled = false;

            if (PlayerStatsManager.Instance != null)
            {
                canRespawnAfterThisDeath =
                    PlayerStatsManager.Instance.LoseLife();
            }
            else
            {
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
            if (phaseTimer < blinkInterval)
                return;

            phaseTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (!SuppressBuiltInDeathVisual &&
                bodySprite != null)
            {
                bodySprite.color =
                    blinkIsColored
                        ? blinkColor
                        : originalColor;
            }

            if (!blinkIsColored)
            {
                blinksDone++;

                if (blinksDone >= blinkCount)
                {
                    deathPhase = DeathVisualPhase.FadingOut;
                    phaseTimer = 0f;

                    if (!SuppressBuiltInDeathVisual &&
                        bodySprite != null)
                    {
                        bodySprite.color = originalColor;
                    }
                }
            }
        }

        private void UpdateFadeOut()
        {
            float t =
                Mathf.Clamp01(
                    phaseTimer / fadeOutDuration
                );

            if (!SuppressBuiltInDeathVisual)
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

                    Debug.Log(
                        "PlayerControllerV1: Game Over"
                    );
                }
            }
        }

        private void UpdateFadeIn()
        {
            float t =
                Mathf.Clamp01(
                    phaseTimer / fadeInDuration
                );

            if (!SuppressBuiltInDeathVisual)
                SetAlpha(t);

            if (t >= 1f)
            {
                if (!SuppressBuiltInDeathVisual)
                    SetAlpha(1f);

                if (hitboxCollider != null)
                    hitboxCollider.enabled = true;

                deathPhase = DeathVisualPhase.None;
                currentState = PlayerState.Alive;

                StartInvincibility();

                OnRespawned?.Invoke();
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
                {
                    bodySprite.color =
                        invincibleBlinkOn
                            ? invincibleColor
                            : originalColor;
                }
            }

            if (invincibleTimer >= invincibleDuration)
                EndInvincibility();
        }

        private void EndInvincibility()
        {
            isInvincible = false;

            if (bodySprite != null)
                bodySprite.color = originalColor;
        }

        private void SetAlpha(float alpha)
        {
            if (bodySprite == null)
                return;

            Color c = bodySprite.color;
            c.a = alpha;
            bodySprite.color = c;
        }

        private void RespawnPosition()
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning(
                    "PlayerControllerV1: ไม่ได้กำหนด spawnPoint ไว้ - จะไม่ย้ายตำแหน่งตอน respawn"
                );
                return;
            }

            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        public void Respawn()
        {
            if (spawnPoint == null)
            {
                Debug.LogWarning(
                    "PlayerControllerV1: Continue Respawn ไม่ได้ เพราะยังไม่ได้กำหนด spawnPoint"
                );
                return;
            }

            RespawnPosition();

            currentState = PlayerState.Dead;

            if (hitboxCollider != null)
                hitboxCollider.enabled = false;

            if (!SuppressBuiltInDeathVisual)
                SetAlpha(0f);

            deathPhase = DeathVisualPhase.FadingIn;
            phaseTimer = 0f;

            Debug.Log(
                $"PlayerControllerV1: Continue - Respawn ที่ตำแหน่งตายล่าสุด {lastDeathPosition} " +
                "(จะ Fade เข้าและได้ Invincibility เหมือน Respawn ปกติ)"
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (hitboxCollider != null)
            {
                Gizmos.color = Color.red;

                Gizmos.DrawWireSphere(
                    transform.position +
                    (Vector3)hitboxCollider.offset,
                    hitboxCollider.radius
                );
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

                Gizmos.DrawWireCube(
                    center,
                    size
                );
            }
        }
    }
}