using System;
using UnityEngine;

namespace PlayerSystem
{
    /// <summary>
    /// ตัวกลางเก็บสถานะของผู้เล่นที่ไม่ใช่ตำแหน่ง/การเคลื่อนที่ (พวกนั้นอยู่ใน PlayerControllerV1 อยู่แล้ว)
    /// แยกออกมาเป็นคนละไฟล์เพราะ Life/Spell/Fragment เป็น "สถานะของรอบเล่น" (run state)
    /// ที่ทั้ง UI, PlayerControllerV1, FragmentPickupV1 ต้องอ่าน/เขียนร่วมกัน - ถ้าฝังไว้ใน PlayerControllerV1
    /// เฉยๆ ทุกระบบจะต้องหา GameObject ผู้เล่นแล้ว GetComponent ไปเรื่อยๆ ซึ่งไม่จำเป็น
    /// ทำเป็น Singleton ตัวเดียวกลางเกมสะดวกกว่า
    /// </summary>
    public class PlayerStatsManager : MonoBehaviour
    {
        public static PlayerStatsManager Instance { get; private set; }

        [Header("Life")]
        [SerializeField] private int startingLives = 2;
        [Tooltip("จำนวนชีวิตสูงสุดที่เก็บได้ (กันเก็บ 1UP เกินจนล้น)")]
        [SerializeField] private int maxLives = 8;

        [Header("Spell (นับเป็นเหมือน Bomb ในเกมนี้)")]
        [SerializeField] private int startingSpells = 3;
        [SerializeField] private int maxSpells = 8;

        [Header("Fragment / Power")]
        [Tooltip("Power level สูงสุด - Fragment แต่ละชิ้นเพิ่ม Power 1 ระดับทันที ไม่ต้องสะสมให้ครบก่อน " +
                 "ตั้งเป็นเลขคงที่ (fixed) ตามจำนวนไอคอน Power ที่วางไว้ใน Scene")]
        [SerializeField] private int maxPowerLevel = 6;

        [Header("Graze")]
        [Tooltip("นับจำนวนครั้งที่ Graze สะสมทั้งหมดในรอบเล่นนี้ (แยกจากคะแนน Graze ใน ScoreManager)")]
        [SerializeField] private int startingGraze = 0;

        private int currentLives;
        private int currentSpells;
        private int currentPowerLevel;
        private int currentGraze;

        public int CurrentLives => currentLives;
        public int CurrentSpells => currentSpells;
        public int CurrentPowerLevel => currentPowerLevel;
        public int MaxPowerLevel => maxPowerLevel;
        public int CurrentGraze => currentGraze;

        public event Action<int> OnLivesChanged;
        public event Action<int> OnSpellsChanged;

        // ยิงทุกครั้งที่ Power level เปลี่ยน (Fragment แต่ละชิ้นเพิ่ม Power ทันที ไม่ต้องรอสะสมครบเหมือนเดิม)
        public event Action<int> OnPowerLevelChanged;

        // ยิงทุกครั้งที่ Graze สะสมเพิ่ม - แยกจาก ScoreManager เพราะนี่คือ "จำนวนครั้ง" ไม่ใช่คะแนน
        public event Action<int> OnGrazeChanged;

        // ยิงตอนชีวิตหมดเกลี้ยงจริงๆ (LoseLife แล้วเหลือ 0) - ให้ระบบ Game Over ในอนาคตมาฟัง event นี้
        public event Action OnGameOver;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            currentLives = startingLives;
            currentSpells = startingSpells;
            currentPowerLevel = 0;
            currentGraze = startingGraze;
        }

        // ---------- Life ----------

        public void AddLife(int amount = 1)
        {
            currentLives = Mathf.Min(currentLives + amount, maxLives);
            OnLivesChanged?.Invoke(currentLives);
        }

        /// <summary>
        /// เรียกตอนผู้เล่นโดนโจมตีจนตาย (จาก PlayerControllerV1.Die())
        /// คืนค่า true ถ้ายังมีชีวิตเหลือให้ respawn ต่อ, false ถ้าหมดแล้ว (Game Over)
        /// แยก return bool ออกมาแทนที่จะให้ PlayerControllerV1 ไปเช็ค CurrentLives เอง
        /// เพราะจุดตัดสินใจ "หมดชีวิตหรือยัง" ควรอยู่ที่เจ้าของข้อมูล (ตัวนี้) ที่เดียว
        /// </summary>
        public bool LoseLife()
        {
            currentLives = Mathf.Max(currentLives - 1, 0);
            OnLivesChanged?.Invoke(currentLives);

            if (currentLives <= 0)
            {
                OnGameOver?.Invoke();
                return false;
            }
            return true;
        }

        // ---------- Spell ----------

        public void AddSpell(int amount = 1)
        {
            currentSpells = Mathf.Min(currentSpells + amount, maxSpells);
            OnSpellsChanged?.Invoke(currentSpells);
        }

        /// <summary>
        /// เรียกตอนกด Bomb - คืนค่า false ถ้าไม่มี spell เหลือให้ใช้ (PlayerControllerV1.Bomb() เช็คก่อนค่อยเคลียร์กระสุน)
        /// </summary>
        public bool UseSpell()
        {
            if (currentSpells <= 0) return false;

            currentSpells--;
            OnSpellsChanged?.Invoke(currentSpells);
            return true;
        }

        // ---------- Fragment / Power ----------

        /// <summary>
        /// เรียกจาก FragmentPickupV1 ตอนผู้เล่นเก็บ Fragment ได้ - Fragment แต่ละชิ้นเพิ่ม Power ทันที 1 ระดับ
        /// (เปลี่ยนจากเดิมที่ต้องสะสมให้ครบ fragmentsPerPowerLevel ก่อน) ใช้ amount เผื่ออนาคตมีไอเทมพิเศษที่ให้ทีเดียวหลาย level
        /// </summary>
        public void AddFragment(int amount = 1)
        {
            if (currentPowerLevel >= maxPowerLevel) return; // Power เต็มแล้ว ไม่มีผลอะไรต่อ

            currentPowerLevel = Mathf.Min(currentPowerLevel + amount, maxPowerLevel);
            OnPowerLevelChanged?.Invoke(currentPowerLevel);
        }

        // ---------- Graze ----------

        /// <summary>
        /// เรียกจาก GrazeDetectorV1 ทุกครั้งที่เฉียดกระสุน 1 นัด - นับจำนวนครั้งสะสม แยกจากคะแนนที่ให้ผ่าน ScoreManager
        /// </summary>
        public void AddGraze(int amount = 1)
        {
            currentGraze += amount;
            OnGrazeChanged?.Invoke(currentGraze);
        }
    }
}