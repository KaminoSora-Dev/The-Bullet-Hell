using UnityEngine;
using PoolingSystem;
using EnemySystem;

namespace PlayerSystem
{
    [RequireComponent(typeof(Collider2D))]
    public class PlayerShieldV1 : MonoBehaviour
    {
        private float remainingTime;
        private bool isActive;

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            gameObject.SetActive(false);
        }

        public void Activate(float duration)
        {
            remainingTime = duration;
            isActive = true;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!isActive) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime <= 0f)
            {
                isActive = false;
                gameObject.SetActive(false);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("EnemyBullet")) return;

            // เปลี่ยนใหม่: ใช้ RequestDestroy() แทน PoolManager.Instance.Return() ตรงๆ
            // เพื่อให้กระสุนที่ผูก Death Animation ไว้เล่นแอนิเมชันตอนโดนโล่ดูดด้วย
            if (other.TryGetComponent<EnemyBulletV2>(out var bulletV2))
                bulletV2.RequestDestroy();
            else
                PoolManager.Instance.Return(other.gameObject);
        }
    }
}
