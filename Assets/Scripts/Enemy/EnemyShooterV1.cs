using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;

namespace EnemySystem
{
    /// <summary>
    /// 1 ชุดการยิง - ยิง bulletCount นัดพร้อมกัน กระจายมุมตาม spreadAngle
    /// แล้วหน่วงเวลา delayAfter ก่อนไปชุดถัดไป
    /// </summary>
    [System.Serializable]
    public class ShootBurst
    {
        [Tooltip("Bullet prefab เฉพาะของชุดยิงนี้ - แต่ละ Burst เลือกกระสุนคนละแบบกันได้ " +
                 "เช่น Burst แรกยิงกระสุนกลมช้าๆ Burst ถัดไปยิงกระสุนเลเซอร์เร็ว โดยไม่ต้องแยก Group/Component")]
        public GameObject bulletPrefab;

        [Tooltip("จำนวนกระสุนที่ยิงพร้อมกันในชุดนี้")]
        public int bulletCount = 1;

        [Tooltip("มุมกระจายรวมทั้งหมด (องศา) - 0 = ยิงตรงทุกนัดไปทิศเดียวกัน, ใช้ตอนอยากได้ spread shot")]
        public float spreadAngle = 0f;

        [Tooltip("ติ๊กถ้าอยากเล็งไปทางผู้เล่น ณ ตอนยิง (คำนวณทิศใหม่ทุกครั้ง)")]
        public bool aimAtPlayer = false;

        [Tooltip("ทิศทางคงที่ที่จะยิง - ใช้เฉพาะตอนไม่ติ๊ก Aim At Player")]
        public Vector2 baseDirection = Vector2.down;

        [Tooltip("หน่วงเวลาหลังยิงชุดนี้ ก่อนไปชุดถัดไป (วินาที)")]
        public float delayAfter = 0.5f;
    }

    /// <summary>
    /// 1 กลุ่มการยิง - มี pattern (ลิสต์ ShootBurst) และ timeline ของตัวเองแยกอิสระจากกลุ่มอื่นโดยสิ้นเชิง
    /// ทำให้ Enemy ตัวเดียวยิงหลายรูปแบบพร้อมกันได้ในตัวเดียว (เช่น กลุ่ม "กระสุนหลัก" ยิงถี่ๆ ตรงลงล่าง
    /// พร้อมกับกลุ่ม "กระสุนล้อม" ที่ยิงช้ากว่าแบบ spread) โดยไม่ต้องเพิ่ม component ซ้อนหลายตัวบน GameObject เดียวกัน
    /// แนวคิด/โครงเดียวกับ SpawnGroup ใน StageSpawnerV1.cs
    /// </summary>
    [System.Serializable]
    public class ShootGroup
    {
        [Tooltip("ชื่อกลุ่ม - ใช้แค่ให้อ่านง่ายใน Inspector เท่านั้น ไม่มีผลต่อการทำงานจริง")]
        public string groupName = "New Group";

        [Tooltip("ลิสต์ชุดการยิงตามลำดับของกลุ่มนี้ - วนซ้ำได้ถ้าติ๊ก Loop Pattern")]
        public List<ShootBurst> pattern = new List<ShootBurst>();

        [Tooltip("วนกลับไปเล่น pattern ของกลุ่มนี้ซ้ำตลอด ถ้าไม่ติ๊ก จะยิงจบแล้วหยุดเฉพาะกลุ่มนี้ (กลุ่มอื่นยังเล่นต่อได้ปกติ)")]
        public bool loopPattern = true;

        // ---- runtime state ของกลุ่มนี้โดยเฉพาะ - ไม่โชว์ใน Inspector เพราะเป็นสถานะตอนรัน ไม่ใช่ค่าที่ผู้ออกแบบฉากต้องแก้ ----
        // แยก state ชุดนี้ไว้ "ต่อกลุ่ม" (ไม่ใช่ field เดี่ยวระดับ EnemyShooterV1 เหมือนเดิม) เพราะแต่ละกลุ่มต้องเดิน
        // burst index/timer เป็นอิสระจากกันเอง ถ้าใช้ field เดี่ยวร่วมกันทุกกลุ่มจะเดินพร้อมกันเป๊ะๆ เสมอ ยิงพร้อมกันตลอดไม่ได้ตามต้องการ
        [System.NonSerialized] public int currentBurstIndex;
        [System.NonSerialized] public float delayTimer;
        [System.NonSerialized] public bool hasFiredCurrentBurst;
        [System.NonSerialized] public bool patternFinished; // true เมื่อ pattern ของกลุ่มนี้จบแล้วและไม่ loop - หยุดยิงถาวรเฉพาะกลุ่มนี้

        /// <summary>รีเซ็ตกลุ่มนี้กลับไปเริ่มเล่นจาก Burst แรกใหม่ทั้งหมด</summary>
        public void ResetState()
        {
            currentBurstIndex = 0;
            delayTimer = 0f;
            hasFiredCurrentBurst = false;
            patternFinished = false;
        }
    }

    public class EnemyShooterV1 : MonoBehaviour
    {
        [Tooltip("จุดที่กระสุนจะ spawn ออกมา ถ้าไม่ใส่จะใช้ตำแหน่งตัว Enemy เอง - ใช้ร่วมกันทุกกลุ่มในตัวนี้")]
        [SerializeField] private Transform firePoint;

        [Tooltip("แต่ละกลุ่มคือชุดการยิงที่มี timeline อิสระของตัวเอง เล่นพร้อมกันได้ทุกกลุ่มโดยไม่ต้องเพิ่ม component ซ้ำ")]
        [SerializeField] private List<ShootGroup> groups = new List<ShootGroup>();

        private Transform playerTransform;

        // ถ้ามี EnemyV1 อยู่บน GameObject เดียวกัน จะถูก "คุม" การยิงตาม Movement Step ของมัน (ผ่าน CanShootNow/OnShootWindowOpened)
        // เป็น null ได้ปกติถ้า EnemyShooterV1 ตัวนี้ไม่มี EnemyV1 คู่กัน (เช่น ตัวยิงลอยๆ ไม่มี movement) - จะไม่ถูก gate เลย
        private EnemyV1 ownerEnemy;

        void Awake()
        {
            ownerEnemy = GetComponent<EnemyV1>();

            // subscribe ครั้งเดียวตอน Awake (ไม่ใช่ทุกครั้งที่ pool หยิบมาใช้ใหม่) กัน subscribe ซ้ำซ้อนทุกรอบ pool reuse
            // ส่วนการ "รีเซ็ตทุกกลุ่มตอน spawn" ทำแยกผ่าน ResetAllGroups() ที่ EnemyV1.OnSpawnFromPool() เรียกให้เองอยู่แล้ว
            if (ownerEnemy != null)
            {
                ownerEnemy.OnShootWindowOpened += ResetAllGroups;
            }
        }

        void OnDestroy()
        {
            if (ownerEnemy != null)
            {
                ownerEnemy.OnShootWindowOpened -= ResetAllGroups;
            }
        }

        void Start()
        {
            // Cache หา Player ไว้ตั้งแต่ต้น ไม่ต้อง FindGameObjectWithTag ซ้ำทุกครั้งที่ยิง
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;

            if (firePoint == null) firePoint = transform;
        }

        void Update()
        {
            if (groups.Count == 0) return;

            // ถูก EnemyV1 gate ไว้อยู่ (เช่น step ปัจจุบันไม่ติ๊ก Allow Shoot) - รอเฉยๆ ไม่ยิงเลยสักกลุ่ม
            // เกทเดียวคุมทุกกลุ่มพร้อมกัน (ไม่ได้ออกแบบให้บาง group ยิงได้ระหว่างอีก group ถูก gate - ถ้าต้องการแบบนั้นจริงๆ
            // ค่อยแยกเป็นคนละ EnemyShooterV1 component ไปเลย เพราะ CanShootNow เป็นสถานะของ Enemy ตัวเดียวทั้งตัว)
            if (ownerEnemy != null && !ownerEnemy.CanShootNow) return;

            // ไล่อัปเดตทุกกลุ่มพร้อมกันทุกเฟรม - แต่ละกลุ่มมี state (currentBurstIndex/delayTimer ฯลฯ) เป็นของตัวเอง
            // จึงเดินหน้าอิสระจากกันสนิท ยิงพร้อมกันในเฟรมเดียวกันได้โดยไม่ชนกัน
            for (int g = 0; g < groups.Count; g++)
            {
                UpdateGroup(groups[g]);
            }
        }

        private void UpdateGroup(ShootGroup group)
        {
            if (group.pattern.Count == 0 || group.patternFinished) return;

            ShootBurst burst = group.pattern[group.currentBurstIndex];

            // ยิงทันทีตอนเพิ่งเข้าคิวชุดนี้ (ยังไม่เคยยิง) แล้วค่อยเริ่มนับเวลาหน่วง
            if (!group.hasFiredCurrentBurst)
            {
                FireBurst(burst);
                group.hasFiredCurrentBurst = true;
                group.delayTimer = 0f;
            }

            group.delayTimer += Time.deltaTime;
            if (group.delayTimer >= burst.delayAfter)
            {
                AdvanceToNextBurst(group);
            }
        }

        private void AdvanceToNextBurst(ShootGroup group)
        {
            group.currentBurstIndex++;
            group.hasFiredCurrentBurst = false;

            if (group.currentBurstIndex >= group.pattern.Count)
            {
                if (group.loopPattern)
                {
                    group.currentBurstIndex = 0;
                }
                else
                {
                    group.patternFinished = true;
                }
            }
        }

        /// <summary>
        /// รีเซ็ตทุกกลุ่มกลับไปเริ่มเล่นจาก Burst แรกใหม่ทั้งหมด เรียกได้จาก 2 ทาง:
        /// 1. EnemyV1.OnSpawnFromPool() - ทุกครั้งที่ Enemy ถูกดึงจาก pool มาใช้ใหม่
        /// 2. EnemyV1.OnShootWindowOpened - ตอน Movement Step เปลี่ยนจาก "ห้ามยิง" เป็น "ยิงได้" (rising edge)
        /// </summary>
        public void ResetAllGroups()
        {
            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].ResetState();
            }
        }

        private void FireBurst(ShootBurst burst)
        {
            // กันลืมตั้ง bulletPrefab ไว้ในบาง Burst - ข้ามการยิงชุดนี้ไปเฉยๆ แทนที่จะ error
            // (ต่างจาก firePoint ที่ error ได้เลยเพราะมี fallback เป็น transform ตัวเองอยู่แล้วใน Start())
            if (burst.bulletPrefab == null)
            {
                Debug.LogWarning($"EnemyShooterV1 ({name}): มี Burst ที่ไม่ได้ตั้งค่า bulletPrefab ไว้ - ข้ามการยิงชุดนี้");
                return;
            }

            Vector2 centerDirection = burst.aimAtPlayer
                ? GetDirectionToPlayer()
                : burst.baseDirection.normalized;

            int count = Mathf.Max(burst.bulletCount, 1);

            for (int i = 0; i < count; i++)
            {
                float angleOffset = CalculateAngleOffset(i, count, burst.spreadAngle);
                Vector2 direction = RotateVector(centerDirection, angleOffset);

                GameObject bulletObj = PoolManager.Instance.Get(burst.bulletPrefab, firePoint.position, Quaternion.identity);
                if (bulletObj.TryGetComponent<EnemyBulletV1>(out var bullet))
                {
                    bullet.SetDirection(direction);
                }
            }
        }

        private Vector2 GetDirectionToPlayer()
        {
            // playerTransform อาจเป็น null ได้ถ้าหา Player ไม่เจอตอน Start (เช่น Player ตายอยู่พอดี)
            // fallback เป็นยิงลงตรงๆ กันกระสุนหายไปเงียบๆ โดยไม่รู้สาเหตุ
            if (playerTransform == null) return Vector2.down;

            return ((Vector2)playerTransform.position - (Vector2)firePoint.position).normalized;
        }

        private float CalculateAngleOffset(int index, int count, float spreadAngle)
        {
            if (count == 1) return 0f; // ยิงนัดเดียว ไม่มีอะไรให้กระจาย

            // กระจายมุมเท่าๆ กันรอบทิศศูนย์กลาง
            // ตัวอย่าง: 5 นัด, spread 40 องศา -> -20, -10, 0, 10, 20
            float step = spreadAngle / (count - 1);
            return -spreadAngle / 2f + step * index;
        }

        private Vector2 RotateVector(Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            // สูตรหมุนเวกเตอร์ 2D มาตรฐาน (rotation matrix)
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }
    }
}