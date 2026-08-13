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

    [Header("Power Shooting (เพิ่มใหม่)")]
    [Tooltip("Prefab กระสุนหลักเวอร์ชั่นอัพเกรด - ใช้แทน bulletPrefab เฉพาะตอน Power Stage 5 " +
             "(ทั้งกระสุนตรงและกระสุนเฉียง) ปล่อยว่างได้ถ้าไม่อยากเปลี่ยนกระสุนหลัก จะใช้ bulletPrefab เดิมตลอด")]
    [SerializeField] private GameObject bulletPrefabPowerUp;
    [Tooltip("ระยะห่างระหว่างกระสุน 2 นัดที่ยิงตรงคู่กัน (Power Stage 1, 3, 4, 5)")]
    [SerializeField] private float bulletSpacing = 0.15f;
    [Tooltip("ระยะห่างแนวนอนของจุดยิงกระสุนเฉียง (Power Stage 2 ขึ้นไป) จาก firePoint")]
    [SerializeField] private float angledSpawnOffset = 0.12f;
    [Tooltip("มุมเฉียงของกระสุนซ้าย/ขวา (องศา) (Power Stage 2 ขึ้นไป)")]
    [SerializeField] private float bulletAngleSpread = 12f;
    [Tooltip("ระยะห่างแนวนอนของจุดยิงกระสุน Homing (Power Stage 4-5) จาก firePoint")]
    [SerializeField] private float homingSpawnOffset = 0.35f;
    [Tooltip("Prefab กระสุน Homing พื้นฐาน - ใช้ตอน Power Stage 4")]
    [SerializeField] private GameObject homingBulletPrefab;
    [Tooltip("Prefab กระสุน Homing เวอร์ชั่นอัพเกรด - ใช้ตอน Power Stage 5 แทน homingBulletPrefab")]
    [SerializeField] private GameObject homingBulletPrefabUpgraded;

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

    /// <summary>
    /// เพิ่มใหม่: ให้ PlayerAnimationController รับช่วง Death Visual แทนได้
    /// (เหมือน SuppressBuiltInDeathVisual ใน EnemyV1 / BossV2)
    ///
    /// ติ๊ก/set เป็น true แล้ว จะแค่ "ไม่เปลี่ยนสี/อัลฟ่า" ของ bodySprite เองในช่วง Blink และ Fade เท่านั้น
    /// จังหวะเวลาของ death sequence (กระพริบกี่ครั้ง, fade out/in นานแค่ไหน, เวลา respawn) ยังทำงานเหมือนเดิม
    /// ทุกอย่าง เพราะผูกกับ Logic การเสียชีวิต/ฟื้นคืนชีพจริง ไม่ใช่แค่ภาพ
    /// </summary>
    public bool SuppressBuiltInDeathVisual { get; set; }

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

    // ============================================================
    // SHOOTING - Power Stage 0-5
    // ============================================================
    //
    // Power mapping:
    // 0 = 1 straight
    // 1 = 2 straight
    // 2 = 3 total: 1 straight + 2 angled
    // 3 = 4 total: 2 straight + 2 angled
    // 4 = 4 main + 2 homing
    // 5 = 4 upgraded main + 2 upgraded homing
    //
    // IMPORTANT:
    // CurrentPowerStage is expected to be exactly 0-5.
    // This method does not shift the value by +1/-1.

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null)
            return;

        int powerStage = PlayerStatsManager.Instance != null
            ? Mathf.Clamp(PlayerStatsManager.Instance.CurrentPowerStage, 0, 5)
            : 0;

        // Power 5 only: replace the normal main bullet with the upgraded prefab.
        // If the upgraded prefab is not assigned, safely fall back to the normal one.
        GameObject mainPrefabToUse =
            powerStage == 5 && bulletPrefabPowerUp != null
                ? bulletPrefabPowerUp
                : bulletPrefab;

        switch (powerStage)
        {
            case 0:
                // 1 bullet: straight through the center.
                FireStraight(mainPrefabToUse, firePoint.position);
                break;

            case 1:
                // 2 bullets: both straight, separated horizontally.
                FireStraight(
                    mainPrefabToUse,
                    firePoint.position + Vector3.left * (bulletSpacing * 0.5f));

                FireStraight(
                    mainPrefabToUse,
                    firePoint.position + Vector3.right * (bulletSpacing * 0.5f));
                break;

            case 2:
                // 3 bullets: 1 center + 2 slightly angled.
                FireStraight(mainPrefabToUse, firePoint.position);

                FireAngled(
                    mainPrefabToUse,
                    firePoint.position + Vector3.left * angledSpawnOffset,
                    -bulletAngleSpread);

                FireAngled(
                    mainPrefabToUse,
                    firePoint.position + Vector3.right * angledSpawnOffset,
                    bulletAngleSpread);
                break;

            case 3:
            case 4:
            case 5:
                // 4 main bullets:
                // 2 straight + 2 slightly angled.
                FireStraight(
                    mainPrefabToUse,
                    firePoint.position + Vector3.left * (bulletSpacing * 0.5f));

                FireStraight(
                    mainPrefabToUse,
                    firePoint.position + Vector3.right * (bulletSpacing * 0.5f));

                FireAngled(
                    mainPrefabToUse,
                    firePoint.position + Vector3.left * angledSpawnOffset,
                    -bulletAngleSpread);

                FireAngled(
                    mainPrefabToUse,
                    firePoint.position + Vector3.right * angledSpawnOffset,
                    bulletAngleSpread);
                break;
        }

        // Power 4-5 add exactly 2 homing bullets.
        if (powerStage >= 4)
        {
            GameObject homingPrefabToUse =
                powerStage == 5 && homingBulletPrefabUpgraded != null
                    ? homingBulletPrefabUpgraded
                    : homingBulletPrefab;

            FireHoming(
                firePoint.position + Vector3.left * homingSpawnOffset,
                homingPrefabToUse,
                Vector2.up);

            FireHoming(
                firePoint.position + Vector3.right * homingSpawnOffset,
                homingPrefabToUse,
                Vector2.up);
        }

        OnShoot?.Invoke();
    }

    private void FireStraight(GameObject prefab, Vector3 spawnPosition)
    {
        if (prefab == null || PoolManager.Instance == null)
            return;

        GameObject bulletObj =
            PoolManager.Instance.Get(prefab, spawnPosition, firePoint.rotation);

        if (bulletObj != null &&
            bulletObj.TryGetComponent<PlayerBulletV1>(out var bullet))
        {
            bullet.SetDirection(Vector2.up);
        }
    }

    private void FireAngled(GameObject prefab, Vector3 spawnPosition, float angleDegrees)
    {
        if (prefab == null || PoolManager.Instance == null)
            return;

        GameObject bulletObj =
            PoolManager.Instance.Get(prefab, spawnPosition, firePoint.rotation);

        if (bulletObj != null &&
            bulletObj.TryGetComponent<PlayerBulletV1>(out var bullet))
        {
            Vector2 angledDirection =
                Quaternion.Euler(0f, 0f, angleDegrees) * Vector2.up;

            bullet.SetDirection(angledDirection);
        }
    }

    /// <summary>
    /// Spawn one homing bullet from the requested side position.
    /// The homing bullet acquires its target when it leaves the pool.
    /// </summary>
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
            bulletObj.TryGetComponent<PlayerBulletHomingV1>(out var homing))
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

    private void Bomb()
    {
        // เพิ่มใหม่: ห้ามกด Spell ซ้ำระหว่างที่โล่ตัวเดิมยังทำงานอยู่ (กันกดรัว)
        // เช็คก่อน UseSpell() เพื่อไม่ให้เสีย Resource ไปฟรีๆ ตอนโดนบล็อก
        if (shield != null && shield.IsActive)
            return;

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

        // เพิ่มใหม่: โล่ (Spell) กันตัวจาก Enemy/กระสุนที่หลุดเข้ามาด้วย ไม่ใช่กันแค่ EnemyBullet
        // ที่ชนโล่โดยตรงแบบเดิม (ดู PlayerShieldV1.IsActive)
        if (shield != null && shield.IsActive) return;

        currentState = PlayerState.Dead;

        // เพิ่มใหม่: reset ก่อน invoke เผื่อ PlayerAnimationController ตั้งค่านี้ค้างไว้จากรอบก่อน
        SuppressBuiltInDeathVisual = false;

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

        // เพิ่มใหม่: SuppressBuiltInDeathVisual = true -> ไม่ยุ่งสีของ bodySprite เอง ปล่อยให้ Animator คุมภาพแทน
        // (จังหวะ/เวลาด้านล่างยังทำงานเหมือนเดิมทุกอย่าง เพราะ respawn logic ผูกอยู่กับมัน)
        if (!SuppressBuiltInDeathVisual && bodySprite != null)
            bodySprite.color = blinkIsColored ? blinkColor : originalColor;

        if (!blinkIsColored)
        {
            blinksDone++;
            if (blinksDone >= blinkCount)
            {
                deathPhase = DeathVisualPhase.FadingOut;
                phaseTimer = 0f;
                if (!SuppressBuiltInDeathVisual && bodySprite != null)
                    bodySprite.color = originalColor;
            }
        }
    }

    private void UpdateFadeOut()
    {
        float t = Mathf.Clamp01(phaseTimer / fadeOutDuration);

        if (!SuppressBuiltInDeathVisual) // เพิ่มใหม่
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

        if (!SuppressBuiltInDeathVisual) // เพิ่มใหม่
            SetAlpha(t);

        if (t >= 1f)
        {
            if (!SuppressBuiltInDeathVisual) // เพิ่มใหม่
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