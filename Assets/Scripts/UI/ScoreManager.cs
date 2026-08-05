using System;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// ตัวกลางเก็บ Score ปัจจุบัน + HiScore ของเกม
    /// เป็น Singleton เพราะต้องมีที่เดียวที่ "เป็นเจ้าของค่าคะแนนจริง" - ทุกคนที่อยากบวกคะแนน
    /// (Enemy ตาย, เก็บ Fragment, Graze ฯลฯ) เรียกผ่านตัวนี้ตัวเดียว ไม่ใช่ต่างคนต่างเก็บตัวแปรคะแนนของตัวเอง
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Persistence")]
        [Tooltip("Key ที่ใช้เก็บ HiScore ใน PlayerPrefs - เปลี่ยนได้ถ้าอยากแยกเซฟหลายโหมด/หลายผู้เล่น")]
        [SerializeField] private string hiScorePlayerPrefsKey = "HiScore";

        private int currentScore;
        private int hiScore;

        public int CurrentScore => currentScore;
        public int HiScore => hiScore;

        // ใช้ event แทนการให้ UI ไป Update() เช็คค่าทุกเฟรม เพราะคะแนนไม่ได้เปลี่ยนทุกเฟรม
        // ยิง event เฉพาะตอนค่าเปลี่ยนจริงๆ พอ ประหยัดกว่าเยอะ
        public event Action<int> OnScoreChanged;
        public event Action<int> OnHiScoreChanged;

        void Awake()
        {
            // Singleton pattern แบบกันซ้ำ: ถ้ามีตัวก่อนหน้าอยู่แล้ว (เช่นเผลอลากใส่ไว้ 2 ที่ใน scene)
            // ให้ทำลายตัวใหม่ทิ้งไป ไม่ใช่ตัวเก่า กัน reference ที่คนอื่น cache ไว้แล้วพัง
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            hiScore = PlayerPrefs.GetInt(hiScorePlayerPrefsKey, 0);
        }

        /// <summary>
        /// บวกคะแนน - ใช้เมธอดเดียวทั้งจาก Enemy ตาย, Graze, หรือแหล่งอื่นในอนาคต
        /// ไม่แยกเมธอดตามแหล่งที่มา เพราะ logic การบวกคะแนนเหมือนกันหมด ต่างกันแค่ "จำนวน" ที่ส่งเข้ามา
        /// </summary>
        public void AddScore(int amount)
        {
            if (amount == 0) return;

            currentScore += amount;
            if (currentScore < 0) currentScore = 0; // กันเผื่อในอนาคตมีระบบหักคะแนน ไม่ให้ติดลบ

            OnScoreChanged?.Invoke(currentScore);

            if (currentScore > hiScore)
            {
                hiScore = currentScore;
                OnHiScoreChanged?.Invoke(hiScore);

                // เซฟเฉพาะตอนทำลาย HiScore เดิมได้จริงๆ เท่านั้น ไม่เซฟทุกครั้งที่คะแนนขยับ
                // เพราะ PlayerPrefs.Save() เขียนลงดิสก์จริง เรียกถี่เกินไปไม่จำเป็นและกิน performance เปล่าๆ
                PlayerPrefs.SetInt(hiScorePlayerPrefsKey, hiScore);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// เรียกตอนเริ่มเกมใหม่ (ไม่แตะ HiScore เพราะ HiScore ต้องอยู่ข้ามรอบเล่น)
        /// </summary>
        public void ResetScore()
        {
            currentScore = 0;
            OnScoreChanged?.Invoke(currentScore);
        }

        /// <summary>
        /// ตั้งคะแนนตรงๆ (เช่น debug/cheat, หรือ system อื่นในอนาคตที่ต้อง set ค่าแทนการบวกทีละนิด)
        /// ต่างจาก AddScore ตรงที่นี่คือ "กำหนดค่าสุดท้าย" ไม่ใช่ "บวกเพิ่ม"
        /// </summary>
        public void SetScore(int value)
        {
            currentScore = Mathf.Max(value, 0);
            OnScoreChanged?.Invoke(currentScore);

            if (currentScore > hiScore)
            {
                hiScore = currentScore;
                OnHiScoreChanged?.Invoke(hiScore);
                PlayerPrefs.SetInt(hiScorePlayerPrefsKey, hiScore);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// ตั้ง HiScore ตรงๆ - เผื่ออยาก reset หรือ import ค่าจาก save file ภายนอก
        /// </summary>
        public void SetHiScore(int value)
        {
            hiScore = Mathf.Max(value, 0);
            PlayerPrefs.SetInt(hiScorePlayerPrefsKey, hiScore);
            PlayerPrefs.Save();
            OnHiScoreChanged?.Invoke(hiScore);
        }

        void OnDestroy()
        {
            // กันเผื่อมีการเปลี่ยน scene/โหลดซ้ำแล้วตัวนี้ถูกทำลายไป
            if (Instance == this) Instance = null;
        }
    }
}
