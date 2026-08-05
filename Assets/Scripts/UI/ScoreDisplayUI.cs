using UnityEngine;
using TMPro;

namespace GameSystem
{
    /// <summary>
    /// แสดงผล Score หรือ HiScore เป็นตัวเลข - ใช้สคริปต์เดียวกันได้ทั้ง 2 แบบ
    /// เลือกโหมดผ่าน Inspector แทนที่จะแยกไฟล์ ScoreDisplayUI/HiScoreDisplayUI ซ้ำกัน 2 ไฟล์
    /// เพราะ logic การแสดงผล (format ตัวเลข, subscribe event) เหมือนกันทุกอย่าง ต่างกันแค่ "ฟัง event ไหน"
    ///
    /// ตัวเลขบนจอไม่กระโดดไปค่าจริงทันทีตอน event ยิง - ไล่ขึ้นทีละนิดใน Update() แทน (ฟีลแบบเกมยิงคลาสสิก)
    /// ใช้ pattern เดียวกับ timer/state field ที่ EnemyV1/PlayerControllerV1 ใช้อยู่แล้วทั้งไฟล์ (ไม่ใช้ Coroutine)
    /// </summary>
    public class ScoreDisplayUI : MonoBehaviour
    {
        private enum DisplayMode
        {
            Score,
            HiScore
        }

        [SerializeField] private TMP_Text targetText;
        [SerializeField] private DisplayMode mode = DisplayMode.Score;

        [Tooltip("จำนวนหลักขั้นต่ำ - เติม 0 นำหน้าให้ครบ แบบเกมยิงคลาสสิก เช่น 8 หลัก = 00012300")]
        [SerializeField] private int digitPadding = 8;

        [Header("Roll Animation")]
        [Tooltip("ความเร็วไล่ตัวเลขเข้าใกล้ค่าจริง (หน่วย: แต้ม/วินาที) " +
                 "ยิ่งค่าน้อยยิ่งไล่ช้า/เห็นเอฟเฟกต์ชัด ตั้งสูงมากๆ (เช่น 999999) จะเหมือนกระโดดทันทีแบบเดิม")]
        [SerializeField] private float rollSpeed = 500f;

        // ค่าที่ "โชว์อยู่บนจอตอนนี้" กับ "ค่าจริงล่าสุดจาก ScoreManager" แยกกันตั้งใจ
        // event จาก ScoreManager จะอัปเดต targetValue ทันที ส่วน displayedValue ให้ Update() ค่อยๆ ไล่ตามทีละเฟรมแทน
        private int displayedValue;
        private int targetValue;

        void Start()
        {
            // สำคัญ: subscribe ใน Start() ไม่ใช่ Awake()
            // เหตุผล: Unity รับประกันว่า Awake() ของทุก object ใน scene จะรันจบหมดก่อน Start() ของใครจะเริ่ม
            // ดังนั้นถ้า ScoreManager.Instance ถูกสร้างใน Awake ของมันเอง พอมาถึง Start() ของสคริปต์นี้
            // Instance จะพร้อมใช้แน่นอน 100% - ถ้า subscribe ใน Awake() แทน อาจชนกรณี Awake ของสคริปต์นี้
            // รันก่อน Awake ของ ScoreManager แล้ว Instance ยังเป็น null อยู่ (ลำดับ Awake ระหว่างคนละ object ไม่การันตี)
            if (ScoreManager.Instance == null)
            {
                Debug.LogWarning("ScoreDisplayUI: ไม่พบ ScoreManager ใน scene - ต้องมี GameObject ที่ติด ScoreManager อยู่ก่อน");
                return;
            }

            int initialValue = mode == DisplayMode.Score
                ? ScoreManager.Instance.CurrentScore
                : ScoreManager.Instance.HiScore;

            // sync ค่าเริ่มต้นแบบไม่ต้องไล่แอนิเมชัน (กันตัวเลขวิ่งจาก 0 ทุกครั้งที่เพิ่งเปิดฉาก/เปิดเกม)
            displayedValue = initialValue;
            targetValue = initialValue;
            RenderText(displayedValue);

            if (mode == DisplayMode.Score)
                ScoreManager.Instance.OnScoreChanged += SetTargetValue;
            else
                ScoreManager.Instance.OnHiScoreChanged += SetTargetValue;
        }

        void OnDestroy()
        {
            // unsubscribe กัน memory leak / NullReferenceException ถ้า UI ถูกทำลายไปแล้วแต่ event ยังถูกยิงมาอยู่
            if (ScoreManager.Instance == null) return;

            if (mode == DisplayMode.Score)
                ScoreManager.Instance.OnScoreChanged -= SetTargetValue;
            else
                ScoreManager.Instance.OnHiScoreChanged -= SetTargetValue;
        }

        // event handler จำค่าเป้าหมายไว้เฉยๆ ไม่แตะ UI ตรงๆ ณ จุดนี้ - ให้ Update() เป็นคนไล่แสดงผลแทนทีละเฟรม
        private void SetTargetValue(int value)
        {
            targetValue = value;
        }

        void Update()
        {
            if (displayedValue == targetValue) return; // ค่าตรงกันแล้ว ไม่ต้องทำอะไร ประหยัด ไม่ต้อง SetText ทุกเฟรมโดยไม่จำเป็น

            // Mathf.MoveTowards เดินเป็นเส้นตรงคงที่ (ต่างจาก Lerp ที่ช้าลงเรื่อยๆ ตอนใกล้ปลายทาง แล้วไม่มีวันถึงเป๊ะ)
            // ทำให้ displayedValue ไปถึง targetValue จริงในเวลาที่คาดเดาได้เสมอ ตรงกับฟีล digit-roll ของเกมยิงคลาสสิก
            float step = rollSpeed * Time.deltaTime;
            displayedValue = (int)Mathf.MoveTowards(displayedValue, targetValue, step);

            RenderText(displayedValue);
        }

        private void RenderText(int value)
        {
            if (targetText == null) return;
            targetText.text = value.ToString().PadLeft(digitPadding, '0');
        }
    }
}