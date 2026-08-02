using UnityEngine;

namespace PlayerSystem
{
    /// <summary>
    /// สถานะหลักของผู้เล่น - ใช้คุมว่ารับ input ได้หรือไม่
    /// </summary>
    public enum PlayerState
    {
        Alive,
        Dead
    }

    /// <summary>
    /// ขั้นตอนย่อยระหว่างตาย - แยกออกจาก PlayerState เพราะเป็นเรื่อง "ภาพ" ล้วนๆ
    /// ไม่เกี่ยวกับ gameplay logic (PlayerState.Dead พอสำหรับบล็อค input)
    /// </summary>
    public enum DeathVisualPhase
    {
        None,
        Blinking,   // กะพริบสีแดง 3 ครั้ง
        FadingOut,  // จางหายไป
        FadingIn    // จางกลับมาหลัง respawn
    }

    public class PlayerControllerV1 : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float slowMoveSpeed = 2f; // ความเร็วตอนกด Shift (Focus/Slow mode)

        [Header("Respawn")]
        [Tooltip("ลาก Transform ของจุด Spawn (Empty GameObject ที่วางไว้ใน Scene) มาใส่")]
        [SerializeField] private Transform spawnPoint;

        [Header("Death Visual - Blink")]
        [SerializeField] private int blinkCount = 3;           // กะพริบกี่ครั้งก่อนตาย
        [SerializeField] private float blinkInterval = 0.1f;   // สลับสีทุกกี่วินาที
        [SerializeField] private Color blinkColor = Color.red;

        [Header("Death Visual - Fade")]
        [SerializeField] private float fadeOutDuration = 0.4f;
        [SerializeField] private float fadeInDuration = 0.4f;

        [Header("Invincibility (อมตะหลัง Respawn)")]
        [Tooltip("ระยะเวลาที่ผู้เล่นอมตะหลังเกิดใหม่ (วินาที) - ตั้งเป็น 0 ถ้าไม่อยากให้มีช่วงอมตะเลย")]
        [SerializeField] private float invincibleDuration = 2f;
        [Tooltip("ความถี่การกะพริบสีฟ้าระหว่างอมตะ (วินาที)")]
        [SerializeField] private float invincibleBlinkInterval = 0.1f;
        [Tooltip("สีที่ใช้กะพริบระหว่างอมตะ (ค่าเริ่มต้น = ฟ้าอ่อน)")]
        [SerializeField] private Color invincibleColor = new Color(0.4f, 0.8f, 1f);

        [Header("Shooting")]
        [Tooltip("Prefab ของ PlayerBullet")]
        [SerializeField] private GameObject bulletPrefab;
        [Tooltip("จุดที่กระสุนจะ spawn ออกมา (ควรอยู่หัวเรือ/หัวตัวละคร)")]
        [SerializeField] private Transform firePoint;
        [Tooltip("จำนวนนัดต่อวินาที ตอนกด Z ค้าง")]
        [SerializeField] private float fireRate = 10f;

        [Header("References")]
        [Tooltip("SpriteRenderer หลักของตัวละคร (ใช้กะพริบสี/fade alpha)")]
        [SerializeField] private SpriteRenderer bodySprite;
        [Tooltip("Collider จริงที่ใช้ตรวจจับกระสุนศัตรู ต้องติ๊ก isTrigger และตั้ง radius ให้เล็กกว่า sprite มาก")]
        [SerializeField] private CircleCollider2D hitboxCollider;
        [Tooltip("จุดเล็กๆ ที่โชว์ให้ผู้เล่นเห็นตำแหน่ง hitbox จริง (โชว์เฉพาะตอน Slow mode)")]
        [SerializeField] private SpriteRenderer hitboxVisual;

        [Header("Play Area Border")]
        [Tooltip("ระยะเผื่อจากขอบกล่อง UIBorder (world unit) - ใส่ 0 ถ้าอยากให้ hitbox ชนขอบพอดีเป๊ะ")]
        [SerializeField] private float borderPadding = 0.1f;

        private PlayerState currentState = PlayerState.Alive;

        private Vector2 moveInput;
        private bool isSlowMode;

        // ตัวแปรคุม death sequence ทั้งหมด - เก็บเป็น field เพราะต้องอ่านค่าต่อเนื่องข้าม Update หลายเฟรม
        private DeathVisualPhase deathPhase = DeathVisualPhase.None;
        private float phaseTimer;
        private int blinksDone;
        private bool blinkIsColored; // สลับ true/false ทุกครึ่ง interval เพื่อทำให้กะพริบ
        private Color originalColor;

        // เวลาที่ยิงนัดต่อไปได้ - ใช้ Time.time เทียบตรงๆ แทนการนับ timer เอง
        // เพราะ Time.time เป็นค่าสะสมของ Unity อยู่แล้ว ไม่ต้อง += เองทุกเฟรมให้เสี่ยง error สะสม
        private float nextFireTime;

        // สถานะอมตะ - แยก timer/blink ออกจากชุดของ death sequence เพราะทำงานตอน Alive
        // (death blink สีแดงทำงานตอน Dead เท่านั้น ไม่มีทางชนกัน)
        private bool isInvincible;
        private float invincibleTimer;
        private float invincibleBlinkTimer;
        private bool invincibleBlinkOn;

        // ขอบเขตที่ผู้เล่นเดินได้ - หามาครั้งเดียวตอน Start แล้ว cache ไว้ ไม่ต้องหาใหม่ทุกเฟรม
        // (เหมือน playerTransform cache ใน EnemyShooterV1 - หลักการเดียวกัน: หาของที่ไม่ขยับทุกเฟรมแค่ครั้งเดียวพอ)
        private Bounds playAreaBounds;
        private bool hasPlayArea;

        void Awake()
        {
            if (bodySprite != null)
                originalColor = bodySprite.color;
        }

        void Start()
        {
            CachePlayAreaBounds();
        }

        /// <summary>
        /// หา GameObject ทุกตัวที่ tag "UIBorder" ในฉาก แล้วรวม Bounds ของ Collider2D ทุกตัวเข้าด้วยกัน
        /// รองรับทั้งกรณีทำเป็นกล่องเดียวครอบพื้นที่เล่น หรือแยกเป็นกำแพง 4 ด้าน (Left/Right/Top/Bottom)
        /// ที่ tag เดียวกันหมด - ถ้าวางกำแพงให้ขอบในตรงกับพื้นที่เล่นจริง ผลรวม bounds จะเท่ากับกรอบพื้นที่เล่นพอดี
        /// ไม่ต้องพึ่ง physics trigger เลย เพราะแค่ต้องการ "กรอบตัวเลข" มา clamp ตำแหน่งเท่านั้น
        /// (ถ้ากล่อง UIBorder ขยับ/ปรับขนาดกลางเกม ต้องเรียกฟังก์ชันนี้ใหม่ - ตอนนี้ยังไม่รองรับ เพราะกรอบจอปกติควรอยู่นิ่ง)
        /// </summary>
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
                    // เริ่มจาก bounds ของชิ้นแรกที่เจอ Collider2D ก่อน แทนที่จะเริ่มจาก Bounds ว่างเปล่า (0,0,0)
                    // เพราะถ้าเริ่มจากค่าว่างแล้ว Encapsulate จะดึงจุด (0,0,0) เข้ามาปนด้วยเสมอ ทำให้ bounds เพี้ยน
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
                // ตายอยู่ -> ไม่รับ input เคลื่อนที่/ยิงเลย แต่ต้องอัปเดต visual sequence ต่อไป
                UpdateDeathSequence();
            }
        }

        private void ReadMovementInput()
        {
            // ปุ่มลูกศรตามที่ต้องการตอนนี้ ทีหลังจะสลับเป็น WASD ได้ง่ายๆ
            // แค่เปลี่ยนตรงนี้เป็นอ่านจากตัวแปร/Settings แทนการ hardcode KeyCode
            float h = 0f;
            float v = 0f;

            if (Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
            if (Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v -= 1f;

            // normalized กันเดินทแยงมุมเร็วกว่าเดินตรง (จาก Pythagoras: (1,1) ยาวกว่า (1,0))
            moveInput = new Vector2(h, v).normalized;

            isSlowMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // โชว์ hitbox visual เฉพาะตอน Slow mode ตาม convention บูลเลตเฮลทั่วไป
            if (hitboxVisual != null)
                hitboxVisual.enabled = isSlowMode;
        }

        private void HandleActionInput()
        {
            // Shoot ใช้ GetKey (ไม่ใช่ GetKeyDown) เพราะต้องการให้ยิงต่อเนื่องตอนกดค้าง
            // คุมอัตรายิงด้วย nextFireTime แทน ไม่งั้นจะยิงถี่ระดับเฟรมเรท (60+ นัด/วิ) ซึ่งเร็วเกินไปและกิน performance
            if (Input.GetKey(KeyCode.Z) && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + (1f / fireRate);
            }

            // Bomb ใช้ GetKeyDown เพราะต้องการจับ "จังหวะกดครั้งแรก" เท่านั้น ไม่ใช่ทุกเฟรมที่กดค้าง
            if (Input.GetKeyDown(KeyCode.X))
            {
                Bomb();
            }
        }

        private void ApplyMovement()
        {
            float speed = isSlowMode ? slowMoveSpeed : moveSpeed;

            // ไม่มี Rigidbody2D แล้ว เลยแก้ transform.position ตรงๆ ผ่าน Time.deltaTime
            Vector3 nextPosition = transform.position + (Vector3)(moveInput * speed * Time.deltaTime);

            if (hasPlayArea)
            {
                // Clamp แยกแกน x และ y อิสระจากกัน - นี่คือจุดสำคัญที่ทำให้ได้ฟีลแบบ Touhou:
                // ชนขอบบน (y ถูกบีบ) แต่ยังกดซ้าย-ขวาไถลไปตามขอบได้ (x ไม่ถูกบีบ) ในเฟรมเดียวกัน
                nextPosition.x = Mathf.Clamp(nextPosition.x, playAreaBounds.min.x + borderPadding, playAreaBounds.max.x - borderPadding);
                nextPosition.y = Mathf.Clamp(nextPosition.y, playAreaBounds.min.y + borderPadding, playAreaBounds.max.y - borderPadding);
            }

            transform.position = nextPosition;
        }

        private void Shoot()
        {
            if (bulletPrefab == null || firePoint == null) return;

            // TODO: ตอนนี้ Instantiate ตรงๆ ก่อน ทีหลังพอทำ Object Pool แล้วให้เปลี่ยนเป็นดึงจาก pool แทน
            // เพราะกดยิงค้างรัวๆ ที่ fireRate สูง จะสร้าง/ทำลาย GameObject ถี่มาก กิน performance จาก GC หนักกว่ากระสุนศัตรูอีก
            GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

            if (bulletObj.TryGetComponent<PlayerBulletV1>(out var bullet))
            {
                bullet.SetDirection(Vector2.up);
            }
        }

        private void Bomb()
        {
            // TODO: เชื่อมกับระบบ Bomb ทีหลัง (เช่น ล้างกระสุนบนจอ + ป้องกันชั่วคราว)
            Debug.Log("Bomb!");
        }

        /// <summary>
        /// เรียกจากภายนอก เช่น EnemyBullet.cs ตอน OnTriggerEnter2D เจอผู้เล่น
        /// </summary>
        public void Die()
        {
            // กันเคสโดนกระสุนหลายลูกพร้อมกันในเฟรมเดียว หรือโดนซ้ำระหว่าง death sequence
            if (currentState == PlayerState.Dead) return;

            // อมตะอยู่ (ช่วงหลัง Respawn) - เพิกเฉยต่อการโดนโจมตีทุกแบบ ไม่ว่าจะเป็น EnemyBullet หรือชน Enemy ตัวเนื้อ
            // เช็คจุดเดียวตรงนี้พอ เพราะทั้ง EnemyBulletV1 และ EnemyV1 ต่างก็เรียก Die() ผ่านทางนี้เหมือนกันหมด
            if (isInvincible) return;

            currentState = PlayerState.Dead;

            // ปิด collider/hitbox visual ทันที กันโดนกระสุนซ้ำระหว่างตาย
            if (hitboxCollider != null) hitboxCollider.enabled = false;
            if (hitboxVisual != null) hitboxVisual.enabled = false;

            // เริ่ม death sequence จากขั้นกะพริบก่อนเสมอ
            deathPhase = DeathVisualPhase.Blinking;
            phaseTimer = 0f;
            blinksDone = 0;
            blinkIsColored = false;
        }

        /// <summary>
        /// ตัวขับเคลื่อน death sequence ทั้งหมด เรียกทุกเฟรมตอน currentState == Dead
        /// ไล่ผ่าน 3 phase ตามลำดับ: Blinking -> FadingOut -> (respawn ตำแหน่ง) -> FadingIn -> จบ
        /// </summary>
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

            // ครบ 1 interval แล้ว -> สลับสี และรีเซ็ต timer สำหรับรอบถัดไป
            phaseTimer = 0f;
            blinkIsColored = !blinkIsColored;

            if (bodySprite != null)
                bodySprite.color = blinkIsColored ? blinkColor : originalColor;

            // นับ 1 "ครั้ง" ตอนกลับมาสีปกติ (คือครบ 1 รอบ แดง->ปกติ)
            if (!blinkIsColored)
            {
                blinksDone++;
                if (blinksDone >= blinkCount)
                {
                    // กะพริบครบแล้ว -> เข้าสู่ phase จางหาย
                    deathPhase = DeathVisualPhase.FadingOut;
                    phaseTimer = 0f;
                    if (bodySprite != null)
                        bodySprite.color = originalColor; // กันสีค้างเป็นแดงตอนเริ่ม fade
                }
            }
        }

        private void UpdateFadeOut()
        {
            float t = Mathf.Clamp01(phaseTimer / fadeOutDuration);
            SetAlpha(1f - t);

            if (t >= 1f)
            {
                // จางหายสนิทแล้ว -> ย้ายไปจุด spawn แล้วเริ่ม fade กลับเข้ามา
                RespawnPosition();
                deathPhase = DeathVisualPhase.FadingIn;
                phaseTimer = 0f;
            }
        }

        private void UpdateFadeIn()
        {
            float t = Mathf.Clamp01(phaseTimer / fadeInDuration);
            SetAlpha(t);

            if (t >= 1f)
            {
                // จบ sequence ทั้งหมด -> คืนสิทธิ์ควบคุมให้ผู้เล่น
                SetAlpha(1f);
                if (hitboxCollider != null) hitboxCollider.enabled = true;

                deathPhase = DeathVisualPhase.None;
                currentState = PlayerState.Alive;

                // เกิดใหม่เสร็จ -> เริ่มช่วงอมตะทันที กันโดนซ้ำตอนเพิ่งคุมตัวได้
                StartInvincibility();
            }
        }

        private void StartInvincibility()
        {
            isInvincible = true;
            invincibleTimer = 0f;
            invincibleBlinkTimer = 0f;
            invincibleBlinkOn = false;
        }

        /// <summary>
        /// เรียกทุกเฟรมตอน Alive + isInvincible เท่านั้น (ดูใน Update())
        /// โครงเดียวกับ UpdateBlinking() ตอนตาย ต่างกันแค่สีที่สลับ และเงื่อนไขจบคือครบเวลา ไม่ใช่ครบจำนวนครั้ง
        /// </summary>
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

            // กันสีค้างเป็นฟ้าอยู่ ถ้า duration ดันหมดพอดีตอนกำลังกะพริบเป็นสีฟ้าอยู่
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
            // กันลืมลาก spawnPoint ใน Inspector - ไม่งั้นจะ error กลางเกมตอนตายครั้งแรกโดยไม่รู้สาเหตุ
            if (spawnPoint == null)
            {
                Debug.LogWarning("PlayerControllerV1: ไม่ได้กำหนด spawnPoint ไว้ - จะไม่ย้ายตำแหน่งตอน respawn");
                return;
            }

            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation;
        }

        // วาด hitbox ใน Scene view แม้ไม่ได้กด Play เพื่อเช็คขนาด/ตำแหน่งง่ายๆ ตอนจัดฉาก
        void OnDrawGizmosSelected()
        {
            if (hitboxCollider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position + (Vector3)hitboxCollider.offset, hitboxCollider.radius);
            }

            // วาดกรอบขอบเขตที่เล่นได้ (สีเหลือง) - เห็นเฉพาะตอนกด Play เพราะ playAreaBounds cache ตอน Start()
            // ใช้เช็คว่า UIBorder ที่หามาได้ตรงกับพื้นที่เล่นจริงไหม ถ้ากรอบเหลืองเพี้ยนไปคนละที่ = อ่าน bounds ผิดตัว/ผิดพิกัด
            if (hasPlayArea)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(playAreaBounds.center, playAreaBounds.size);
            }
        }
    }
}