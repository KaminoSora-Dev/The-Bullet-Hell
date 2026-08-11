using UnityEngine;
using PlayerSystem;
using PoolingSystem;

namespace ItemSystem
{
    /// <summary>
    /// Item Drop กลางสำหรับ Life / Spell / Fragment
    ///
    /// ชนิดของ Item ถูกกำหนดจาก Tag ของ GameObject:
    /// - Life
    /// - Spell
    /// - Fragment
    ///
    /// ใช้ prefab เดียวกันได้ แต่แต่ละ prefab ควรมี Sprite / Tag ของตัวเอง
    ///
    /// Lifecycle:
    /// Spawn
    ///   ↓
    /// เคลื่อนที่ลงตาม moveSpeed หรืออยู่นิ่ง
    ///   ↓
    /// ครบ lifetime
    ///   ↓
    /// Fade Out
    ///   ↓
    /// Return เข้า Pool
    ///
    /// หมายเหตุ:
    /// ไม่ใช้ Destroy() เพราะระบบเกมใช้ Object Pooling
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ItemDropPickup : MonoBehaviour, IPoolable
    {
        public enum MovementMode
        {
            Stationary,
            Falling
        }

        [Header("Item")]
        [Tooltip(
            "ชนิด Item จะตรวจจาก Tag ของ GameObject โดยตรง:\n" +
            "Life / Spell / Fragment"
        )]
        [SerializeField] private int itemValue = 1;

        [Header("Movement")]
        [SerializeField] private MovementMode movementMode = MovementMode.Stationary;

        [Tooltip("ความเร็วในการเคลื่อนที่ลงด้านล่างของจอ")]
        [SerializeField] private float fallSpeed = 1.5f;

        [Header("Lifetime")]
        [Tooltip("เวลาที่ Item อยู่ในฉากก่อนเริ่ม Fade Out")]
        [SerializeField] private float lifetime = 10f;

        [Tooltip("ระยะเวลา Fade Out ก่อน Return เข้า Pool")]
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("References")]
        [SerializeField] private SpriteRenderer itemSprite;

        private Rigidbody2D rb;

        private float lifetimeTimer;
        private float fadeTimer;

        private bool isFading;
        private bool hasBeenCollected;

        private Color originalColor;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // Kinematic เพราะเราเป็นคนควบคุมตำแหน่งเอง
            // ไม่ต้องการ Gravity / Physics Force
            rb.bodyType = RigidbodyType2D.Kinematic;

            Collider2D pickupCollider = GetComponent<Collider2D>();
            pickupCollider.isTrigger = true;

            if (itemSprite == null)
                itemSprite = GetComponent<SpriteRenderer>();

            if (itemSprite != null)
                originalColor = itemSprite.color;
        }

        public void OnSpawnFromPool()
        {
            lifetimeTimer = 0f;
            fadeTimer = 0f;

            isFading = false;
            hasBeenCollected = false;

            if (itemSprite != null)
                itemSprite.color = originalColor;

            Collider2D pickupCollider = GetComponent<Collider2D>();
            if (pickupCollider != null)
                pickupCollider.enabled = true;

            // Sync Rigidbody กับตำแหน่งใหม่ทุกครั้งที่ถูกดึงจาก Pool
            rb.position = transform.position;
        }

        public void OnReturnToPool()
        {
            // ไม่มี state ที่ต้องทำตอน Return
            // เพราะทุกอย่าง reset ใน OnSpawnFromPool()
        }

        private void Update()
        {
            if (hasBeenCollected)
                return;

            if (!isFading)
            {
                UpdateMovement();
                UpdateLifetime();
            }
            else
            {
                UpdateFade();
            }
        }

        private void UpdateMovement()
        {
            if (movementMode != MovementMode.Falling)
                return;

            // เคลื่อนลงตามแกน Y ของ World
            // ใช้ MovePosition เพื่อให้ Rigidbody2D sync กับตำแหน่ง
            Vector2 nextPosition =
                rb.position + Vector2.down * fallSpeed * Time.deltaTime;

            rb.MovePosition(nextPosition);
        }

        private void UpdateLifetime()
        {
            lifetimeTimer += Time.deltaTime;

            if (lifetimeTimer >= lifetime)
            {
                BeginFade();
            }
        }

        private void BeginFade()
        {
            if (isFading)
                return;

            isFading = true;
            fadeTimer = 0f;

            // ระหว่าง Fade ไม่ควรเก็บได้อีก
            Collider2D pickupCollider = GetComponent<Collider2D>();
            if (pickupCollider != null)
                pickupCollider.enabled = false;

            // กรณี fadeDuration <= 0 ให้หายทันที
            if (fadeDuration <= 0f)
            {
                ReturnToPool();
            }
        }

        private void UpdateFade()
        {
            fadeTimer += Time.deltaTime;

            float t = Mathf.Clamp01(
                fadeTimer / Mathf.Max(fadeDuration, 0.0001f)
            );

            if (itemSprite != null)
            {
                Color color = itemSprite.color;
                color.a = Mathf.Lerp(originalColor.a, 0f, t);
                itemSprite.color = color;
            }

            if (t >= 1f)
            {
                ReturnToPool();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasBeenCollected || isFading)
                return;

            if (!other.CompareTag("Player"))
                return;

            Collect();
        }

        private void Collect()
        {
            if (hasBeenCollected)
                return;

            hasBeenCollected = true;

            PlayerStatsManager stats = PlayerStatsManager.Instance;

            if (stats != null)
            {
                if (CompareTag("Life"))
                {
                    stats.AddLife(itemValue);
                }
                else if (CompareTag("Spell"))
                {
                    stats.AddBomb(itemValue);
                }
                else if (CompareTag("Fragment"))
                {
                    stats.AddFragment(itemValue);
                }
                else
                {
                    Debug.LogWarning(
                        $"ItemDropPickup on '{gameObject.name}' has an unsupported Tag: {tag}",
                        this
                    );
                }
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Return(gameObject);
            }
            else
            {
                Debug.LogWarning(
                    "ItemDropPickup: PoolManager.Instance is null.",
                    this
                );
            }
        }
    }
}
