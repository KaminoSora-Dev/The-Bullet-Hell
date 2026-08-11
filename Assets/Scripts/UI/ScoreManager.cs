using System;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Singleton ถาวร (DontDestroyOnLoad) - เก็บคะแนนปัจจุบันและ High Score
    /// ของรอบการเล่นปัจจุบัน
    ///
    /// API รุ่นเก่ายังคงอยู่เพื่อไม่ให้ UI/Debug ที่เขียนไว้ก่อนหน้านี้พัง:
    /// - CurrentScore
    /// - OnScoreChanged
    /// - HiScore
    /// - OnHiScoreChanged
    ///
    /// MenuManager เรียก ResetScore() ตอน New Game ดังนั้นทั้งคะแนนปัจจุบัน
    /// และ High Score ของรอบนั้นจะเริ่มใหม่พร้อมกัน
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        private int currentScore;
        private int hiScore;

        public int CurrentScore => currentScore;
        public int HiScore => hiScore;

        /// <summary>ยิงทุกครั้งที่ CurrentScore เปลี่ยน</summary>
        public event Action<int> OnScoreChanged;

        /// <summary>ยิงเมื่อ HiScore เปลี่ยน</summary>
        public event Action<int> OnHiScoreChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// เพิ่มคะแนน
        /// ถ้าคะแนนใหม่สูงกว่า HiScore จะอัปเดต HiScore และยิง OnHiScoreChanged
        /// </summary>
        public void AddScore(int amount)
        {
            if (amount == 0)
                return;

            currentScore += amount;

            // ป้องกันคะแนนติดลบโดยไม่ตั้งใจ
            if (currentScore < 0)
                currentScore = 0;

            OnScoreChanged?.Invoke(currentScore);

            if (currentScore > hiScore)
            {
                hiScore = currentScore;
                OnHiScoreChanged?.Invoke(hiScore);
            }
        }

        /// <summary>
        /// เริ่ม Run ใหม่ - รีเซ็ตคะแนนและ High Score ของ Run นี้
        /// </summary>
        public void ResetScore()
        {
            currentScore = 0;
            hiScore = 0;

            OnScoreChanged?.Invoke(currentScore);
            OnHiScoreChanged?.Invoke(hiScore);
        }
    }
}