using System;
using UnityEngine;

namespace MenuSystemV1
{
    /// <summary>
    /// รูปแบบการ Ease ตอนเคลื่อนที่ - เลือกได้ใน Inspector โดยไม่ต้องยุ่งกับ AnimationCurve
    /// </summary>
    public enum BoxMoveEase
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad
    }

    /// <summary>
    /// ควบคุมกล่องดำ (World Space Sprite - ใช้ SpriteRenderer) ที่ใช้เป็น Transition
    /// ตอนเข้า/ออกจากหน้า Main Menu
    ///
    /// พฤติกรรม:
    /// - ตอน Scene เริ่มต้น (ใน Awake()) จะเคลื่อนจากตำแหน่งตัวเองไปยัง endPoint ให้อัตโนมัติ
    ///   ถ้าเปิด revealOnStart ไว้ (ค่าเริ่มต้น = true)
    /// - เมื่อเรียก MoveToStart() จะเคลื่อนกลับไปยัง Start Position
    ///   (ตำแหน่งของ startPoint ถ้ากำหนดไว้ ไม่งั้นใช้ตำแหน่งตัวเองตอน Start() เป็นค่าเริ่มต้น)
    /// - ระหว่างเคลื่อนที่ IsMoving = true - ผู้เรียกใช้ (เช่น MenuManager)
    ///   ต้องเอาไปเช็คเพื่อบล็อคปุ่มกดเอง (Component นี้ไม่ยุ่งกับ UI โดยตรง)
    /// - ไม่ใช้ Coroutine - เดินผ่าน Update() ตาม convention ของโปรเจกต์ (ใช้ Time.unscaledDeltaTime)
    ///
    /// Setup:
    /// 1. แปะ Component นี้ไว้บน GameObject ที่มี SpriteRenderer เป็นกล่องดำ
    /// 2. สร้าง Empty GameObject ไว้ตำแหน่งที่ต้องการให้กล่องเคลื่อนไปหา (ตอนเปิดจอ/เผยหน้าเมนู) ลากใส่ endPoint
    /// 3. (ไม่บังคับ) ถ้าต้องการกำหนดตำแหน่งเริ่มต้นเอง แทนที่จะใช้ตำแหน่งตัวเอง ให้ลาก Transform ใส่ startPoint
    /// </summary>
    [DisallowMultipleComponent]
    public class TransitionBoxController : MonoBehaviour
    {
        private enum MoveState
        {
            Idle,
            MovingToEnd,
            MovingToStart
        }

        [Header("Points")]
        [Tooltip("ตำแหน่งเริ่มต้น - ถ้าไม่ใส่ จะใช้ตำแหน่งของตัว GameObject เอง ณ ตอน Start() เป็นค่าเริ่มต้น")]
        [SerializeField] private Transform startPoint;

        [Tooltip("ตำแหน่งปลายทาง (ตำแหน่งตอนเปิดเผยหน้าเมนู) - สร้าง Empty GameObject ไว้แล้วลากมาใส่")]
        [SerializeField] private Transform endPoint;

        [Header("Movement")]
        [Min(0.01f)]
        [Tooltip("ระยะเวลาที่ใช้เคลื่อนที่ (วินาที) - ใช้ Time.unscaledDeltaTime")]
        [SerializeField] private float moveDuration = 0.6f;

        [SerializeField] private BoxMoveEase easeType = BoxMoveEase.EaseOutQuad;

        [Tooltip(
            "จำกัดเวลาสูงสุดที่นับต่อ 1 เฟรม (วินาที) - กันปัญหาเฟรมแรกหลังเข้า Scene " +
            "ที่ deltaTime มักกระโดดสูงผิดปกติ (เพราะ Unity กำลังโหลด Asset อยู่พอดี) " +
            "ซึ่งถ้าไม่ Clamp ไว้ กล่องจะเหมือนเทเลพอร์ตไปปลายทางเลยในเฟรมเดียว แทนที่จะ Ease ปกติ"
        )]
        [SerializeField] private float maxDeltaTimePerFrame = 0.05f;

        [Header("Behaviour")]
        [Tooltip("ถ้าเปิดไว้ พอเข้า Scene ปุ๊บ กล่องจะเคลื่อนไปยัง endPoint ให้อัตโนมัติทันที (Reveal เมนู)")]
        [SerializeField] private bool revealOnStart = true;

        /// <summary>ยิงตอนกล่องเคลื่อนถึง endPoint (เผยหน้าเมนูเสร็จแล้ว)</summary>
        public event Action OnArrivedAtEnd;

        /// <summary>ยิงตอนกล่องเคลื่อนกลับถึง startPoint (ปิดจอเสร็จแล้ว - พร้อมโหลด Scene ถัดไป)</summary>
        public event Action OnArrivedAtStart;

        /// <summary>กำลังเคลื่อนที่อยู่หรือไม่ - ผู้เรียกใช้ต้องเช็คก่อนรับ Input ใดๆ</summary>
        public bool IsMoving => state != MoveState.Idle;

        /// <summary>สะท้อนค่า revealOnStart ให้ผู้เรียกใช้ภายนอกอ่านได้ (เช่น MenuManager เอาไปตัดสินใจล็อกปุ่มตอนเริ่ม Scene)</summary>
        public bool RevealOnStart => revealOnStart;

        private MoveState state = MoveState.Idle;
        private Vector3 startPos;
        private Vector3 endPos;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private float timer;

        private void Awake()
        {
            // จับตำแหน่งเริ่มต้น/ปลายทางไว้ครั้งเดียวตอน Scene เริ่ม
            // ย้ายมาไว้ที่ Awake() แทน Start() เพื่อให้ทำงานเร็วขึ้นหนึ่งจังหวะ
            startPos = startPoint != null ? startPoint.position : transform.position;
            endPos = endPoint != null ? endPoint.position : transform.position;

            Debug.Log(
                $"[TransitionBoxController] Awake() บน '{gameObject.name}' - " +
                $"activeInHierarchy={gameObject.activeInHierarchy}, enabled={enabled}, " +
                $"revealOnStart={revealOnStart}, startPos={startPos}, endPos={endPos}, " +
                $"endPoint={(endPoint != null ? endPoint.name : "NULL")}",
                this);

            if (endPoint == null)
            {
                Debug.LogWarning(
                    "TransitionBoxController: ยังไม่ได้กำหนด endPoint - " +
                    "กล่องจะไม่เคลื่อนที่ไปไหนเลย", this);
            }

            // สำคัญ: Snap กล่องไปที่ startPos ก่อนเสมอ (ถ้ากำหนด startPoint ไว้)
            // เผื่อกรณีตำแหน่งที่วาง GameObject ไว้ตอนแต่ง Scene ไม่ตรงกับ startPos จริง
            // (เช่น วางไว้ใกล้ๆ ตำแหน่ง End เพื่อความสะดวกตอนจัด Layout ใน Editor)
            // ถ้าไม่ Snap ก่อน ตอน BeginMove() จะอ่าน transform.position ปัจจุบันผิดตำแหน่ง
            // ทำให้ moveFrom กับ moveTo ดันเป็นค่าเดียวกัน และดูเหมือนกล่องไม่ขยับเลย
            if (startPoint != null)
                transform.position = startPos;

            if (revealOnStart)
                MoveToEnd();
        }

        private void Update()
        {
            if (state == MoveState.Idle)
                return;

            // Clamp deltaTime ต่อเฟรม กันเฟรมแรก/เฟรมกระตุกที่ Spike สูงผิดปกติ
            // ทำให้ Animation เหมือนเทเลพอร์ตแทนที่จะ Ease ไปตามเวลาปกติ
            float dt = Mathf.Min(Time.unscaledDeltaTime, maxDeltaTimePerFrame);
            timer += dt;

            float t = moveDuration > 0f
                ? Mathf.Clamp01(timer / moveDuration)
                : 1f;

            float eased = ApplyEase(t);
            transform.position = Vector3.LerpUnclamped(moveFrom, moveTo, eased);

            if (t >= 1f)
                FinishMove();
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>เคลื่อนกล่องไปยังตำแหน่งปลายทาง (เผยหน้าเมนู)</summary>
        public void MoveToEnd()
        {
            BeginMove(toEnd: true);
        }

        /// <summary>เคลื่อนกล่องกลับไปยังตำแหน่งเริ่มต้น (ปิดจอ ก่อนเปลี่ยน Scene)</summary>
        public void MoveToStart()
        {
            BeginMove(toEnd: false);
        }

        // ============================================================
        // INTERNAL
        // ============================================================

        private void BeginMove(bool toEnd)
        {
            // ถ้ากำลังเคลื่อนที่อยู่แล้ว ให้เริ่มนับใหม่จากตำแหน่งปัจจุบัน กัน Snap กระชาก
            moveFrom = transform.position;
            moveTo = toEnd ? endPos : startPos;
            timer = 0f;
            state = toEnd ? MoveState.MovingToEnd : MoveState.MovingToStart;

            Debug.Log(
                $"[TransitionBoxController] BeginMove(toEnd={toEnd}) บน '{gameObject.name}' - " +
                $"moveFrom={moveFrom} -> moveTo={moveTo}, duration={moveDuration}",
                this);
        }

        private void FinishMove()
        {
            transform.position = moveTo;

            MoveState finishedState = state;
            state = MoveState.Idle;

            Debug.Log(
                $"[TransitionBoxController] FinishMove บน '{gameObject.name}' - " +
                $"finishedState={finishedState}, ตำแหน่งสุดท้าย={transform.position}",
                this);

            if (finishedState == MoveState.MovingToEnd)
                OnArrivedAtEnd?.Invoke();
            else if (finishedState == MoveState.MovingToStart)
                OnArrivedAtStart?.Invoke();
        }

        private float ApplyEase(float t)
        {
            switch (easeType)
            {
                case BoxMoveEase.Linear:
                    return t;

                case BoxMoveEase.EaseInQuad:
                    return t * t;

                case BoxMoveEase.EaseOutQuad:
                    return 1f - (1f - t) * (1f - t);

                case BoxMoveEase.EaseInOutQuad:
                    return t < 0.5f
                        ? 2f * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default:
                    return t;
            }
        }
    }
}