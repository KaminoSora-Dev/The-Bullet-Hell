using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UISystem
{
    /// <summary>
    /// ทำให้กลุ่มปุ่ม UI เลือกได้ด้วยลูกศรขึ้น/ลง (ปรับ Key ได้) + ยืนยันด้วยปุ่ม Z (ปรับ Key ได้)
    /// เมาส์ยังคลิก/hover เลือกได้ตามปกติ เพราะไม่ได้แตะ onClick เดิมของปุ่มเลย
    ///
    /// Component นี้ Reusable ใช้ได้กับทุกเมนู (MainMenu, Pause, GameLose ฯลฯ)
    /// เพียงแปะไว้บน GameObject ของ Panel เมนูนั้นๆ แล้วลากปุ่มใส่ menuButtons
    ///
    /// พฤติกรรม Auto:
    /// - Component นี้เปิด/ปิดตาม GameObject ของ Panel เอง (ผ่าน OnEnable/OnDisable ของ Unity)
    ///   ไม่ต้องมีใครมา Enable/Disable เองจากที่อื่น - แค่ SetActive Panel ตามปกติ
    /// - ทุกครั้งที่ Panel ถูกเปิด (OnEnable) จะ Reset กลับไปเลือกปุ่มแรกที่กดได้ให้อัตโนมัติ
    ///
    /// Setup:
    /// 1. แปะ Component นี้ไว้บน Canvas/Panel ของเมนูนั้นๆ
    /// 2. ลากปุ่มเรียงตามลำดับ (บนลงล่าง ตามที่อยากให้ลูกศรขึ้น/ลงไล่) ใส่ menuButtons
    /// 3. (ไม่บังคับ) ลาก Sprite/Image ซ้าย-ขวา ที่จะใช้เป็นตัวชี้ว่ากำลังเลือกปุ่มไหน ใส่ selectorLeft / selectorRight
    /// </summary>
    public class KeyboardMenuNavigator : MonoBehaviour
    {
        [Header("Buttons (เรียงบนลงล่างตามลำดับที่ต้องการ Navigate)")]
        [SerializeField] private List<Button> menuButtons = new List<Button>();

        [Header("Selector Indicator (ไม่ใส่ก็ได้)")]
        [Tooltip(
            "GameObject ที่มี SpriteRenderer ติดอยู่ (แบบเดียวกับกล่องดำ) - ฝั่งซ้ายของปุ่มที่กำลังเลือกอยู่\n" +
            "ไม่จำเป็นต้องเป็น UI Element - Transform ธรรมดาก็ใช้ได้ เพราะ Navigator อ่านตำแหน่งปุ่มเป็น World Position อยู่แล้ว\n" +
            "ตั้ง Sorting Layer ของ SpriteRenderer เอาเองใน Inspector เพื่อคุมว่าจะให้อยู่หน้า/หลังอะไร"
        )]
        [SerializeField] private Transform selectorLeft;

        [Tooltip("GameObject ที่มี SpriteRenderer ติดอยู่ - ฝั่งขวาของปุ่มที่กำลังเลือกอยู่")]
        [SerializeField] private Transform selectorRight;

        [Tooltip("ระยะห่างจากขอบปุ่ม (หน่วยเดียวกับ World Space - ปรับตามขนาด Sprite จริง)")]
        [SerializeField] private float selectorPadding = 0.3f;

        [Header("Input")]
        [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
        [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
        [SerializeField] private KeyCode confirmKey = KeyCode.Z;

        [Header("Click Sound (ไม่ใส่ก็ได้)")]
        [Tooltip("ไม่ใส่ = จะพยายามหา AudioSource บน GameObject เดียวกันให้เอง")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("เสียงสั้นๆ ตอนกดปุ่ม - เล่นเหมือนกันทั้งตอนคลิกเมาส์และกด Z เพราะทั้งคู่วิ่งผ่าน onClick เดียวกัน")]
        [SerializeField] private AudioClip clickSound;

        private int currentIndex = -1;
        private bool inputEnabled = true;
        private bool hoverRelaysAttached = false;
        private bool clickSoundAttached = false;

        /// <summary>สถานะ Input ปัจจุบัน - true = รับลูกศร/Z ปกติ</summary>
        public bool InputEnabled => inputEnabled;

        private void Awake()
        {
            AttachHoverRelays();
            AttachClickSoundListeners();
        }

        private void OnDestroy()
        {
            DetachClickSoundListeners();
        }

        private void OnEnable()
        {
            inputEnabled = true;
            ResetSelectionToFirst();
        }

        private void OnDisable()
        {
            HideSelector();
        }

        private void Update()
        {
            if (!inputEnabled)
                return;

            if (Input.GetKeyDown(upKey))
                MoveSelection(-1);
            else if (Input.GetKeyDown(downKey))
                MoveSelection(1);

            if (Input.GetKeyDown(confirmKey))
                ActivateSelected();
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// เปิด/ปิดการรับ Input คีย์บอร์ด
        /// ใช้ตอนมี Transition อื่นกำลังทำงานอยู่ (เช่น TransitionBox กำลังเคลื่อนที่)
        /// เพื่อกันผู้เล่นกด Z/ลูกศร ระหว่างนั้น
        /// </summary>
        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
        }

        /// <summary>เลือกกลับไปที่ปุ่มแรกที่ยังกดได้ (interactable) - เรียกอัตโนมัติทุกครั้งที่ Panel เปิด</summary>
        public void ResetSelectionToFirst()
        {
            int firstInteractable = FindNextInteractable(-1, 1);
            SelectIndex(firstInteractable);
        }

        // ============================================================
        // SELECTION
        // ============================================================

        private void MoveSelection(int direction)
        {
            int next = FindNextInteractable(currentIndex, direction);
            SelectIndex(next);
        }

        /// <summary>
        /// หาปุ่มถัดไปที่ interactable ในทิศทางที่กำหนด โดยไม่ Wrap กลับไปอีกฝั่ง
        /// (ชนขอบบนสุด/ล่างสุดแล้วหยุดอยู่ตรงนั้น)
        /// </summary>
        private int FindNextInteractable(int fromIndex, int direction)
        {
            if (menuButtons == null || menuButtons.Count == 0)
                return -1;

            int count = menuButtons.Count;
            int index = fromIndex;

            for (int step = 0; step < count; step++)
            {
                index += direction;

                if (index < 0 || index >= count)
                    break; // ชนขอบ - ไม่ Wrap

                if (menuButtons[index] != null && menuButtons[index].interactable)
                    return index;
            }

            // หาไม่เจอตัวถัดไปที่กดได้ - อยู่ตำแหน่งเดิมถ้ายังกดได้
            if (fromIndex >= 0 && fromIndex < count &&
                menuButtons[fromIndex] != null && menuButtons[fromIndex].interactable)
            {
                return fromIndex;
            }

            // ไม่งั้นหาตัวแรกสุดที่กดได้ทั้งลิสต์
            for (int i = 0; i < count; i++)
            {
                if (menuButtons[i] != null && menuButtons[i].interactable)
                    return i;
            }

            return -1;
        }

        private void SelectIndex(int index)
        {
            if (menuButtons == null || index < 0 || index >= menuButtons.Count)
            {
                currentIndex = -1;
                HideSelector();
                return;
            }

            currentIndex = index;
            UpdateSelectorPosition();
        }

        /// <summary>เรียกจาก Hover Relay ตอนเมาส์ชี้ปุ่ม - ทำให้ Hover ก็เลือกปุ่มนั้นเหมือนกดลูกศร</summary>
        private void OnPointerEnterButton(int index)
        {
            if (menuButtons == null || index < 0 || index >= menuButtons.Count)
                return;

            if (menuButtons[index] == null || !menuButtons[index].interactable)
                return;

            SelectIndex(index);
        }

        private void ActivateSelected()
        {
            if (menuButtons == null || currentIndex < 0 || currentIndex >= menuButtons.Count)
                return;

            Button target = menuButtons[currentIndex];

            // จำลองการกดปุ่มเดียวกับตอนคลิกเมาส์ - ใช้ onClick เดิมของปุ่มทุกอย่าง
            if (target != null && target.interactable)
                target.onClick.Invoke();
        }

        // ============================================================
        // SELECTOR VISUAL
        // ============================================================

        private void UpdateSelectorPosition()
        {
            if (currentIndex < 0 || currentIndex >= menuButtons.Count)
                return;

            Button target = menuButtons[currentIndex];
            if (target == null)
                return;

            RectTransform buttonRect = target.GetComponent<RectTransform>();
            if (buttonRect == null)
                return;

            Vector3 center = buttonRect.position;
            float halfWidth = (buttonRect.rect.width * buttonRect.lossyScale.x) * 0.5f;

            if (selectorLeft != null)
            {
                selectorLeft.gameObject.SetActive(true);
                selectorLeft.position = center + new Vector3(-(halfWidth + selectorPadding), 0f, 0f);
            }

            if (selectorRight != null)
            {
                selectorRight.gameObject.SetActive(true);
                selectorRight.position = center + new Vector3(halfWidth + selectorPadding, 0f, 0f);
            }
        }

        private void HideSelector()
        {
            if (selectorLeft != null) selectorLeft.gameObject.SetActive(false);
            if (selectorRight != null) selectorRight.gameObject.SetActive(false);
        }

        // ============================================================
        // CLICK SOUND
        // ============================================================

        /// <summary>
        /// ผูกเสียงคลิกเข้ากับ onClick ของทุกปุ่มโดยตรง
        /// เพราะทั้งคลิกเมาส์ และกด Z (ซึ่งเรียก onClick.Invoke() ใน ActivateSelected())
        /// วิ่งผ่าน Event ตัวเดียวกันเป๊ะ - เลยได้เสียงเดียวกันทั้งสองทางโดยไม่ต้องเช็คแยก
        /// </summary>
        private void AttachClickSoundListeners()
        {
            if (clickSoundAttached || menuButtons == null)
                return;

            clickSoundAttached = true;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button btn = menuButtons[i];
                if (btn == null)
                    continue;

                btn.onClick.AddListener(PlayClickSound);
            }
        }

        private void DetachClickSoundListeners()
        {
            if (menuButtons == null)
                return;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button btn = menuButtons[i];
                if (btn == null)
                    continue;

                btn.onClick.RemoveListener(PlayClickSound);
            }
        }

        private void PlayClickSound()
        {
            if (audioSource != null && clickSound != null)
                audioSource.PlayOneShot(clickSound);
        }

        // ============================================================
        // MOUSE HOVER RELAY
        // ============================================================

        private void AttachHoverRelays()
        {
            if (hoverRelaysAttached || menuButtons == null)
                return;

            hoverRelaysAttached = true;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button btn = menuButtons[i];
                if (btn == null)
                    continue;

                HoverRelay relay = btn.gameObject.GetComponent<HoverRelay>();
                if (relay == null)
                    relay = btn.gameObject.AddComponent<HoverRelay>();

                relay.Setup(this, i);
            }
        }

        /// <summary>
        /// ติดอยู่บนปุ่มแต่ละอันแบบอัตโนมัติ (AddComponent ให้เอง)
        /// ใช้ส่ง Hover Event กลับมาที่ Navigator เท่านั้น - ไม่แตะ onClick เดิมของปุ่มเลย
        /// </summary>
        private class HoverRelay : MonoBehaviour, IPointerEnterHandler
        {
            private KeyboardMenuNavigator owner;
            private int index;

            public void Setup(KeyboardMenuNavigator owner, int index)
            {
                this.owner = owner;
                this.index = index;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                owner?.OnPointerEnterButton(index);
            }
        }
    }
}