using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// เส้นเตือน (Danger Zone) - สร้าง LineRenderer ให้เองทั้งหมด ไม่ต้องทำ Prefab
    /// มี Pool ของตัวเองใน static Stack (ไม่ผูกกับ PoolManager)
    ///
    /// วิธีใช้ (ในโค้ด):
    ///   DangerLineV1 line = DangerLineV1.Show(points, count, width, color, sortingOrder);          // ค้างจนกว่าจะสั่ง Release
    ///   DangerLineV1 line = DangerLineV1.Show(..., autoReleaseSeconds: 0.6f);                      // หายเองเมื่อครบเวลา
    ///   line.Release();                                                                            // คืนเข้า Pool
    ///
    /// Show() คัดลอกตำแหน่งจุดลง LineRenderer ทันที ผู้เรียกจึงส่ง Array ที่ใช้ซ้ำ (Buffer) เข้ามาได้
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class DangerLineV1 : MonoBehaviour
    {
        private static readonly Stack<DangerLineV1> freeLines =
            new Stack<DangerLineV1>();

        private static Material sharedMaterial;

        private LineRenderer line;
        private bool inUse;
        private bool autoRelease;
        private float releaseTimer;

        private void Awake()
        {
            EnsureLine();
        }

        private void EnsureLine()
        {
            if (line != null)
                return;

            line = GetComponent<LineRenderer>();

            if (line == null)
                return;

            line.useWorldSpace = true;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
        }

        public static DangerLineV1 Show(
            Vector3[] points,
            int count,
            float width,
            Color color,
            int sortingOrder,
            float autoReleaseSeconds = -1f)
        {
            if (points == null || count < 2 || count > points.Length)
                return null;

            DangerLineV1 instance = Acquire();

            if (instance == null)
                return null;

            instance.Apply(points, count, width, color, sortingOrder, autoReleaseSeconds);
            return instance;
        }

        public void Release()
        {
            if (!inUse)
                return;

            inUse = false;
            autoRelease = false;

            gameObject.SetActive(false);
            freeLines.Push(this);
        }

        private void Update()
        {
            if (!inUse || !autoRelease)
                return;

            releaseTimer -= Time.deltaTime;

            if (releaseTimer <= 0f)
                Release();
        }

        private void Apply(
            Vector3[] points,
            int count,
            float width,
            Color color,
            int sortingOrder,
            float autoReleaseSeconds)
        {
            EnsureLine();

            Material material = GetMaterial();

            if (material != null)
                line.sharedMaterial = material;

            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;

            line.positionCount = count;

            for (int i = 0; i < count; i++)
                line.SetPosition(i, points[i]);

            inUse = true;
            autoRelease = autoReleaseSeconds > 0f;
            releaseTimer = autoReleaseSeconds;
        }

        private static DangerLineV1 Acquire()
        {
            // ข้ามตัวที่ถูกทำลายไปแล้ว (เช่น เปลี่ยนซีน) - Unity null check
            while (freeLines.Count > 0)
            {
                DangerLineV1 candidate = freeLines.Pop();

                if (candidate != null)
                {
                    candidate.gameObject.SetActive(true);
                    return candidate;
                }
            }

            GameObject lineObject = new GameObject("DangerLine");
            return lineObject.AddComponent<DangerLineV1>();
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("UI/Default");

            if (shader != null)
                sharedMaterial = new Material(shader);

            return sharedMaterial;
        }
    }
}