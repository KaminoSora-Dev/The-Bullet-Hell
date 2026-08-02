using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 1 ชุดการยิง - ยิง bulletCount นัดพร้อมกัน กระจายมุมตาม spreadAngle
    /// แล้วหน่วงเวลา delayAfter ก่อนไปชุดถัดไป
    /// </summary>
    [System.Serializable]
    public class ShootBurst
    {
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

    public class EnemyShooterV1 : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;
        [Tooltip("จุดที่กระสุนจะ spawn ออกมา ถ้าไม่ใส่จะใช้ตำแหน่งตัว Enemy เอง")]
        [SerializeField] private Transform firePoint;

        [Tooltip("ลิสต์ชุดการยิงตามลำดับ - วนซ้ำได้ถ้าติ๊ก Loop Pattern")]
        [SerializeField] private List<ShootBurst> pattern = new List<ShootBurst>();
        [SerializeField] private bool loopPattern = true;

        private Transform playerTransform;

        private int currentBurstIndex;
        private float delayTimer;
        private bool hasFiredCurrentBurst;
        private bool patternFinished; // true เมื่อ pattern จบแล้วและไม่ loop - หยุดยิงถาวร

        void Start()
        {
            // Cache หา Player ไว้ตั้งแต่ต้น ไม่ต้อง FindGameObjectWithTag ซ้ำทุกครั้งที่ยิง
            // (FindGameObjectWithTag ต้องไล่หาทั้ง scene ถ้าเรียกทุกเฟรม/ทุกนัดจะกิน performance โดยไม่จำเป็น)
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;

            if (firePoint == null) firePoint = transform;
        }

        void Update()
        {
            if (bulletPrefab == null || pattern.Count == 0 || patternFinished) return;

            ShootBurst burst = pattern[currentBurstIndex];

            // ยิงทันทีตอนเพิ่งเข้าคิวชุดนี้ (ยังไม่เคยยิง) แล้วค่อยเริ่มนับเวลาหน่วง
            if (!hasFiredCurrentBurst)
            {
                FireBurst(burst);
                hasFiredCurrentBurst = true;
                delayTimer = 0f;
            }

            delayTimer += Time.deltaTime;
            if (delayTimer >= burst.delayAfter)
            {
                AdvanceToNextBurst();
            }
        }

        private void AdvanceToNextBurst()
        {
            currentBurstIndex++;
            hasFiredCurrentBurst = false;

            if (currentBurstIndex >= pattern.Count)
            {
                if (loopPattern)
                {
                    currentBurstIndex = 0;
                }
                else
                {
                    patternFinished = true;
                }
            }
        }

        private void FireBurst(ShootBurst burst)
        {
            Vector2 centerDirection = burst.aimAtPlayer
                ? GetDirectionToPlayer()
                : burst.baseDirection.normalized;

            int count = Mathf.Max(burst.bulletCount, 1);

            for (int i = 0; i < count; i++)
            {
                float angleOffset = CalculateAngleOffset(i, count, burst.spreadAngle);
                Vector2 direction = RotateVector(centerDirection, angleOffset);

                GameObject bulletObj = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
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