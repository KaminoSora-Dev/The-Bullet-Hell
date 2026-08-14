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

        // เพิ่มใหม่: Key สำหรับเก็บ HiScore ถาวรใน PlayerPrefs (อยู่ข้าม Session/ปิดเกมแล้วเปิดใหม่)
        // แยกต่างหากจาก hiScore ตัวเดิมด้านล่างโดยตั้งใจ - hiScore เดิมเป็นแค่ "HiScore ของ Run นี้"
        // (รีเซ็ตเป็น 0 ทุกครั้งที่ ResetScore() ถูกเรียก) ส่วนตัวใหม่นี้คือสถิติสูงสุดตลอดกาลของเครื่อง
        private const string PersistentHiScorePrefsKey = "BulletHell_PersistentHiScore";

        private int currentScore;
        private int hiScore;
        private int persistentHiScore; // เพิ่มใหม่

        public int CurrentScore => currentScore;
        public int HiScore => hiScore;

        /// <summary>เพิ่มใหม่: HiScore ถาวรตลอดกาลของเครื่องนี้ (โหลดจาก PlayerPrefs)</summary>
        public int PersistentHiScore => persistentHiScore;

        /// <summary>ยิงทุกครั้งที่ CurrentScore เปลี่ยน</summary>
        public event Action<int> OnScoreChanged;

        /// <summary>ยิงเมื่อ HiScore เปลี่ยน</summary>
        public event Action<int> OnHiScoreChanged;

        /// <summary>เพิ่มใหม่: ยิงเมื่อ PersistentHiScore ถูกบันทึกค่าใหม่ (สำหรับ UI หน้าเมนูในอนาคต)</summary>
        public event Action<int> OnPersistentHiScoreChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // เพิ่มใหม่: โหลด HiScore ถาวรจากเครื่อง (ถ้าไม่เคยมีมาก่อนจะได้ 0)
            persistentHiScore = PlayerPrefs.GetInt(PersistentHiScorePrefsKey, 0);
            hiScore = persistentHiScore;
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
            // บันทึก High Score ถาวรก่อนรีเซ็ตคะแนน
            SavePersistentHiScoreIfHigher();

            currentScore = 0;
            hiScore = persistentHiScore;

            OnScoreChanged?.Invoke(currentScore);
            OnHiScoreChanged?.Invoke(hiScore);
        }

        /// <summary>
        /// ตั้ง CurrentScore สำหรับระบบ Checkpoint/Restore โดยไม่แตะ PersistentHiScore
        /// </summary>
        public void SetCurrentScore(int value)
        {
            currentScore = Mathf.Max(0, value);

            if (currentScore > hiScore)
                hiScore = currentScore;

            OnScoreChanged?.Invoke(currentScore);
            OnHiScoreChanged?.Invoke(hiScore);
        }

        /// <summary>
        /// เพิ่มใหม่: เช็คว่า CurrentScore ตอนนี้สูงกว่า PersistentHiScore เดิมไหม
        /// ถ้าสูงกว่า จะบันทึกลง PlayerPrefs ทันที
        ///
        /// สำคัญ: ต้องเรียกฟังก์ชันนี้ "ก่อน" ResetScore() เสมอ ไม่งั้นคะแนนจะถูกรีเซ็ตเป็น 0
        /// ไปแล้วก่อนที่จะมาเช็ค ทำให้ไม่มีทางบันทึก HiScore ใหม่ได้เลย
        /// </summary>
        public void SavePersistentHiScoreIfHigher()
        {
            if (currentScore <= persistentHiScore)
            {
                Debug.Log($"ScoreManager: Score ปัจจุบัน ({currentScore}) ไม่สูงกว่า PersistentHiScore เดิม ({persistentHiScore}) - ไม่บันทึกใหม่");
                return;
            }

            persistentHiScore = currentScore;
            PlayerPrefs.SetInt(PersistentHiScorePrefsKey, persistentHiScore);
            PlayerPrefs.Save();

            OnPersistentHiScoreChanged?.Invoke(persistentHiScore);

            Debug.Log($"ScoreManager: บันทึก PersistentHiScore ใหม่ = {persistentHiScore} (เซฟลง PlayerPrefs แล้ว)");
        }
    }
}