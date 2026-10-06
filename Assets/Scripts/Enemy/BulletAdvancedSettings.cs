using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    // ============================================================
    // ENUMS
    // ============================================================

    /// <summary>
    /// พฤติกรรมของกระสุนหลังวนรอบศูนย์กลางครบเวลา
    /// Tangent = พุ่งออกตามเส้นสัมผัสวงโคจร (ทิศที่กำลังวิ่งอยู่) แล้วใช้ความเร็วปกติของกระสุน
    /// Outward = พุ่งออกนอกวง (ทิศจากศูนย์กลางไปหากระสุน)
    /// Stop    = หยุดนิ่งอยู่ตรงจุดที่วนถึง (ความเร็วเป็น 0)
    /// </summary>
    public enum OrbitReleaseMode
    {
        Tangent,
        Outward,
        Stop
    }

    /// <summary>
    /// FlowAlongPath   = กระสุนทุกนัดเกิดที่จุดเริ่ม Path แล้วไหลตาม Path ไปทีละนัด (เป็นสายกระสุนต่อกันตามเส้น)
    /// SweepSpawnPoint = จุด Spawn เดินไปตาม Path - กระสุนแต่ละนัดโผล่คนละตำแหน่งบนเส้น
    ///                   แล้วบินตามทิศ/ความเร็วของ Element ตามปกติ (เหมือนกวางยิงไปตามเส้น)
    /// </summary>
    public enum PathStreamMode
    {
        FlowAlongPath,
        SweepSpawnPoint
    }

    /// <summary>
    /// ElementSpawn    = จุดเริ่มคือตำแหน่ง Spawn ปกติของ Element (OwnerCenter/Offset) แล้วต่อไปยัง Waypoints
    /// FirstWaypoint   = จุดเริ่มคือ Waypoint ตัวแรกในรายการ
    /// CustomTransform = จุดเริ่มคือ Transform ที่ลากมาใส่ (อ่านตำแหน่งสดทุกครั้งที่ Spawn) ถ้าไม่ได้ลากใส่จะใช้ FirstWaypoint แทน
    /// </summary>
    public enum PathStreamStartMode
    {
        ElementSpawn,
        FirstWaypoint,
        CustomTransform
    }

    // ============================================================
    // SETTINGS (Serializable)
    // ============================================================

    /// <summary>
    /// กระสุนเลี้ยวเข้าหาผู้เล่นต่อเนื่องในช่วงเวลาที่กำหนด (ต่างจาก Aim At Player ใน Direction Timeline
    /// ที่คำนวณทิศครั้งเดียวตอนเริ่ม Redirect)
    /// ระหว่างที่ Homing ทำงาน Direction Timeline จะหยุดชั่วคราวแล้วเดินต่อหลัง Homing จบ
    /// </summary>
    [Serializable]
    public class HomingSettings
    {
        public bool enabled = false;

        [Tooltip("เริ่ม Homing หลังกระสุนเริ่มเคลื่อนที่ไปกี่วินาที")]
        [Min(0f)] public float startDelay = 0f;

        [Tooltip("Homing ทำงานนานกี่วินาที (หลังผ่าน Start Delay)")]
        [Min(0.01f)] public float duration = 2f;

        [Tooltip("ความเร็วเลี้ยวสูงสุด (องศา/วินาที) - ยิ่งน้อยยิ่งหลบง่าย\n0 = ล็อกเป้าทันที (หลบยากมาก)")]
        [Min(0f)] public float turnSpeed = 90f;

        [Tooltip("รักษามุมเบี่ยงจาก Spread ตอน Homing: 0 = ทุกนัดหันหา Player จุดเดียวกัน | 1 = รักษามุมเบี่ยงเดิมเต็มที่")]
        [Range(0f, 1f)] public float keepSpread = 0f;
    }

    /// <summary>
    /// กระสุนวนรอบศูนย์กลาง (ตัวที่ยิง Pattern นี้ เช่น Boss/Enemy หรือกระสุนที่เรียก Burst Trigger)
    /// ระหว่างวน Burst Timeline ของกระสุนยังนับเวลาต่อ จึงใช้เป็นตัวปล่อยกระสุนเพิ่มได้
    /// ใช้ร่วมกับ Path Motion / Path Stream แบบ Flow ไม่ได้ (Path ชนะ)
    /// </summary>
    [Serializable]
    public class OrbitSettings
    {
        public bool enabled = false;

        [Tooltip("รัศมีวงโคจร - กระสุนจะค่อยๆ เคลื่อนจากจุด Spawn มาที่รัศมีนี้ภายใน Radius Blend Time")]
        [Min(0f)] public float radius = 1.5f;

        [Tooltip("ความเร็วหมุน (องศา/วินาที) + = ทวนเข็มนาฬิกา, - = ตามเข็มนาฬิกา")]
        public float angularSpeed = 180f;

        [Tooltip("วนนานกี่วินาที แล้วปล่อยตัวตาม Release Mode")]
        [Min(0.01f)] public float duration = 2f;

        [Tooltip("เวลาที่ใช้เลื่อนจากจุด Spawn เข้าสู่รัศมีวงโคจร (0 = ย้ายทันที)")]
        [Min(0f)] public float radiusBlendTime = 0.25f;

        [Header("Spread Compatibility")]
        [Tooltip("เพิ่ม/ลด Radius แบบสุ่มตามค่านี้ - 0 = ไม่เปลี่ยน")]
        [Min(0f)] public float radiusVary = 0f;

        [Tooltip("Scope ของ Radius Vary: PerPattern / PerBurst / PerShot")]
        public RandomScope radiusVaryScope = RandomScope.PerShot;

        [Tooltip("เพิ่ม/ลดมุมเริ่ม Orbit แบบสุ่ม (องศา) - 0 = ไม่เปลี่ยน")]
        [Min(0f)] public float startAngleVary = 0f;

        [Tooltip("Scope ของ Start Angle Vary: PerPattern / PerBurst / PerShot")]
        public RandomScope startAngleVaryScope = RandomScope.PerShot;

        public OrbitReleaseMode releaseMode = OrbitReleaseMode.Tangent;
    }

    /// <summary>
    /// Danger Zone - เส้นสีแดงบอกเส้นทางล่วงหน้าก่อนกระสุนเริ่มเคลื่อนที่
    /// กระสุนจะ Spawn แล้วซ่อน/ไม่ชน/ไม่เคลื่อนที่ ระหว่างที่เส้นแสดงอยู่ พอครบ Duration จึงเริ่มวิ่ง
    /// เส้นที่วาดได้: เส้นตรง (ทิศเริ่มต้น), Path Motion, Path Stream แบบ Flow
    /// ถ้าใช้กับ Path Stream: เส้นจะวาดทั้ง Path ครั้งเดียวตอนเริ่ม Stream แล้ว Stream รอ Duration ก่อนเริ่ม Spawn
    /// </summary>
    [Serializable]
    public class TelegraphSettings
    {
        public bool enabled = false;

        [Tooltip("เวลาแสดงเส้นเตือนก่อนกระสุนเริ่มวิ่ง (วินาที)")]
        [Min(0.05f)] public float duration = 0.6f;

        [Tooltip("ความหนาเส้น (หน่วยโลก)")]
        [Min(0.005f)] public float lineWidth = 0.08f;

        public Color color = new Color(1f, 0.1f, 0.1f, 0.8f);

        [Tooltip("ความยาวเส้นตรง (ใช้เฉพาะกระสุนที่ไม่ได้ใช้ Path - Path จะวาดตามความยาวจริงของเส้น)")]
        [Min(0.1f)] public float lineLength = 12f;

        [Tooltip("Sorting Order ของเส้น - ตั้งให้สูงกว่าพื้นหลัง")]
        public int sortingOrder = 100;

        [Tooltip("ติ๊ก = แสดง Danger Line เพียงครั้งเดียวใน BCP และเฉพาะ Repeat แรก (กระสุนหลัก/ทิศหลัก) | ปิด = พฤติกรรมเดิม")]
        public bool showOncePerPattern = false;
    }

    /// <summary>
    /// Path Stream - Spawn กระสุนต่อเนื่องตามช่วงเวลา โดยอ้างอิงเส้นทางที่สร้างจาก Transform หลายตัวในซีน
    /// ใช้ Bullet Prefab / Direction / Motion / Modifiers ของ Element เดิมทุกอย่าง
    /// ตำแหน่งของ Transform ทุกตัว (รวม Start Transform) ถูกอ่านสดทุกครั้งที่ Spawn - ถ้าลาก Boss มาเป็นจุดเริ่ม
    /// เส้นก็ตามตัว Boss
    /// หมายเหตุ: Burst 1 Repeat = Stream 1 สาย ถ้า Repeat Count > 1 และ Repeat Interval สั้นกว่า Stream Duration สายจะซ้อนกัน
    /// </summary>
    [Serializable]
    public class PathStreamSettings
    {
        public bool enabled = false;

        public PathStreamMode mode = PathStreamMode.FlowAlongPath;

        public PathStreamStartMode startMode = PathStreamStartMode.FirstWaypoint;

        [Tooltip("ใช้เมื่อ Start Mode = Custom Transform - ลาก Transform ที่อยากให้เป็นจุดเริ่มมาใส่ (อ่านตำแหน่งสดทุกครั้งที่ Spawn)")]
        public Transform startTransform;

        [Tooltip("จุดผ่านของ Path เรียงตามลำดับในรายการ - เลือก Empty GameObject หลายตัวแล้วลากมาวางทีเดียวได้")]
        public List<Transform> waypoints = new List<Transform>();

        [Tooltip("ติ๊ก = เส้นโค้ง Spline ผ่านทุกจุด | ไม่ติ๊ก = เส้นตรงต่อกันทีละจุด")]
        public bool useCurve = false;

        [Tooltip("Spawn ต่อเนื่องนานกี่วินาที")]
        [Min(0f)] public float streamDuration = 2f;

        [Tooltip("ห่างกันกี่วินาทีต่อการ Spawn 1 ครั้ง")]
        [Min(0.01f)] public float spawnInterval = 0.05f;

        [Tooltip(
            "Flow: ความเร็วที่กระสุนไหลตาม Path (หน่วย/วินาที) - 0 = ใช้ความเร็วของกระสุน (Initial Speed หรือ Base Speed)\n" +
            "Sweep: ความเร็วที่จุด Spawn เดินไปตาม Path - 0 = เดินให้ถึงปลาย Path พอดีเมื่อครบ Stream Duration")]
        [Min(0f)] public float pathSpeed = 8f;

        [Tooltip("Path Speed = ใช้ Path Speed เดิม | Speed Timeline = ใช้ Speed Timeline ของ Element เดียวกันควบคุมความเร็วจริงของ Flow โดย Target Speed เป็นหน่วย/วินาที")]
        public PathSpeedMode speedMode = PathSpeedMode.PathTiming;

        [Header("Spread Compatibility (Flow only)")]
        [Tooltip("ติ๊ก = หมุนทั้ง Path รอบจุดเริ่มตามมุม Spread ของแต่ละนัด | ปิด = Path เดิม")]
        public bool rotateWithSpread = false;

        [Tooltip("None = ไม่แยก | Random = สุ่ม Lane Offset | FollowSpread = คำนวณจาก Spread แบบแน่นอน")]
        public PathVaryMode varyMode = PathVaryMode.None;

        [Tooltip("ระยะ Offset สูงสุดด้านข้าง Path (หน่วยโลก) - 0 = ไม่แยก")]
        [Min(0f)] public float varyAmount = 0f;

        [Tooltip("Scope ของ Random Vary: PerPattern / PerBurst / PerShot")]
        public RandomScope varyScope = RandomScope.PerShot;

        [Tooltip("ใช้กับ Flow - พฤติกรรมของกระสุนเมื่อไหลถึงปลาย Path")]
        public PathEndBehavior endBehavior = PathEndBehavior.ContinueFlying;

        /// <summary>
        /// เก็บจุดควบคุมตามลำดับ [จุดเริ่ม (ถ้ามี)] + Waypoints (ข้าม null) ลง output
        /// อ่านตำแหน่งสดของ Transform ณ ตอนเรียกนี้
        /// </summary>
        public int CollectPoints(Vector3 elementSpawn, List<Vector3> output)
        {
            output.Clear();

            switch (startMode)
            {
                case PathStreamStartMode.ElementSpawn:
                    output.Add(elementSpawn);
                    break;

                case PathStreamStartMode.CustomTransform:
                    if (startTransform != null)
                        output.Add(startTransform.position);
                    break;
            }

            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    if (waypoints[i] != null)
                        output.Add(waypoints[i].position);
                }
            }

            return output.Count;
        }
    }

    // ============================================================
    // RUNTIME DATA ส่งให้ EnemyBulletV2
    // ============================================================

    /// <summary>
    /// ตัวเลือกขั้นสูงที่ Executor ส่งให้กระสุนตอน ConfigureMotion (struct = ไม่สร้าง GC ต่อกระสุน)
    /// ค่า default ทั้งหมดคือไม่ใช้ฟีเจอร์ใดเลย
    /// </summary>
    public struct BulletAdvancedConfig
    {
        public TelegraphSettings telegraph;
        public HomingSettings homing;
        public OrbitSettings orbit;
        public Transform orbitCenter;
        public float orbitRadiusOffset;
        public float orbitStartAngleOffset;
        public float homingSpreadAngle;
        public float homingKeepSpread;

        public WaypointPath waypointPath;
        public float waypointSpeed;
        public PathSpeedMode waypointSpeedMode;
        public PathEndBehavior waypointEnd;
        public float waypointRotation;
        public Vector3 waypointOffset;
    }

    /// <summary>
    /// เส้นทางที่ Sample แล้ว (เส้นตรงต่อกัน หรือ Catmull-Rom Spline) พร้อมความยาวสะสม
    /// ใช้หาตำแหน่ง ณ ระยะทางที่เดินไปแล้ว ทำให้ความเร็วคงที่ไม่ว่าเส้นจะโค้งหรือสั้นยาวแค่ไหน
    /// สร้างใหม่ทุกครั้งที่ Spawn (อ่านตำแหน่ง Transform สด) กระสุนทุกนัดใน Tick เดียวกันแชร์ตัวเดียวกัน
    /// </summary>
    public sealed class WaypointPath
    {
        private readonly Vector3[] points;
        private readonly float[] cumulative;

        public Vector3[] Points => points;
        public int Count => points.Length;
        public float TotalLength { get; }
        public Vector3 Start => points[0];

        private WaypointPath(Vector3[] points, float[] cumulative)
        {
            this.points = points;
            this.cumulative = cumulative;
            TotalLength = cumulative[cumulative.Length - 1];
        }

        public static WaypointPath Build(
            IList<Vector3> control,
            bool smooth,
            int samplesPerSegment = 12)
        {
            if (control == null || control.Count == 0)
                return null;

            int n = control.Count;
            List<Vector3> samples = new List<Vector3>(
                smooth && n >= 3 ? (n - 1) * samplesPerSegment + 1 : n);

            if (!smooth || n < 3)
            {
                for (int i = 0; i < n; i++)
                    samples.Add(control[i]);
            }
            else
            {
                samplesPerSegment = Mathf.Max(2, samplesPerSegment);
                samples.Add(control[0]);

                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 p0 = control[Mathf.Max(i - 1, 0)];
                    Vector3 p1 = control[i];
                    Vector3 p2 = control[i + 1];
                    Vector3 p3 = control[Mathf.Min(i + 2, n - 1)];

                    for (int s = 1; s <= samplesPerSegment; s++)
                    {
                        float t = (float)s / samplesPerSegment;
                        samples.Add(CatmullRom(p0, p1, p2, p3, t));
                    }
                }
            }

            // ตัดจุดซ้อนทับ (ระยะห่าง 0) เพื่อให้ทุก Segment ยาว > 0 เสมอ
            List<Vector3> unique = new List<Vector3>(samples.Count);

            for (int i = 0; i < samples.Count; i++)
            {
                if (unique.Count == 0 ||
                    (samples[i] - unique[unique.Count - 1]).sqrMagnitude > 1e-8f)
                {
                    unique.Add(samples[i]);
                }
            }

            Vector3[] pts = unique.ToArray();
            float[] cum = new float[pts.Length];

            for (int i = 1; i < pts.Length; i++)
                cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).magnitude;

            return new WaypointPath(pts, cum);
        }

        /// <summary>
        /// หาตำแหน่ง ณ ระยะทาง distance จากจุดเริ่ม (clamp 0..TotalLength) และทิศแทนเจนต์ ณ จุดนั้น
        /// </summary>
        public Vector3 Evaluate(float distance, out Vector3 tangent)
        {
            int last = points.Length - 1;

            if (last <= 0)
            {
                tangent = Vector3.zero;
                return points[0];
            }

            if (distance <= 0f)
            {
                tangent = points[1] - points[0];
                return points[0];
            }

            if (distance >= TotalLength)
            {
                tangent = points[last] - points[last - 1];
                return points[last];
            }

            int lo = 0;
            int hi = last;

            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;

                if (cumulative[mid] <= distance)
                    lo = mid;
                else
                    hi = mid;
            }

            float segmentLength = cumulative[lo + 1] - cumulative[lo];

            float t =
                segmentLength > 1e-6f
                    ? (distance - cumulative[lo]) / segmentLength
                    : 0f;

            tangent = points[lo + 1] - points[lo];
            return Vector3.Lerp(points[lo], points[lo + 1], t);
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (p2 - p0) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}