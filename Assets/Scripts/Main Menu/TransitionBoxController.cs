using System;
using UnityEngine;

namespace MenuSystemV1
{
    /// <summary>
    /// รูปแบบ Ease ของ Transition Box
    /// </summary>
    public enum BoxMoveEase
    {
        Linear,
        EaseInQuad,
        EaseOutQuad,
        EaseInOutQuad
    }

    /// <summary>
    /// ควบคุม Transition Box แบบ World Space Sprite
    ///
    /// หน้าที่:
    /// - เปิดเผย Main Menu ด้วยการ Move Start -> End
    /// - ปิด Main Menu ด้วยการ Move End -> Start
    /// - ใช้ Unscaled Time
    /// - มี API สำหรับตรวจตำแหน่งปัจจุบัน
    /// - มี API สำหรับ Reset state หลัง Scene ถูกโหลดใหม่
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

        [SerializeField]
        private Transform startPoint;

        [SerializeField]
        private Transform endPoint;

        [Header("Movement")]

        [Min(0.01f)]
        [SerializeField]
        private float moveDuration = 0.6f;

        [SerializeField]
        private BoxMoveEase easeType =
            BoxMoveEase.EaseOutQuad;

        [SerializeField]
        private float maxDeltaTimePerFrame = 0.05f;

        [Header("Behaviour")]

        [SerializeField]
        private bool revealOnStart = true;

        // ============================================================
        // EVENTS
        // ============================================================

        public event Action OnArrivedAtEnd;
        public event Action OnArrivedAtStart;

        // ============================================================
        // PUBLIC STATE
        // ============================================================

        public bool IsMoving =>
            state != MoveState.Idle;

        public bool RevealOnStart =>
            revealOnStart;

        /// <summary>
        /// กล่องอยู่ที่ตำแหน่ง End แล้วหรือไม่
        /// </summary>
        public bool IsAtEnd =>
            state == MoveState.Idle &&
            IsApproximatelyAt(
                transform.position,
                endPos
            );

        /// <summary>
        /// กล่องอยู่ที่ตำแหน่ง Start แล้วหรือไม่
        /// </summary>
        public bool IsAtStart =>
            state == MoveState.Idle &&
            IsApproximatelyAt(
                transform.position,
                startPos
            );

        // ============================================================
        // RUNTIME
        // ============================================================

        private MoveState state =
            MoveState.Idle;

        private Vector3 startPos;
        private Vector3 endPos;

        private Vector3 moveFrom;
        private Vector3 moveTo;

        private float timer;

        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            startPos =
                startPoint != null
                    ? startPoint.position
                    : transform.position;

            endPos =
                endPoint != null
                    ? endPoint.position
                    : transform.position;

            if (startPoint != null)
                transform.position = startPos;

            state =
                MoveState.Idle;

            timer = 0f;

            if (endPoint == null)
            {
                Debug.LogWarning(
                    "TransitionBoxController: " +
                    "ยังไม่ได้กำหนด endPoint",
                    this
                );
            }

            // --------------------------------------------------------
            // เปิด Main Menu
            // --------------------------------------------------------

            if (revealOnStart)
                MoveToEnd();
        }

        private void Update()
        {
            if (state == MoveState.Idle)
                return;

            float dt =
                Mathf.Min(
                    Time.unscaledDeltaTime,
                    maxDeltaTimePerFrame
                );

            timer += dt;

            float t =
                moveDuration > 0f
                    ? Mathf.Clamp01(
                        timer /
                        moveDuration
                    )
                    : 1f;

            float eased =
                ApplyEase(t);

            transform.position =
                Vector3.LerpUnclamped(
                    moveFrom,
                    moveTo,
                    eased
                );

            if (t >= 1f)
                FinishMove();
        }

        // ============================================================
        // PUBLIC API
        // ============================================================

        public void MoveToEnd()
        {
            BeginMove(true);
        }

        public void MoveToStart()
        {
            BeginMove(false);
        }

        /// <summary>
        /// บังคับกล่องกลับ Start ทันที
        /// ใช้สำหรับ Reset ตอนเริ่ม/จบ Menu lifecycle
        /// </summary>
        public void ResetToStart()
        {
            transform.position = startPos;

            moveFrom = startPos;
            moveTo = startPos;

            timer = 0f;

            state =
                MoveState.Idle;
        }

        /// <summary>
        /// บังคับกล่องไป End ทันที
        /// </summary>
        public void ResetToEnd()
        {
            transform.position = endPos;

            moveFrom = endPos;
            moveTo = endPos;

            timer = 0f;

            state =
                MoveState.Idle;
        }

        // ============================================================
        // INTERNAL
        // ============================================================

        private void BeginMove(bool toEnd)
        {
            Vector3 destination =
                toEnd
                    ? endPos
                    : startPos;

            moveFrom =
                transform.position;

            moveTo =
                destination;

            timer = 0f;

            state =
                toEnd
                    ? MoveState.MovingToEnd
                    : MoveState.MovingToStart;
        }

        private void FinishMove()
        {
            transform.position =
                moveTo;

            MoveState finishedState =
                state;

            state =
                MoveState.Idle;

            timer = 0f;

            if (finishedState ==
                MoveState.MovingToEnd)
            {
                OnArrivedAtEnd?.Invoke();
            }
            else if (finishedState ==
                     MoveState.MovingToStart)
            {
                OnArrivedAtStart?.Invoke();
            }
        }

        private static bool IsApproximatelyAt(
            Vector3 a,
            Vector3 b)
        {
            return
                Vector3.SqrMagnitude(a - b)
                <= 0.0001f;
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
                    return
                        1f -
                        (1f - t) *
                        (1f - t);

                case BoxMoveEase.EaseInOutQuad:
                    return
                        t < 0.5f
                            ? 2f * t * t
                            : 1f -
                              Mathf.Pow(
                                  -2f * t + 2f,
                                  2f
                              ) /
                              2f;

                default:
                    return t;
            }
        }
    }
}