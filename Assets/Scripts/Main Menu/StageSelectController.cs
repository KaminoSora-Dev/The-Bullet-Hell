using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameFlowSystem;
using GameSystem;
using PlayerSystem;
using UISystem;

namespace MenuSystemV1
{
    [System.Serializable]
    public class StageSelectEntry
    {
        [Tooltip("ปุ่มของด่านนี้ - ลาก Text/จัดตำแหน่งเองใน Editor ได้ตามต้องการ")]
        public Button button;

        [Tooltip("ชื่อ Scene ที่จะโหลดเมื่อกดปุ่มนี้ (ต้องตรงกับชื่อใน Build Settings)")]
        public string sceneName;
    }

    /// <summary>
    /// ควบคุมหน้า Stage Select (Stage List)
    ///
    /// เปิดโดย MenuManager ผ่าน Open() (ตอนกดปุ่ม "Stage Select" ที่ Main Menu)
    /// ปิดตัวเองผ่าน Go Back หรือกดปุ่มด่านแล้วโหลด Scene ทันที
    ///
    /// Panel/Navigator ของหน้านี้จัดการเองทั้งหมด - ไม่ไปยุ่งกับ
    /// Panel หรือ KeyboardMenuNavigator ของ Main Menu โดยตรง
    /// (แยกความรับผิดชอบตาม convention เดิมของโปรเจกต์)
    ///
    /// เมื่อปิด (Go Back) จะยิง OnClosed event ให้ MenuManager
    /// เป็นคนเปิด Panel ปุ่มหลักของตัวเองกลับ
    /// </summary>
    public class StageSelectController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Panel/Canvas ของหน้า Stage List ทั้งหมด")]
        [SerializeField] private GameObject stageSelectPanel;

        [Header("Keyboard Navigation")]
        [Tooltip("KeyboardMenuNavigator แยกต่างหากสำหรับหน้านี้โดยเฉพาะ (ต้องมีปุ่มด่าน + Go Back อยู่ในลิสต์ของมัน)")]
        [SerializeField] private KeyboardMenuNavigator stageSelectNavigator;

        [Header("Stage Entries")]
        [Tooltip("รายการปุ่มแต่ละด่าน - เพิ่ม/ลดได้อิสระตามจำนวนด่านจริง")]
        [SerializeField] private List<StageSelectEntry> stages = new List<StageSelectEntry>();

        [Header("Go Back")]
        [SerializeField] private Button goBackButton;

        [Header("Run Reset")]
        [Tooltip("Reset Run/Score เหมือนกด Start ปกติ ก่อนโหลดด่านที่เลือก (ตั้ง false ถ้าอยากโหลดตรง ๆ ไม่ reset)")]
        [SerializeField] private bool resetRunOnStageSelect = true;

        /// <summary>
        /// ยิงเมื่อหน้า Stage Select ถูกปิด (Go Back) - MenuManager ฟัง event นี้
        /// เพื่อเปิด Panel ปุ่มหลักของตัวเองกลับ
        /// </summary>
        public event Action OnClosed;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            ValidateStageEntries();
        }

        private void OnEnable()
        {
            ResetToClosedState();

            for (int i = 0; i < stages.Count; i++)
            {
                StageSelectEntry entry = stages[i];

                if (entry == null || entry.button == null)
                    continue;

                // ผูก sceneName ของแต่ละ entry เข้ากับปุ่มของมันเอง
                entry.button.onClick.AddListener(() => OnStageButtonClicked(entry.sceneName));
            }

            if (goBackButton != null)
                goBackButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            for (int i = 0; i < stages.Count; i++)
            {
                StageSelectEntry entry = stages[i];

                if (entry == null || entry.button == null)
                    continue;

                // ล้าง Listener ที่ผูกไว้ตอน OnEnable ทั้งหมด (ไม่กระทบ Listener ที่ตั้งไว้ใน Inspector)
                entry.button.onClick.RemoveAllListeners();
            }

            if (goBackButton != null)
                goBackButton.onClick.RemoveListener(Close);
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// เปิดหน้า Stage Select - เรียกจาก MenuManager
        /// </summary>
        public void Open()
        {
            if (stageSelectPanel != null)
                stageSelectPanel.SetActive(true);

            if (stageSelectNavigator != null)
            {
                stageSelectNavigator.SetInputEnabled(true);
                stageSelectNavigator.ResetForMenuEntry();
            }
        }

        // ============================================================
        // GO BACK
        // ============================================================

        private void Close()
        {
            if (stageSelectNavigator != null)
                stageSelectNavigator.SetInputEnabled(false);

            if (stageSelectPanel != null)
                stageSelectPanel.SetActive(false);

            OnClosed?.Invoke();
        }

        // ============================================================
        // STAGE BUTTON
        // ============================================================

        private void OnStageButtonClicked(string sceneName)
        {
            // กันกดซ้ำระหว่างกำลังโหลดอยู่แล้ว
            if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading)
                return;

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("StageSelectController: มีปุ่มที่ไม่ได้ตั้ง sceneName ไว้ - ข้าม");
                return;
            }

            if (resetRunOnStageSelect)
            {
                ScoreManager.Instance?.ResetScore();
                PlayerStatsManager.Instance?.ResetRun();
            }

            if (LoadingScreenManager.Instance != null)
            {
                LoadingScreenManager.Instance.LoadScene(sceneName);
                return;
            }

            Debug.LogError(
                $"StageSelectController: ไม่พบ LoadingScreenManager.Instance - " +
                $"โหลด Scene '{sceneName}' ไม่ได้"
            );
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private void ResetToClosedState()
        {
            if (stageSelectPanel != null)
                stageSelectPanel.SetActive(false);

            if (stageSelectNavigator != null)
                stageSelectNavigator.SetInputEnabled(false);
        }

        private void ValidateStageEntries()
        {
            for (int i = 0; i < stages.Count; i++)
            {
                StageSelectEntry entry = stages[i];

                if (entry == null || entry.button == null)
                {
                    Debug.LogWarning(
                        $"StageSelectController: Stage Entry ลำดับที่ {i} ไม่ได้ตั้งค่า Button ไว้"
                    );
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.sceneName))
                {
                    Debug.LogWarning(
                        $"StageSelectController: ปุ่ม '{entry.button.name}' ไม่ได้ตั้ง sceneName ไว้"
                    );
                }
            }
        }
    }
}