using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UISystem
{
    /// <summary>
    /// ทำให้กลุ่มปุ่ม UI เลือกได้ด้วยลูกศรขึ้น/ลง + ยืนยันด้วยปุ่ม Z
    ///
    /// จุดสำคัญ:
    /// - ไม่พึ่ง Unity UI Navigation ในการควบคุมด้วยคีย์บอร์ด
    /// - ใช้ currentIndex ของตัวเองเป็นหลัก
    /// - ทุกครั้งที่เข้าเมนูใหม่ จะ reset selection
    /// - reset EventSystem.currentSelectedGameObject ด้วย
    ///
    /// Component นี้เป็น Scene-local โดยตั้งใจ
    /// ไม่ควรใช้ DontDestroyOnLoad
    /// </summary>
    public class KeyboardMenuNavigator : MonoBehaviour
    {
        [Header("Buttons (เรียงบนลงล่างตามลำดับที่ต้องการ Navigate)")]
        [SerializeField] private List<Button> menuButtons = new List<Button>();

        [Header("Selector Indicator (ไม่ใส่ก็ได้)")]
        [Tooltip(
            "GameObject ที่มี SpriteRenderer ติดอยู่ - ฝั่งซ้ายของปุ่มที่กำลังเลือกอยู่"
        )]
        [SerializeField] private Transform selectorLeft;

        [Tooltip(
            "GameObject ที่มี SpriteRenderer ติดอยู่ - ฝั่งขวาของปุ่มที่กำลังเลือกอยู่"
        )]
        [SerializeField] private Transform selectorRight;

        [Tooltip("ระยะห่างจากขอบปุ่ม")]
        [SerializeField] private float selectorPadding = 0.3f;

        [Header("Input")]
        [SerializeField] private KeyCode upKey = KeyCode.UpArrow;
        [SerializeField] private KeyCode downKey = KeyCode.DownArrow;
        [SerializeField] private KeyCode confirmKey = KeyCode.Z;

        [Header("Click Sound")]
        [Tooltip("ไม่ใส่ = จะพยายามหา AudioSource บน GameObject เดียวกัน")]
        [SerializeField] private AudioSource audioSource;

        [SerializeField] private AudioClip clickSound;

        private int currentIndex = -1;
        private bool inputEnabled = true;

        private bool hoverRelaysAttached = false;
        private bool clickSoundAttached = false;

        /// <summary>
        /// สถานะ Input ปัจจุบัน
        /// </summary>
        public bool InputEnabled => inputEnabled;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            AttachHoverRelays();
            AttachClickSoundListeners();
        }

        private void OnEnable()
        {
            // สำคัญ:
            // ทุกครั้งที่ Main Menu ถูกเปิดใหม่ ให้ reset state ทั้งหมด
            // ไม่ปล่อยให้ EventSystem หรือ currentIndex จากรอบก่อนมีผล
            ResetForMenuEntry();
        }

        private void OnDisable()
        {
            HideSelector();
        }

        private void OnDestroy()
        {
            DetachClickSoundListeners();
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
        /// </summary>
        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
        }

        /// <summary>
        /// Reset Navigator สำหรับการเข้าเมนูใหม่
        ///
        /// ทำ 3 อย่าง:
        /// 1. เปิด Keyboard Input
        /// 2. เลือกปุ่มแรกที่กดได้
        /// 3. ล้าง EventSystem selection เก่าจาก Scene ก่อนหน้า
        ///
        /// จุดนี้สำคัญมากสำหรับกรณี:
        /// MainMenu -> Gameplay -> MainMenu
        /// </summary>
        public void ResetForMenuEntry()
        {
            inputEnabled = true;
            currentIndex = -1;

            ResetSelectionToFirst();

            ResetUnityEventSystemSelection();
        }

        /// <summary>
        /// เลือกกลับไปปุ่มแรกที่ interactable
        /// </summary>
        public void ResetSelectionToFirst()
        {
            int firstInteractable = FindNextInteractable(-1, 1);

            SelectIndex(firstInteractable);
        }

        // ============================================================
        // UNITY EVENTSYSTEM RESET
        // ============================================================

        /// <summary>
        /// ล้าง selection ของ Unity EventSystem ที่อาจค้างจาก Scene ก่อนหน้า
        ///
        /// ถึง Navigator จะใช้ currentIndex ของตัวเองในการรับ Input
        /// แต่การ reset EventSystem ทำให้ UI อยู่ในสถานะสะอาดด้วย
        /// </summary>
        private void ResetUnityEventSystemSelection()
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
                return;

            // ล้าง object ที่ถูกเลือกจาก Scene ก่อนหน้า
            eventSystem.SetSelectedGameObject(null);

            // เลือกปุ่มปัจจุบันใหม่
            if (currentIndex < 0 ||
                currentIndex >= menuButtons.Count)
            {
                return;
            }

            Button button = menuButtons[currentIndex];

            if (button == null || !button.interactable)
                return;

            eventSystem.SetSelectedGameObject(button.gameObject);
        }

        // ============================================================
        // SELECTION
        // ============================================================

        private void MoveSelection(int direction)
        {
            int next = FindNextInteractable(currentIndex, direction);

            SelectIndex(next);

            // อัปเดต Unity EventSystem ด้วย
            // เพื่อให้ selection ของระบบ UI และ Navigator ตรงกัน
            ResetUnityEventSystemSelection();
        }

        /// <summary>
        /// หาปุ่มถัดไปที่ interactable
        /// ไม่ Wrap กลับอีกฝั่ง
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
                    break;

                if (menuButtons[index] != null &&
                    menuButtons[index].interactable)
                {
                    return index;
                }
            }

            // ถ้าปุ่มปัจจุบันยังกดได้ ให้ค้างอยู่ที่เดิม
            if (fromIndex >= 0 &&
                fromIndex < count &&
                menuButtons[fromIndex] != null &&
                menuButtons[fromIndex].interactable)
            {
                return fromIndex;
            }

            // ถ้าไม่มี currentIndex ให้หาปุ่มแรกที่กดได้
            for (int i = 0; i < count; i++)
            {
                if (menuButtons[i] != null &&
                    menuButtons[i].interactable)
                {
                    return i;
                }
            }

            return -1;
        }

        private void SelectIndex(int index)
        {
            if (menuButtons == null ||
                index < 0 ||
                index >= menuButtons.Count)
            {
                currentIndex = -1;
                HideSelector();
                return;
            }

            currentIndex = index;

            UpdateSelectorPosition();
        }

        /// <summary>
        /// เรียกจาก Hover Relay เมื่อเมาส์ชี้ปุ่ม
        /// </summary>
        private void OnPointerEnterButton(int index)
        {
            if (menuButtons == null ||
                index < 0 ||
                index >= menuButtons.Count)
            {
                return;
            }

            Button button = menuButtons[index];

            if (button == null || !button.interactable)
                return;

            SelectIndex(index);

            ResetUnityEventSystemSelection();
        }

        private void ActivateSelected()
        {
            if (menuButtons == null ||
                currentIndex < 0 ||
                currentIndex >= menuButtons.Count)
            {
                return;
            }

            Button target = menuButtons[currentIndex];

            if (target == null || !target.interactable)
                return;

            // ใช้ onClick เดิมของ Button
            target.onClick.Invoke();

            // ปิด selector ระหว่าง Transition
            HideSelector();
        }

        // ============================================================
        // SELECTOR VISUAL
        // ============================================================

        private void UpdateSelectorPosition()
        {
            if (currentIndex < 0 ||
                currentIndex >= menuButtons.Count)
            {
                return;
            }

            Button target = menuButtons[currentIndex];

            if (target == null)
                return;

            RectTransform buttonRect =
                target.GetComponent<RectTransform>();

            if (buttonRect == null)
                return;

            Vector3 center = buttonRect.position;

            float halfWidth =
                buttonRect.rect.width *
                buttonRect.lossyScale.x *
                0.5f;

            if (selectorLeft != null)
            {
                selectorLeft.gameObject.SetActive(true);

                selectorLeft.position =
                    center +
                    new Vector3(
                        -(halfWidth + selectorPadding),
                        0f,
                        0f
                    );
            }

            if (selectorRight != null)
            {
                selectorRight.gameObject.SetActive(true);

                selectorRight.position =
                    center +
                    new Vector3(
                        halfWidth + selectorPadding,
                        0f,
                        0f
                    );
            }
        }

        private void HideSelector()
        {
            if (selectorLeft != null)
                selectorLeft.gameObject.SetActive(false);

            if (selectorRight != null)
                selectorRight.gameObject.SetActive(false);
        }

        // ============================================================
        // CLICK SOUND
        // ============================================================

        private void AttachClickSoundListeners()
        {
            if (clickSoundAttached ||
                menuButtons == null)
            {
                return;
            }

            clickSoundAttached = true;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button button = menuButtons[i];

                if (button == null)
                    continue;

                button.onClick.AddListener(PlayClickSound);
            }
        }

        private void DetachClickSoundListeners()
        {
            if (menuButtons == null)
                return;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button button = menuButtons[i];

                if (button == null)
                    continue;

                button.onClick.RemoveListener(PlayClickSound);
            }
        }

        private void PlayClickSound()
        {
            if (audioSource != null &&
                clickSound != null)
            {
                audioSource.PlayOneShot(clickSound);
            }
        }

        // ============================================================
        // MOUSE HOVER RELAY
        // ============================================================

        private void AttachHoverRelays()
        {
            if (hoverRelaysAttached ||
                menuButtons == null)
            {
                return;
            }

            hoverRelaysAttached = true;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                Button button = menuButtons[i];

                if (button == null)
                    continue;

                HoverRelay relay =
                    button.gameObject.GetComponent<HoverRelay>();

                if (relay == null)
                {
                    relay =
                        button.gameObject.AddComponent<HoverRelay>();
                }

                relay.Setup(this, i);
            }
        }

        /// <summary>
        /// Component ภายในสำหรับส่ง Pointer Enter
        /// กลับมาที่ Navigator
        /// </summary>
        private class HoverRelay :
            MonoBehaviour,
            IPointerEnterHandler
        {
            private KeyboardMenuNavigator owner;
            private int index;

            public void Setup(
                KeyboardMenuNavigator owner,
                int index)
            {
                this.owner = owner;
                this.index = index;
            }

            public void OnPointerEnter(
                PointerEventData eventData)
            {
                owner?.OnPointerEnterButton(index);
            }
        }
    }
}