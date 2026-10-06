using System;
using System.Collections.Generic;
using UnityEngine;
using PoolingSystem;

namespace EnemySystem
{
    // ============================================================
    // V2 PATTERN DATA
    // ============================================================

    public enum BulletDirectionMode
    {
        AimAtPlayer,
        FixedAngle,
        RelativeToOwner,
        TowardPlayer,
        AwayFromPlayer,
        Random,
        Rotating
    }

    public enum BulletSpawnMode
    {
        OwnerCenter,
        Offset,
        Circle
    }

    public enum SpeedEasing
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    /// <summary>
    /// แหล่งควบคุมความเร็วระหว่างการเคลื่อนที่ตาม Path
    /// PathTiming = ใช้ Duration + Easing ของ Path แบบเดิม
    /// SpeedTimeline = ใช้ Speed Timeline ของ Element เดียวกันเป็นตัวกำหนดความเร็วจริง
    /// </summary>
    public enum PathSpeedMode
    {
        PathTiming,
        SpeedTimeline
    }

    /// <summary>
    /// PerPattern = สุ่มค่าเดียวใช้ตลอดอายุของ Pattern (Key คือตัว Modifier เอง ดังนั้นแต่ละ Element
    ///              ในหนึ่ง Burst จะได้ค่าสุ่มอิสระจากกัน เพราะแต่ละ Element มี Modifier เป็นของตัวเอง)
    /// PerBurst   = สุ่มใหม่ทุกครั้งที่ Element นี้ยิง (1 ค่าต่อ Element ต่อ Repeat ใช้ร่วมกันทุกกระสุนใน Element นั้น)
    /// PerShot    = สุ่มใหม่ทุกนัดกระสุนแยกกัน
    /// </summary>
    public enum RandomScope
    {
        PerPattern,
        PerBurst,
        PerShot
    }

    /// <summary>
    /// วิธีคำนวณทิศทางเป้าหมายของ Direction Redirect แต่ละจุดใน Direction Timeline
    /// RelativeAngle = หมุนจากทิศตอนเริ่ม Redirect นี้ไปกี่องศา (รองรับเกิน 180 องศาสำหรับลาย spiral)
    /// AimAtPlayer   = หมุนเข้าหาตำแหน่งผู้เล่น ณ ตอนเริ่ม Redirect นี้เท่านั้น (คำนวณครั้งเดียว ไม่ track ตามตลอด)
    /// </summary>
    public enum DirectionRedirectMode
    {
        RelativeAngle,
        AimAtPlayer
    }

    /// <summary>
    /// PerShot   = กระจาย Spread ระหว่างกระสุนที่ยิงพร้อมกันในนัดเดียว (ตาม bulletCount) - พฤติกรรมเดิม
    /// PerRepeat = กระจาย Spread ข้าม Repeat ของ Burst แทน (แต่ละ Repeat ได้มุมคงที่ บาวด์อยู่ในช่วง Spread
    ///             ตาม repeatCount ทั้งหมด - คนละแบบกับ Rotating ที่หมุนต่อเนื่องไม่มีขอบเขต)
    /// </summary>
    public enum SpreadDistribution
    {
        PerShot,
        PerRepeat
    }

    /// <summary>
    /// Even          = กระจายเท่าๆ กันทุกช่อง (ของเดิม)
    /// Gaussian      = กระจายแบบ Bell Curve คงที่ (ไม่สุ่ม) - หนาแน่นตรงกลาง ห่างที่ขอบ ผลลัพธ์เหมือนเดิมทุกครั้งที่ยิง
    /// RandomCluster = สุ่มมุมแบบ Normal Distribution ทุกครั้งที่ยิง (ไม่ซ้ำ) - เหมือนกระสุนลูกปราย/shotgun
    /// RandomUniform = สุ่มมุมเท่าๆ กันทั่วช่วง Spread ทุกครั้งที่ยิง (ไม่กระจุกตรงกลาง)
    ///                 เช่น Angle 120 / Spread 40 -> สุ่มได้ทุกมุมระหว่าง 100..140
    ///                 ไม่ขึ้นกับจำนวนนัด ใช้ได้แม้ bulletCount/repeatCount = 1
    ///
    /// หมายเหตุ: RandomUniform ต่อท้ายสุดเพื่อไม่ให้ค่า enum ที่ Serialize ไว้เดิมเลื่อน
    /// </summary>
    public enum SpreadShape
    {
        Even,
        Gaussian,
        RandomCluster,
        RandomUniform
    }

    /// <summary>
    /// กำหนดว่าจุดจบ/จุดโค้งของ Path อ้างอิงแบบไหน
    /// Offset    = Vector2 relative จากตำแหน่ง Spawn ของกระสุนแต่ละนัดเอง - ใช้ซ้ำข้าม Enemy/Boss ได้
    /// Transform = ตำแหน่งตายตัวในซีน - เหมาะกับ Boss เฉพาะจุดที่ไม่ต้องใช้ซ้ำ
    /// </summary>
    public enum BulletPathPointMode
    {
        Offset,
        Transform
    }

    /// <summary>
    /// พฤติกรรมของกระสุนหลังวิ่งจบ Path
    /// ContinueFlying = บินต่อด้วยทิศทาง (Tangent ปลาย Path) + ความเร็วปกติ กลับไปใช้ระบบ Direction/Speed เดิม
    /// Destroy        = ทำลายกระสุนทันที
    /// Stop           = หยุดนิ่งอยู่ตรงจุดจบ Path (ความเร็วเป็น 0)
    /// </summary>
    public enum PathEndBehavior
    {
        ContinueFlying,
        Destroy,
        Stop
    }


    /// <summary>
    /// วิธีแยก Path เมื่อใช้ร่วมกับ Spread
    /// None = พฤติกรรมเดิม | Random = สุ่ม Lane Offset | FollowSpread = Offset ตาม Spread แบบ deterministic
    /// </summary>
    public enum PathVaryMode
    {
        None,
        Random,
        FollowSpread
    }
    [Serializable]
    public class SpeedTransition
    {
        [Tooltip("ความเร็วเป้าหมายของช่วงนี้ - ใส่ค่าติดลบได้ กระสุนจะเคลื่อนที่ถอยหลัง (สวนทาง Direction ที่ตั้งไว้)")]
        public float targetSpeed = 8f;

        [Min(0f)] public float duration = 1f;
        public SpeedEasing easing = SpeedEasing.Linear;
    }

    /// <summary>
    /// 1 จุดของ Direction Timeline - หมุนทิศทางกระสุนจากค่าปัจจุบัน ไปยังเป้าหมายภายใน Duration ที่กำหนด
    /// ทำงานคู่ขนานไปกับ Speed Timeline (คนละ Timeline กัน ควบคุมคนละแกน)
    /// </summary>
    [Serializable]
    public class DirectionTransition
    {
        [Tooltip("Relative Angle = หมุนจากทิศปัจจุบันไปกี่องศา | Aim At Player = หมุนเข้าหาผู้เล่น ณ ตอนเริ่ม Redirect นี้")]
        public DirectionRedirectMode mode = DirectionRedirectMode.RelativeAngle;

        [Tooltip("ใช้กับ Relative Angle เท่านั้น - องศาที่จะหมุนเพิ่มจากทิศปัจจุบัน (+ = ทวนเข็มนาฬิกา, - = ตามเข็มนาฬิกา) " +
                 "ใส่เกิน 180 หรือติดลบเกิน -180 ได้ตามปกติ เพื่อทำลาย spiral")]
        public float relativeAngle = 0f;

        [Min(0f)] public float duration = 1f;
        public SpeedEasing easing = SpeedEasing.Linear;
    }

    /// <summary>
    /// กำหนด Path การเคลื่อนที่แบบกำหนดเองของกระสุน (เส้นตรง หรือเส้นโค้ง Quadratic Bezier)
    /// จุดเริ่มคือตำแหน่ง Spawn ของกระสุนเองเสมอ (ไม่ต้องกำหนดเพิ่ม)
    /// ระหว่างวิ่งตาม Path บนกระสุนจะไม่ใช้ Speed/Direction Timeline ปกติ (ระบบคนละแกนกัน)
    /// แต่ Burst Timeline / Death Animation / ฟังก์ชันอื่นของกระสุนยังทำงานตามปกติทุกอย่าง
    /// </summary>
    [Serializable]
    public class BulletPathSettings
    {
        [Tooltip("ติ๊กเพื่อเปิดใช้ Path Motion - ถ้าไม่ติ๊ก กระสุนใช้ระบบ Direction/Speed ปกติทุกอย่างเหมือนเดิม")]
        public bool enabled = false;

        [Tooltip(
            "Offset = จุดจบ/จุดโค้งอ้างอิงจากตำแหน่ง Spawn ของกระสุนแต่ละนัดเอง (ใช้ซ้ำข้าม Enemy/Boss ได้)\n" +
            "Transform = จุดจบ/จุดโค้งเป็นตำแหน่งตายตัวในซีน (เหมาะกับ Boss เฉพาะจุด ใช้ซ้ำข้าม Enemy ไม่ได้)"
        )]
        public BulletPathPointMode pointMode = BulletPathPointMode.Offset;

        [Header("End Point")]
        [Tooltip("ใช้เมื่อ Point Mode = Offset - ตำแหน่งจบ Path เทียบจากจุด Spawn ของกระสุนนัดนั้น")]
        public Vector2 endOffset = new Vector2(0f, -5f);

        [Tooltip("ใช้เมื่อ Point Mode = Transform - ลาก Transform ปลายทางในซีนมาใส่")]
        public Transform endTransform;

        [Header("Curve (Optional)")]
        [Tooltip("ติ๊ก = ใช้เส้นโค้ง Quadratic Bezier ผ่านจุดกลางด้านล่าง | ไม่ติ๊ก = เส้นตรงจาก Spawn ไปจุดจบ")]
        public bool useCurve = false;

        [Tooltip("ใช้เมื่อ Point Mode = Offset และ Use Curve - จุดกลางของเส้นโค้ง เทียบจากจุด Spawn")]
        public Vector2 controlOffset = Vector2.zero;

        [Tooltip("ใช้เมื่อ Point Mode = Transform และ Use Curve - ลาก Transform จุดกลางเส้นโค้งในซีนมาใส่")]
        public Transform controlTransform;

        [Header("Spread Compatibility")]
        [Tooltip("ติ๊ก = หมุนทั้ง Path รอบจุด Spawn ตามมุม Spread ของกระสุนนัดนั้น | ปิด = แกนโลกเดิม (พฤติกรรมเดิม)")]
        public bool rotateWithSpread = false;

        [Tooltip("None = ไม่แยก Path | Random = สุ่ม Lane Offset | FollowSpread = คำนวณ Lane Offset จากมุม Spread แบบแน่นอน")]
        public PathVaryMode varyMode = PathVaryMode.None;

        [Tooltip("ระยะ Offset สูงสุดด้านข้าง Path (หน่วยโลก) - 0 = ไม่แยก")]
        [Min(0f)] public float varyAmount = 0f;

        [Tooltip("Scope ของ Random Vary: PerPattern = คงที่ทั้ง Pattern | PerBurst = ใหม่ทุก Repeat | PerShot = ใหม่ทุกนัด")]
        public RandomScope varyScope = RandomScope.PerShot;

        [Header("Timing / Speed") ]
        [Tooltip("Path Timing = ใช้ Duration + Easing ของ Path แบบเดิม | Speed Timeline = ใช้ Speed Timeline ของ Element นี้ควบคุมความเร็วจริงระหว่างวิ่ง Path")]
        public PathSpeedMode speedMode = PathSpeedMode.PathTiming;

        [Tooltip("เวลาที่ใช้วิ่งจบ Path ทั้งเส้น (วินาที) เมื่อ Speed Mode = Path Timing - คงที่ไม่ว่าเส้นจะยาวแค่ไหน")]
        [Min(0.01f)]
        public float duration = 1f;

        public SpeedEasing easing = SpeedEasing.Linear;

        [Header("After Path Completes")]
        public PathEndBehavior endBehavior = PathEndBehavior.ContinueFlying;
    }

    /// <summary>
    /// ค่า Path ที่ Executor คำนวณ (resolve Offset/Transform เป็นตำแหน่งโลกจริงแล้ว) ส่งให้ EnemyBulletV2 ใช้ตรงๆ
    /// </summary>
    public sealed class ResolvedPathMotion
    {
        public Vector3 endPosition;
        public bool hasCurve;
        public Vector3 controlPosition;
        public float duration;
        public SpeedEasing easing;
        public PathSpeedMode speedMode;
        public PathEndBehavior endBehavior;
    }

    [Serializable]
    public class BulletMotionSettings
    {
        [Tooltip("ใช้ความเร็ว default จาก EnemyBulletV2 prefab ถ้าไม่ติ๊ก Override")]
        public bool overrideInitialSpeed = false;

        [Tooltip("ความเร็วเริ่มต้น - ใส่ค่าติดลบได้ กระสุนจะเริ่มเคลื่อนที่ถอยหลัง (สวนทาง Direction) ตั้งแต่ Spawn")]
        public float initialSpeed = 6f;

        [Tooltip("เปลี่ยนความเร็วจากค่าปัจจุบันไป Target ภายใน Duration ตั้งแต่กระสุน Spawn - Target Speed ใส่ค่าติดลบได้ " +
                 "เพื่อให้กระสุนกลับทิศ (เคลื่อนที่สวนทาง Direction เดิม)")]
        public List<SpeedTransition> speedTimeline = new List<SpeedTransition>();

        [Tooltip("เปลี่ยนทิศทางกระสุนตามเวลา ตั้งแต่กระสุน Spawn - ทำงานคู่ขนานไปกับ Speed Timeline คนละแกนกัน")]
        public List<DirectionTransition> directionTimeline = new List<DirectionTransition>();

        [Tooltip(
            "ติ๊ก = กระสุนทุกนัดจาก Burst นี้ (ทุก Repeat) จะ Redirect ตาม Direction Timeline ด้านบน " +
            "พร้อมกันที่เวลาจริงเดียวกันทั้งหมด แทนที่จะนับเวลาแยกจากตอน Spawn ของตัวเอง\n" +
            "ไม่ติ๊ก (ปกติ) = แต่ละนัดนับเวลา Redirect จากตอน Spawn ของตัวเอง (พฤติกรรมเดิม)\n" +
            "DirectionRedirectMode (Relative Angle / Aim At Player) ในแต่ละจุดของ Direction Timeline " +
            "ใช้งานได้ตามปกติไม่ว่าจะติ๊กนี้หรือไม่"
        )]
        public bool syncDirectionTimelineToBurst = false;

        [Tooltip(
            "จุดเวลา (นับจากตอนกระสุนนี้ Spawn) ที่จะสั่งยิง Pattern ใหม่ออกจากตำแหน่งกระสุนตัวนี้เอง " +
            "(ไม่ใช่ตำแหน่ง Boss/Enemy) - แก้ปัญหาคาดเดาตำแหน่งกระสุนกลางอากาศไม่ได้\n" +
            "เป็นคนละระบบกับ Speed/Direction Timeline ด้านบน ใช้ร่วมกันได้"
        )]
        public List<BulletBurstTrigger> burstTimeline = new List<BulletBurstTrigger>();

        [Tooltip(
            "Path การเคลื่อนที่กำหนดเอง (เส้นตรง/เส้นโค้ง) - เปิดใช้ที่ Enabled ข้างใน\n" +
            "ระหว่างวิ่งตาม Path จะไม่ใช้ Speed/Direction Timeline ด้านบน (คนละระบบ)"
        )]
        public BulletPathSettings path = new BulletPathSettings();

        [Tooltip("Homing - เลี้ยวเข้าหาผู้เล่นต่อเนื่องในช่วงเวลาที่กำหนด (ดู HomingSettings)")]
        public HomingSettings homing = new HomingSettings();

        [Tooltip("Orbit - วนรอบตัวที่ยิง Pattern นี้ก่อนปล่อยตัว (ดู OrbitSettings)")]
        public OrbitSettings orbit = new OrbitSettings();
    }

    /// <summary>
    /// 1 จุดใน Burst Timeline ของกระสุน - พอถึงเวลาที่กำหนด จะสั่งยิง Pattern ที่ผูกไว้
    /// ออกจากตำแหน่งจริงของกระสุนตัวนี้ ณ ตอนนั้น (ใช้ระบบ Group/Burst เดิมทุกอย่าง)
    /// </summary>
    [Serializable]
    public class BulletBurstTrigger
    {
        [Tooltip("ชื่อ Trigger สำหรับอ่านง่ายใน Inspector")]
        public string name = "Burst Trigger";

        [Tooltip("เวลานับจากตอนกระสุนตัวนี้ Spawn (วินาที)")]
        [Min(0f)]
        public float time = 0.5f;

        [Tooltip(
            "Pattern ที่จะยิงเมื่อ Trigger ทำงาน\n" +
            "ลาก GameObject ที่มี BulletCustomPattern มาใส่โดยตรง\n" +
            "ห้ามอ้าง Pattern เดิมใน Call Chain ซ้ำ"
        )]
        public BulletCustomPattern pattern;

        [Tooltip(
            "ติ๊ก = ทำลายกระสุนตัวนี้ทันทีหลังเรียก Pattern\n" +
            "ไม่ติ๊ก = กระสุนตัวนี้เดินต่อและ Pattern ที่เรียกจะทำงานต่อเอง"
        )]
        public bool destroySelfAfterFire = false;
    }

    [Serializable]
    public class BulletPatternSchedule
    {
        [Tooltip("BulletCustomPattern ที่จะถูกเรียกใช้")]
        public BulletCustomPattern pattern;

        [Tooltip(
            "เวลารอก่อนเริ่ม Pattern นี้ (วินาที)\n" +
            "Enemy/Boss เป็นผู้ควบคุม Timing ส่วนรายละเอียดกระสุนอยู่ใน BCP"
        )]
        [Min(0f)]
        public float startDelay = 0f;

        [Tooltip(
            "เปิด = อนุญาตให้ Group ที่ตั้ง Loop ใน BCP วนต่อ\n" +
            "ปิด = บังคับทุก Group ให้จบครั้งเดียว"
        )]
        public bool loop = true;
    }

    /// <summary>
    /// Even       = วางกระสุนเท่าๆ กันรอบวง (เดิม) - ตำแหน่งคงที่ทุกครั้ง
    /// RandomEdge = สุ่มมุมบนขอบวง รัศมีคงที่ = circleRadius เป๊ะ
    /// RandomArea = สุ่มทั้งมุมและรัศมี (0..circleRadius) กระจายเท่ากันตามพื้นที่วงกลม ไม่กระจุกตรงกลาง
    /// </summary>
    public enum CircleSpawnDistribution
    {
        Even,
        RandomEdge,
        RandomArea
    }

    [Serializable]
    public class BulletSpawnSettings
    {
        public BulletSpawnMode mode = BulletSpawnMode.OwnerCenter;

        [Tooltip("ใช้เมื่อ Spawn Mode = Offset")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("ใช้เมื่อ Spawn Mode = Circle")]
        [Min(0f)] public float circleRadius = 0f;

        [Tooltip("ใช้เมื่อ Spawn Mode = Circle - เลือกวิธีวางกระสุนรอบวง")]
        public CircleSpawnDistribution circleDistribution = CircleSpawnDistribution.Even;
    }

    [Serializable]
    public class BulletDirectionSettings
    {
        public BulletDirectionMode mode = BulletDirectionMode.AimAtPlayer;

        [Tooltip("ใช้กับ Fixed Angle / Relative To Owner / Rotating")]
        public float angle = -90f;

        [Tooltip("มุมรวมของการกระจายกระสุน เช่น 45 = -22.5 ถึง +22.5")]
        public float spread = 0f;

        [Tooltip(
            "ใช้กับ Rotating: หมุนเพิ่มกี่องศาต่อ Repeat\n" +
            "Repeat นับร่วมกันทั้ง Burst (ทุก Element ยิงพร้อมกันในจังหวะเดียวกัน) " +
            "แต่ค่าที่หมุนเป็นของ Element นี้เอง - ใส่ + กับ - คนละเครื่องหมายในสอง Element " +
            "เพื่อให้ยิง Clockwise/Counter Clockwise พร้อมกันได้"
        )]
        public float rotationPerRepeat = 0f;

        [Tooltip(
            "PerShot (ปกติ) = Spread กระจายระหว่างกระสุนที่ยิงพร้อมกันในนัดเดียว (bulletCount) - " +
            "เห็นผลก็ต่อเมื่อ bulletCount > 1\n" +
            "PerRepeat = Spread กระจายข้าม Repeat ของ Burst นี้แทน - เห็นผลก็ต่อเมื่อ repeatCount > 1 " +
            "แม้ bulletCount = 1 ก็ใช้ได้ (เช่น ยิงทีละนัด แต่ละนัดเบนมุมไปทีละนิดตามลำดับ)\n" +
            "เลือกได้ทีละแบบ ไม่ทำงานพร้อมกันทั้งคู่"
        )]
        public SpreadDistribution spreadDistribution = SpreadDistribution.PerShot;

        [Tooltip(
            "Even (ปกติ) = กระจายเท่าๆ กัน\n" +
            "Gaussian = กระจายแบบ Bell Curve คงที่ หนาตรงกลาง บางที่ขอบ (ไม่สุ่ม เหมือนเดิมทุกครั้ง)\n" +
            "Random Cluster = สุ่มแบบ Normal Distribution ทุกครั้งที่ยิง (ไม่ซ้ำ เหมือนกระสุนลูกปราย) - กระจุกรอบมุมกลาง\n" +
            "Random Uniform = สุ่มเท่าๆ กันทั่วช่วง Spread ทุกครั้งที่ยิง (ไม่กระจุก) - ใช้ได้แม้ยิงนัดเดียว/Repeat เดียว\n" +
            "มีผลทั้งตอน Spread Distribution = PerShot และ PerRepeat"
        )]
        public SpreadShape spreadShape = SpreadShape.Even;
    }

    [Serializable]
    public class RandomAngleModifier
    {
        public bool enabled = false;
        [Min(0f)] public float range = 0f;
        public RandomScope scope = RandomScope.PerShot;
    }

    [Serializable]
    public class RandomSpeedModifier
    {
        public bool enabled = false;
        [Min(0f)] public float range = 0f;
        public RandomScope scope = RandomScope.PerShot;
    }

    [Serializable]
    public class BulletPatternModifiers
    {
        public RandomAngleModifier randomAngle = new RandomAngleModifier();
        public RandomSpeedModifier randomSpeed = new RandomSpeedModifier();

        public bool HasAnyRandom =>
            randomAngle != null && randomAngle.enabled ||
            randomSpeed != null && randomSpeed.enabled;
    }

    /// <summary>
    /// 1 Pattern ย่อยภายใน Burst - ทุก Element ในหนึ่ง Burst ยิงพร้อมกันด้วย Timing เดียวกัน
    /// (repeatCount / repeatInterval / delayAfter เป็นของ BulletBurstV2 ที่เป็นเจ้าของ ใช้ร่วมกันหมด)
    /// แต่ละ Element มี Bullet Prefab / Spawn / Direction / Motion / Modifiers เป็นของตัวเอง
    ///
    /// ใช้เพื่อให้ Burst เดียวยิงได้หลายรูปแบบพร้อมกัน เช่น Element "CW" กับ "CCW"
    /// ที่หมุนคนละทางแต่ยิงพร้อมกันทุก Repeat (ดู rotationPerRepeat ใน BulletDirectionSettings)
    /// </summary>
    [Serializable]
    public class BulletBurstElement
    {
        [Tooltip("ชื่อ Element สำหรับอ่านง่ายใน Inspector เช่น \"CW\", \"CCW\"")]
        public string elementName = "Element";

        [Tooltip("กระสุนที่จะยิงใน Element นี้")]
        public GameObject bulletPrefab;

        [Min(1)]
        public int bulletCount = 1;

        public BulletSpawnSettings spawn = new BulletSpawnSettings();
        public BulletDirectionSettings direction = new BulletDirectionSettings();
        public BulletMotionSettings motion = new BulletMotionSettings();
        public BulletPatternModifiers modifiers = new BulletPatternModifiers();

        [Tooltip("Danger Zone - เส้นเตือนสีแดงก่อนกระสุนเริ่มวิ่ง (ดู TelegraphSettings)")]
        public TelegraphSettings telegraph = new TelegraphSettings();

        [Tooltip("Path Stream - Spawn กระสุนต่อเนื่องตามเส้นทางจาก Transform หลายตัว (ดู PathStreamSettings)")]
        public PathStreamSettings stream = new PathStreamSettings();

        /// <summary>
        /// Deep copy พอสำหรับใช้งานจริง - spawn/direction/motion/modifiers แชร์ Reference เดิมได้
        /// เพราะ Executor ไม่เคยเขียนทับค่าพวกนี้ (เหมือน BulletBurstV2 เดิม)
        ///
        /// กัน null ไว้ด้วย (?? new ...()) เผื่อโดน Unity List-Add Inspector bug ที่ field
        /// initializer ของสมาชิกใหม่ใน List ไม่ถูกเรียก (บั๊กเดียวกับที่เคยเจอกับ baseDirection เดิม)
        /// </summary>
        public BulletBurstElement Clone()
        {
            return new BulletBurstElement
            {
                elementName = elementName,
                bulletPrefab = bulletPrefab,
                bulletCount = bulletCount,
                spawn = spawn ?? new BulletSpawnSettings(),
                direction = direction ?? new BulletDirectionSettings(),
                motion = motion ?? new BulletMotionSettings(),
                modifiers = modifiers ?? new BulletPatternModifiers(),
                telegraph = telegraph ?? new TelegraphSettings(),
                stream = stream ?? new PathStreamSettings()
            };
        }
    }

    [Serializable]
    public class BulletBurstV2 : ISerializationCallbackReceiver
    {
        public string name = "Burst";

        [Min(1)]
        public int repeatCount = 1;

        [Min(0f)]
        public float repeatInterval = 0.1f;

        [Min(0f)]
        public float delayAfter = 0.5f;

        [Tooltip(
            "Pattern ย่อยที่ยิงพร้อมกันทุกครั้งใน Burst นี้ (Timing เดียวกันหมด)\n" +
            "เพิ่มมากกว่า 1 Element เพื่อยิงหลาย Pattern พร้อมกัน " +
            "เช่น Element ยิง Clockwise + Element ยิง Counter Clockwise"
        )]
        public List<BulletBurstElement> elements = new List<BulletBurstElement>();

        // ============================================================
        // LEGACY (ก่อนมี Multi-Element)
        // เก็บไว้เฉพาะให้ OnAfterDeserialize ใช้ Migrate ข้อมูลเดิมที่เคยตั้งค่าไว้
        // ก่อนหน้านี้เข้า elements[0] อัตโนมัติ - ห้ามแก้ตรงนี้ตรงๆ อีกต่อไป ใช้ elements แทน
        // ============================================================
        [SerializeField, HideInInspector] private GameObject bulletPrefab;
        [SerializeField, HideInInspector] private int bulletCount = 1;
        [SerializeField, HideInInspector] private BulletSpawnSettings spawn;
        [SerializeField, HideInInspector] private BulletDirectionSettings direction;
        [SerializeField, HideInInspector] private BulletMotionSettings motion;
        [SerializeField, HideInInspector] private BulletPatternModifiers modifiers;

        [NonSerialized] public int currentRepeat;
        [NonSerialized] public float repeatTimer;
        [NonSerialized] public float delayTimer;
        [NonSerialized] public bool fired;

        [Tooltip(
            "เวลาที่ผ่านไปตั้งแต่ Burst นี้ยิงนัดแรก (Runtime เท่านั้น) - ใช้เป็นตัวอ้างอิงเวลาให้ " +
            "syncDirectionTimelineToBurst ของแต่ละ Element คำนวณ Catch-up Direction Timeline ให้ตรงกัน"
        )]
        [NonSerialized] public float burstClock;

        public void ResetRuntime()
        {
            currentRepeat = 0;
            repeatTimer = 0f;
            delayTimer = 0f;
            fired = false;
            burstClock = 0f;
        }

        /// <summary>
        /// ใช้ตอนกระสุนตัวหนึ่งจะยิง Pattern จาก Burst Timeline ของตัวเอง (ดู EnemyBulletV2)
        /// Config ของแต่ละ Element แชร์ Reference เดิมได้เลย เพราะ Executor ไม่เคยเขียนทับค่าพวกนี้
        /// สิ่งเดียวที่ต้องแยกคือ Field ที่มี [NonSerialized] Runtime State ด้านบน
        /// เพื่อกันกระสุนหลายตัวที่ใช้ Pattern เดียวกัน มาแย่ง currentRepeat/timer กันเอง
        /// </summary>
        public BulletBurstV2 Clone()
        {
            var clone = new BulletBurstV2
            {
                name = name,
                repeatCount = repeatCount,
                repeatInterval = repeatInterval,
                delayAfter = delayAfter,
                elements = new List<BulletBurstElement>()
            };

            if (elements != null)
            {
                for (int i = 0; i < elements.Count; i++)
                    clone.elements.Add(elements[i]?.Clone());
            }

            return clone;
        }

        // ============================================================
        // AUTO MIGRATION
        //
        // ทำงานทุกครั้งที่ Object นี้ถูก Deserialize (เปิด Scene, เข้า Play Mode,
        // Domain Reload) - ถ้า elements ยังว่างอยู่แต่มีข้อมูล Legacy เดิม จะสร้าง
        // Element แรกให้จากข้อมูลเดิมทันทีอัตโนมัติ ไม่ต้องกดปุ่ม Migrate เอง
        //
        // หมายเหตุ: การ Clear legacy field (bulletPrefab = null ท้ายฟังก์ชัน) มีผล
        // จริงถาวรก็ต่อเมื่อมีการ Save Scene/Prefab หลังจากนี้ - ถ้ายังไม่ได้ Save
        // รอบถัดไปที่โหลด Object นี้จะ Migrate ซ้ำอีกครั้งจากข้อมูลเดิมบนดิสก์
        // (ให้ผลลัพธ์เหมือนเดิมทุกครั้ง ไม่กระทบ Gameplay แค่ Log ซ้ำจนกว่าจะ Save)
        // ============================================================
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (elements == null)
                elements = new List<BulletBurstElement>();

            if (elements.Count > 0)
                return;

            if (ReferenceEquals(bulletPrefab, null))
                return;

            elements.Add(new BulletBurstElement
            {
                elementName = "Element 0",
                bulletPrefab = bulletPrefab,
                bulletCount = Mathf.Max(1, bulletCount),
                spawn = spawn ?? new BulletSpawnSettings(),
                direction = direction ?? new BulletDirectionSettings(),
                motion = motion ?? new BulletMotionSettings(),
                modifiers = modifiers ?? new BulletPatternModifiers()
            });

            Debug.LogWarning(
                $"BulletPatternV2: Burst '{name}' migrated automatically to multi-element format " +
                "(1 Element created from previous single-pattern data). Legacy fields are now unused - " +
                "save the Scene/Prefab once to make this permanent.");

            bulletPrefab = null;
        }
    }

    [Serializable]
    public class BulletPatternGroup
    {
        public string groupName = "New Pattern";

        [Header("Timing")]
        [Tooltip("เวลารอก่อน Pattern นี้เริ่มทำงานครั้งแรก")]
        [Min(0f)]
        public float initialDelay = 0f;

        [Tooltip("แต่ละ Group มี timeline แยกจาก Group อื่น")]
        public List<BulletBurstV2> bursts = new List<BulletBurstV2>();

        public bool loopPattern = true;

        [NonSerialized] public int currentBurst;
        [NonSerialized] public bool finished;
        [NonSerialized] public float initialDelayTimer;

        public void ResetRuntime()
        {
            currentBurst = 0;
            finished = false;
            initialDelayTimer = initialDelay;

            if (bursts == null) return;
            for (int i = 0; i < bursts.Count; i++)
                bursts[i].ResetRuntime();
        }

        /// <summary>
        /// Deep copy เฉพาะส่วนที่มี Runtime State (ตัวเองกับ Bursts ข้างใน)
        /// ใช้ตอนกระสุนแต่ละตัวยิง Pattern เดียวกันจาก Burst Timeline พร้อมกันหลายตัว
        /// กัน currentBurst/timer ของ Group เดียวกันโดนเขียนทับข้ามกระสุน
        /// </summary>
        public BulletPatternGroup Clone()
        {
            BulletPatternGroup clone = new BulletPatternGroup
            {
                groupName = groupName,
                initialDelay = initialDelay,
                loopPattern = loopPattern,
                bursts = new List<BulletBurstV2>()
            };

            if (bursts != null)
            {
                for (int i = 0; i < bursts.Count; i++)
                    clone.bursts.Add(bursts[i]?.Clone());
            }

            clone.ResetRuntime();
            return clone;
        }
    }

    // ============================================================
    // V2 RUNTIME
    // ============================================================

    public sealed class BulletPatternV2Executor
    {
        private readonly List<BulletPatternGroup> groups;
        private readonly Transform owner;
        private readonly Func<Transform> playerProvider;
        private readonly HashSet<BulletCustomPattern> patternCallChain;

        private System.Random random;

        // Same modifier object = same random value for PerPattern.
        private readonly Dictionary<object, float> perPatternRandomValues =
            new Dictionary<object, float>();

        private readonly Dictionary<object, float> perPatternPathVaryValues =
            new Dictionary<object, float>();
        private readonly Dictionary<object, float> perPatternOrbitRadiusValues =
            new Dictionary<object, float>();
        private readonly Dictionary<object, float> perPatternOrbitAngleValues =
            new Dictionary<object, float>();

        // เมื่อเปิด Telegraph.showOncePerPattern จะวาดเฉพาะเส้นหลักครั้งแรกของ BCP นี้
        private bool dangerLineShown;

        // ------------------------------------------------------------
        // PATH STREAM runtime
        // 1 Session = Stream 1 สายที่กำลังทำงานอยู่ (เริ่มทุกครั้งที่ Burst ยิง Element ที่เปิด Path Stream)
        // ------------------------------------------------------------
        private sealed class StreamSession
        {
            public string burstName;
            public BulletBurstElement element;
            public PathStreamSettings settings;

            public float burstClockAtStart;
            public float delay;          // เวลารอเส้นเตือน (Danger Zone) ก่อนเริ่ม Spawn
            public float spawnTimer;
            public float elapsed;

            public float sweepDistance;  // ระยะที่จุด Spawn เดินไปบน Path (โหมด Sweep)
            public float sweepSpeed;

            public int tick;
            public int maxTicks;
        }

        private readonly List<StreamSession> streamSessions =
            new List<StreamSession>();

        // Buffer ใช้ซ้ำเพื่อเลี่ยง GC ตอนรวบรวมจุดควบคุมของ Path Stream
        private readonly List<Vector3> controlPointBuffer =
            new List<Vector3>();

        public BulletPatternV2Executor(
            List<BulletPatternGroup> groups,
            Transform owner,
            Func<Transform> playerProvider,
            IEnumerable<BulletCustomPattern> patternCallChain = null)
        {
            this.groups = groups;
            this.owner = owner;
            this.playerProvider = playerProvider;
            this.patternCallChain =
                patternCallChain != null
                    ? new HashSet<BulletCustomPattern>(patternCallChain)
                    : new HashSet<BulletCustomPattern>();

            ResetAllGroups();
        }

        public void ResetAllGroups()
        {
            if (groups == null) return;

            for (int i = 0; i < groups.Count; i++)
                groups[i]?.ResetRuntime();

            streamSessions.Clear();
            dangerLineShown = false;

            ResetRandom();
        }

        public void ResetRandom()
        {
            random = new System.Random(Environment.TickCount);
            perPatternRandomValues.Clear();
            perPatternPathVaryValues.Clear();
            perPatternOrbitRadiusValues.Clear();
            perPatternOrbitAngleValues.Clear();
        }

        public void SetSeed(int seed)
        {
            random = new System.Random(seed);
            perPatternRandomValues.Clear();
        }

        public void Tick(float deltaTime)
        {
            if (groups == null || owner == null || PoolManager.Instance == null)
                return;

            Transform player = playerProvider != null ? playerProvider() : null;

            for (int i = 0; i < groups.Count; i++)
                TickGroup(groups[i], deltaTime, player);

            if (streamSessions.Count > 0)
                TickStreams(deltaTime, player);
        }

        private void TickGroup(BulletPatternGroup group, float dt, Transform player)
        {
            if (group == null || group.finished || group.bursts == null || group.bursts.Count == 0)
                return;

            if (group.initialDelayTimer > 0f)
            {
                group.initialDelayTimer -= dt;
                if (group.initialDelayTimer > 0f)
                    return;
            }
            
            if (group.currentBurst < 0 || group.currentBurst >= group.bursts.Count)
            {
                group.finished = true;
                return;
            }

            BulletBurstV2 burst = group.bursts[group.currentBurst];
            if (burst == null)
            {
                AdvanceBurst(group);
                return;
            }

            // Sync Redirect: burstClock นับเวลาตั้งแต่ Burst นี้ยิงนัดแรก (ยังไม่นับในเฟรมที่ยิงนัดแรกพอดี
            // เพราะ burst.fired ยังเป็น false ตอนเช็คบรรทัดนี้ - เพิ่มให้ตั้งแต่เฟรมถัดไป)
            if (burst.fired)
                burst.burstClock += dt;

            int repeatCount = Mathf.Max(1, burst.repeatCount);

            if (!burst.fired)
            {
                FireBurst(burst, 0, player);
                burst.fired = true;
                burst.currentRepeat = 0;
                burst.repeatTimer = 0f;
                burst.delayTimer = 0f;
                return;
            }

            if (burst.currentRepeat < repeatCount - 1)
            {
                float interval = Mathf.Max(0f, burst.repeatInterval);

                if (interval <= 0f)
                {
                    burst.currentRepeat++;
                    FireBurst(burst, burst.currentRepeat, player);
                    return;
                }

                burst.repeatTimer += dt;

                while (burst.repeatTimer >= interval &&
                       burst.currentRepeat < repeatCount - 1)
                {
                    burst.repeatTimer -= interval;
                    burst.currentRepeat++;
                    FireBurst(burst, burst.currentRepeat, player);
                }

                return;
            }

            burst.delayTimer += dt;
            if (burst.delayTimer >= Mathf.Max(0f, burst.delayAfter))
                AdvanceBurst(group);
        }

        private void AdvanceBurst(BulletPatternGroup group)
        {
            group.currentBurst++;

            if (group.currentBurst >= group.bursts.Count)
            {
                if (group.loopPattern)
                    group.currentBurst = 0;
                else
                {
                    group.currentBurst = group.bursts.Count - 1;
                    group.finished = true;
                }
            }

            if (!group.finished &&
                group.currentBurst >= 0 &&
                group.currentBurst < group.bursts.Count)
            {
                group.bursts[group.currentBurst].ResetRuntime();
            }
        }

        /// <summary>
        /// ยิงทุก Element ของ Burst นี้พร้อมกัน ในจังหวะ Repeat เดียวกัน
        /// (repeatIndex เดียวกันสำหรับทุก Element ใน Burst นี้)
        /// Element ที่เปิด Path Stream จะเริ่ม Stream สายใหม่แทนการยิงปกติ
        /// </summary>
        private void FireBurst(BulletBurstV2 burst, int repeatIndex, Transform player)
        {
            if (burst?.elements == null)
                return;

            for (int e = 0; e < burst.elements.Count; e++)
            {
                BulletBurstElement element = burst.elements[e];

                if (element == null)
                    continue;

                if (element.stream != null && element.stream.enabled)
                {
                    StartStream(burst.name, element, repeatIndex, burst.burstClock);
                    continue;
                }

                FireElement(
                    burst.name,
                    element,
                    repeatIndex,
                    Mathf.Max(1, burst.repeatCount),
                    burst.burstClock,
                    player);
            }
        }

        // ============================================================
        // PATH STREAM
        // ============================================================

        private WaypointPath BuildStreamPath(
            PathStreamSettings settings,
            Vector3 elementSpawn,
            bool warn,
            string burstName,
            BulletBurstElement element)
        {
            int count = settings.CollectPoints(elementSpawn, controlPointBuffer);

            if (count < 2)
            {
                if (warn)
                {
                    Debug.LogWarning(
                        $"BulletPatternV2: Burst '{burstName}' > Element '{element.elementName}' Path Stream " +
                        "มีจุดไม่ถึง 2 จุด (ตรวจ Start Mode / Start Transform / Waypoints) - ข้ามการยิง");
                }

                return null;
            }

            WaypointPath path = WaypointPath.Build(controlPointBuffer, settings.useCurve);

            if (path == null || path.Count < 2 || path.TotalLength < 0.01f)
            {
                if (warn)
                {
                    Debug.LogWarning(
                        $"BulletPatternV2: Burst '{burstName}' > Element '{element.elementName}' Path Stream " +
                        "สั้นเกินไป (จุดซ้อนทับกัน) - ข้ามการยิง");
                }

                return null;
            }

            return path;
        }

        private void StartStream(
            string burstName,
            BulletBurstElement element,
            int repeatIndex,
            float burstClock)
        {
            if (element.bulletPrefab == null)
            {
                Debug.LogWarning(
                    $"BulletPatternV2: Burst '{burstName}' > Element '{element.elementName}' ไม่มี Bullet Prefab - ข้ามการยิง");
                return;
            }

            PathStreamSettings settings = element.stream;

            Vector3 elementSpawn = CalculateBaseSpawnPosition(element.spawn);

            WaypointPath path =
                BuildStreamPath(settings, elementSpawn, true, burstName, element);

            if (path == null)
                return;

            float interval = Mathf.Max(0.01f, settings.spawnInterval);
            float duration = Mathf.Max(0f, settings.streamDuration);

            StreamSession session = new StreamSession
            {
                burstName = burstName,
                element = element,
                settings = settings,
                burstClockAtStart = burstClock,
                spawnTimer = interval,   // Spawn ครั้งแรกทันทีในเฟรมแรกที่เริ่ม (หลังเส้นเตือนจบ)
                maxTicks = Mathf.Max(1, Mathf.FloorToInt(duration / interval) + 1),
                sweepSpeed =
                    settings.pathSpeed > 0f
                        ? settings.pathSpeed
                        : path.TotalLength / Mathf.Max(0.01f, duration)
            };

            TelegraphSettings telegraph = element.telegraph;

            bool allowTelegraph =
                telegraph != null &&
                telegraph.enabled &&
                (!telegraph.showOncePerPattern || (!dangerLineShown && repeatIndex == 0));

            if (allowTelegraph)
            {
                float warnTime = Mathf.Max(0.05f, telegraph.duration);

                DangerLineV1.Show(
                    path.Points,
                    path.Count,
                    telegraph.lineWidth,
                    telegraph.color,
                    telegraph.sortingOrder,
                    warnTime);

                session.delay = warnTime;
                if (telegraph.showOncePerPattern)
                    dangerLineShown = true;
            }

            streamSessions.Add(session);
        }

        private void TickStreams(float dt, Transform player)
        {
            for (int i = streamSessions.Count - 1; i >= 0; i--)
            {
                StreamSession session = streamSessions[i];

                if (session.delay > 0f)
                {
                    session.delay -= dt;

                    if (session.delay > 0f)
                        continue;
                }

                float interval = Mathf.Max(0.01f, session.settings.spawnInterval);

                session.elapsed += dt;
                session.spawnTimer += dt;

                bool finished = false;

                while (!finished && session.spawnTimer >= interval)
                {
                    session.spawnTimer -= interval;
                    finished = !SpawnStreamTick(session, interval, player);
                }

                if (finished)
                    streamSessions.RemoveAt(i);
            }
        }

        /// <summary>
        /// Spawn 1 ครั้งของ Stream - คืน false เมื่อ Stream จบแล้ว (ครบจำนวนครั้ง / Sweep ถึงปลาย Path / Path หายไป)
        /// อ่านตำแหน่ง Transform สดทุกครั้ง และ Rebuild Path ใหม่ (Array เล็กๆ ต่อครั้ง ไม่ใช่ต่อกระสุน)
        /// </summary>
        private bool SpawnStreamTick(
            StreamSession session,
            float interval,
            Transform player)
        {
            BulletBurstElement element = session.element;
            PathStreamSettings settings = session.settings;

            Vector3 elementSpawn = CalculateBaseSpawnPosition(element.spawn);

            WaypointPath path =
                BuildStreamPath(settings, elementSpawn, false, session.burstName, element);

            // Transform บางตัวถูกทำลายระหว่างทางจน Path เหลือไม่ถึง 2 จุด -> จบ Stream
            if (path == null)
                return false;

            float clock = session.burstClockAtStart + session.elapsed;
            bool reachedEnd = false;

            if (settings.mode == PathStreamMode.FlowAlongPath)
            {
                FireElement(
                    session.burstName,
                    element,
                    session.tick,
                    session.maxTicks,
                    clock,
                    player,
                    path.Start,
                    path,
                    settings.pathSpeed,
                    settings.endBehavior,
                    true);
            }
            else
            {
                float distance =
                    Mathf.Min(session.sweepDistance, path.TotalLength);

                Vector3 tangent;
                Vector3 position = path.Evaluate(distance, out tangent);

                FireElement(
                    session.burstName,
                    element,
                    session.tick,
                    session.maxTicks,
                    clock,
                    player,
                    position,
                    null,
                    0f,
                    PathEndBehavior.ContinueFlying,
                    true);

                reachedEnd = session.sweepDistance >= path.TotalLength;
                session.sweepDistance += session.sweepSpeed * interval;
            }

            session.tick++;

            return session.tick < session.maxTicks && !reachedEnd;
        }

        // ============================================================
        // FIRE ELEMENT
        // ============================================================

        /// <summary>
        /// spawnOverride / flowPath / flowSpeedSetting / flowEnd / fromStream ใช้เฉพาะตอนถูกเรียกจาก Path Stream
        /// - spawnOverride: ตำแหน่ง Spawn แทนตำแหน่งปกติของ Element
        /// - flowPath: ถ้าไม่ null กระสุนจะไหลตาม Path นี้ (โหมด Flow)
        /// - fromStream: Danger Zone ของ Stream เป็นระดับ Session จึงไม่ส่งต่อให้กระสุนแต่ละนัด
        /// </summary>
        private void FireElement(
            string burstName,
            BulletBurstElement element,
            int repeatIndex,
            int repeatCount,
            float burstClock,
            Transform player,
            Vector3? spawnOverride = null,
            WaypointPath flowPath = null,
            float flowSpeedSetting = 0f,
            PathEndBehavior flowEnd = PathEndBehavior.ContinueFlying,
            bool fromStream = false)
        {
            if (element.bulletPrefab == null)
            {
                Debug.LogWarning(
                    $"BulletPatternV2: Burst '{burstName}' > Element '{element.elementName}' ไม่มี Bullet Prefab - ข้ามการยิง");
                return;
            }

            int count = Mathf.Max(1, element.bulletCount);

            Vector3 baseSpawn =
                spawnOverride.HasValue
                    ? spawnOverride.Value
                    : CalculateBaseSpawnPosition(element.spawn);

            bool hasWaypointFlow = flowPath != null;

            float elementRandomAngle = 0f;
            float elementRandomSpeed = 0f;
            float pathVaryPerBurst = 0f;
            float orbitRadiusPerBurst = 0f;
            float orbitAnglePerBurst = 0f;

            if (element.modifiers != null)
            {
                if (element.modifiers.randomAngle != null &&
                    element.modifiers.randomAngle.enabled)
                {
                    RandomAngleModifier modifier = element.modifiers.randomAngle;

                    elementRandomAngle =
                        modifier.scope == RandomScope.PerPattern
                            ? GetPerPatternRandom(modifier, modifier.range)
                            : modifier.scope == RandomScope.PerBurst
                                ? RandomRange(modifier.range)
                                : 0f;
                }

                if (element.modifiers.randomSpeed != null &&
                    element.modifiers.randomSpeed.enabled)
                {
                    RandomSpeedModifier modifier = element.modifiers.randomSpeed;

                    elementRandomSpeed =
                        modifier.scope == RandomScope.PerPattern
                            ? GetPerPatternRandom(modifier, modifier.range)
                            : modifier.scope == RandomScope.PerBurst
                                ? RandomRange(modifier.range)
                                : 0f;
                }
            }

            PathVaryMode pathVaryMode = PathVaryMode.None;
            float pathVaryAmount = 0f;
            RandomScope pathVaryScope = RandomScope.PerShot;

            if (element.motion != null && element.motion.path != null)
            {
                pathVaryMode = element.motion.path.varyMode;
                pathVaryAmount = Mathf.Max(0f, element.motion.path.varyAmount);
                pathVaryScope = element.motion.path.varyScope;

                if (pathVaryMode == PathVaryMode.Random &&
                    pathVaryAmount > 0f &&
                    pathVaryScope == RandomScope.PerPattern)
                    pathVaryPerBurst = GetPerPatternRandom(element.motion.path, pathVaryAmount);
                else if (pathVaryMode == PathVaryMode.Random &&
                         pathVaryAmount > 0f &&
                         pathVaryScope == RandomScope.PerBurst)
                    pathVaryPerBurst = RandomRange(pathVaryAmount);
            }

            if (hasWaypointFlow && element.stream != null)
            {
                pathVaryMode = element.stream.varyMode;
                pathVaryAmount = Mathf.Max(0f, element.stream.varyAmount);
                pathVaryScope = element.stream.varyScope;
                pathVaryPerBurst = 0f;

                if (pathVaryMode == PathVaryMode.Random &&
                    pathVaryAmount > 0f &&
                    pathVaryScope == RandomScope.PerPattern)
                    pathVaryPerBurst = GetPerPatternRandom(element.stream, pathVaryAmount);
                else if (pathVaryMode == PathVaryMode.Random &&
                         pathVaryAmount > 0f &&
                         pathVaryScope == RandomScope.PerBurst)
                    pathVaryPerBurst = RandomRange(pathVaryAmount);
            }


            Vector2 centerDirection = CalculateCenterDirection(
                element.direction,
                baseSpawn,
                player,
                repeatIndex,
                repeatCount);

            // ตัวเลือกขั้นสูงที่เหมือนกันทุกนัดใน Element นี้ (Reference ของ Config เดิม ไม่สร้างของใหม่)
            BulletPathSettings pathCfg = element.motion.path;
            bool pathCfgEnabled = pathCfg != null && pathCfg.enabled;

            HomingSettings homingCfg =
                element.motion.homing != null && element.motion.homing.enabled
                    ? element.motion.homing
                    : null;

            // Orbit ใช้ร่วมกับ Path Motion / Path Flow ไม่ได้ (Path ชนะ)
            OrbitSettings orbitCfg =
                !hasWaypointFlow &&
                !pathCfgEnabled &&
                element.motion.orbit != null &&
                element.motion.orbit.enabled
                    ? element.motion.orbit
                    : null;

            if (orbitCfg != null)
            {
                OrbitSettings orbitSettings = orbitCfg;

                if (orbitSettings.radiusVary > 0f)
                {
                    if (orbitSettings.radiusVaryScope == RandomScope.PerPattern)
                        orbitRadiusPerBurst = GetPerPatternRandom(
                            orbitSettings, orbitSettings.radiusVary);
                    else if (orbitSettings.radiusVaryScope == RandomScope.PerBurst)
                        orbitRadiusPerBurst = RandomRange(orbitSettings.radiusVary);
                }

                if (orbitSettings.startAngleVary > 0f)
                {
                    if (orbitSettings.startAngleVaryScope == RandomScope.PerPattern)
                        orbitAnglePerBurst = GetPerPatternOrbitAngleRandom(
                            orbitSettings, orbitSettings.startAngleVary);
                    else if (orbitSettings.startAngleVaryScope == RandomScope.PerBurst)
                        orbitAnglePerBurst = RandomRange(orbitSettings.startAngleVary);
                }
            }


            // Danger Zone ระดับกระสุน: ไม่ใช้กับ Stream (Session จัดการเอง) และไม่ใช้กับ Orbit
            TelegraphSettings telegraphCfg =
                !fromStream &&
                orbitCfg == null &&
                element.telegraph != null &&
                element.telegraph.enabled &&
                (!element.telegraph.showOncePerPattern ||
                 (!dangerLineShown && repeatIndex == 0))
                    ? element.telegraph
                    : null;

            for (int i = 0; i < count; i++)
            {
                float perShotSpread =
                    element.direction.spreadDistribution == SpreadDistribution.PerShot
                        ? element.direction.spread
                        : 0f;

                float spreadAngle = CalculateSpreadOffset(i, count, perShotSpread, element.direction.spreadShape);
                float repeatSpreadAngle =
                    element.direction.spreadDistribution == SpreadDistribution.PerRepeat
                        ? CalculateSpreadOffset(repeatIndex, repeatCount, element.direction.spread, element.direction.spreadShape)
                        : 0f;
                float spreadCompatibilityAngle = spreadAngle + repeatSpreadAngle;
                float angle = spreadAngle;

                if (element.modifiers != null &&
                    element.modifiers.randomAngle != null &&
                    element.modifiers.randomAngle.enabled &&
                    element.modifiers.randomAngle.scope == RandomScope.PerShot)
                {
                    angle += RandomRange(element.modifiers.randomAngle.range);
                }

                angle += elementRandomAngle;

                Vector2 direction = Rotate(centerDirection, angle);

                Vector3 spawnPosition = baseSpawn;

                // Path Flow: แต่ละนัดสามารถรับ Rotation/Offset ของตัวเองได้
                // ทำให้ Spread ไม่ถูกเขียนทับเป็นเส้นเดียวกัน
                WaypointPath bulletFlowPath = flowPath;
                float waypointRotation = 0f;
                Vector3 waypointOffset = Vector3.zero;

                if (hasWaypointFlow)
                {
                    float flowVary = pathVaryMode == PathVaryMode.Random
                        ? (pathVaryScope == RandomScope.PerShot
                            ? RandomRange(pathVaryAmount)
                            : pathVaryPerBurst)
                        : pathVaryMode == PathVaryMode.FollowSpread
                            ? FollowSpreadOffset(spreadCompatibilityAngle, element.direction.spread) * pathVaryAmount
                            : 0f;

                    Vector3 tangent = flowPath.Points.Length > 1
                        ? flowPath.Points[1] - flowPath.Points[0]
                        : Vector3.right;

                    if (tangent.sqrMagnitude < 0.000001f)
                        tangent = Vector3.right;

                    waypointRotation =
                        element.stream != null && element.stream.rotateWithSpread
                            ? spreadCompatibilityAngle
                            : 0f;

                    tangent = RotateVector(tangent, waypointRotation).normalized;
                    waypointOffset = new Vector3(-tangent.y, tangent.x, 0f) * flowVary;
                    spawnPosition = flowPath.Start + waypointOffset;
                }

                // Path Flow: กระสุนต้องเกิดที่จุดเริ่ม Path เป๊ะๆ จึงไม่ใช้ Circle Spawn
                if (!hasWaypointFlow && element.spawn.mode == BulletSpawnMode.Circle)
                {
                    float circleAngle;
                    float circleRadius = element.spawn.circleRadius;

                    switch (element.spawn.circleDistribution)
                    {
                        case CircleSpawnDistribution.RandomEdge:
                            circleAngle = RandomRange(180f) + 180f;
                            break;

                        case CircleSpawnDistribution.RandomArea:
                            circleAngle = RandomRange(180f) + 180f;
                            circleRadius =
                                Mathf.Sqrt(RandomUnit01()) *
                                element.spawn.circleRadius;
                            break;

                        default: // Even
                            circleAngle = count == 1 ? 0f : 360f * i / count;
                            break;
                    }

                    Vector2 offset =
                        Rotate(Vector2.right, circleAngle) *
                        circleRadius;

                    spawnPosition += (Vector3)offset;
                }

                GameObject bulletObject =
                    PoolManager.Instance.Get(
                        element.bulletPrefab,
                        spawnPosition,
                        Quaternion.identity);

                if (bulletObject == null)
                    continue;

                EnemyBulletV2 bullet =
                    bulletObject.GetComponent<EnemyBulletV2>();

                if (bullet == null)
                {
                    Debug.LogError(
                        $"BulletPatternV2: '{element.bulletPrefab.name}' ไม่มี EnemyBulletV2",
                        element.bulletPrefab);

                    PoolManager.Instance.Return(bulletObject);
                    continue;
                }

                float speedOffset = elementRandomSpeed;

                if (element.modifiers != null &&
                    element.modifiers.randomSpeed != null &&
                    element.modifiers.randomSpeed.enabled &&
                    element.modifiers.randomSpeed.scope == RandomScope.PerShot)
                {
                    speedOffset +=
                        RandomRange(element.modifiers.randomSpeed.range);
                }

                bool usePatternSpeed = element.motion.overrideInitialSpeed;
                float initialSpeed = element.motion.initialSpeed;

                // Random Speed must also work when the Pattern itself does not
                // override the initial speed: use the bullet prefab's default.
                if (Mathf.Abs(speedOffset) > 0.000001f)
                {
                    initialSpeed =
                        (usePatternSpeed
                            ? initialSpeed
                            : bullet.DefaultSpeed) +
                        speedOffset;

                    usePatternSpeed = true;
                }

                bullet.SetDirection(direction);

                ResolvedPathMotion resolvedPath = null;

                // Path Flow (Stream) ชนะ Path Motion ปกติ
                if (pathCfgEnabled && !hasWaypointFlow)
                {
                    Vector3 endPos =
                        pathCfg.pointMode == BulletPathPointMode.Transform &&
                        pathCfg.endTransform != null
                            ? pathCfg.endTransform.position
                            : spawnPosition + (Vector3)pathCfg.endOffset;

                    Vector3 controlPos = spawnPosition;

                    if (pathCfg.useCurve)
                    {
                        controlPos =
                            pathCfg.pointMode == BulletPathPointMode.Transform &&
                            pathCfg.controlTransform != null
                                ? pathCfg.controlTransform.position
                                : spawnPosition + (Vector3)pathCfg.controlOffset;
                    }

                    float pathVary = pathVaryMode == PathVaryMode.Random
                        ? (pathVaryScope == RandomScope.PerShot
                            ? RandomRange(pathVaryAmount)
                            : pathVaryPerBurst)
                        : pathVaryMode == PathVaryMode.FollowSpread
                            ? FollowSpreadOffset(spreadCompatibilityAngle, element.direction.spread) * pathVaryAmount
                            : 0f;

                    float pathRotation = pathCfg.rotateWithSpread ? spreadCompatibilityAngle : 0f;
                    Vector3 pathTangent = pathHasInitialTangent(spawnPosition, controlPos, endPos);
                    pathTangent = RotateVector(pathTangent, pathRotation).normalized;
                    Vector3 lateralOffset = new Vector3(-pathTangent.y, pathTangent.x, 0f) * pathVary;

                    if (Mathf.Abs(pathRotation) > 0.0001f)
                    {
                        endPos = spawnPosition + RotateVector(endPos - spawnPosition, pathRotation);
                        if (pathCfg.useCurve)
                            controlPos = spawnPosition + RotateVector(controlPos - spawnPosition, pathRotation);
                    }

                    endPos += lateralOffset;
                    if (pathCfg.useCurve)
                        controlPos += lateralOffset;

                    resolvedPath = new ResolvedPathMotion
                    {
                        endPosition = endPos,
                        hasCurve = pathCfg.useCurve,
                        controlPosition = controlPos,
                        duration = Mathf.Max(0.01f, pathCfg.duration),
                        easing = pathCfg.easing,
                        speedMode = pathCfg.speedMode,
                        endBehavior = pathCfg.endBehavior
                    };
                }

                float directionCatchUp =
                    element.motion.syncDirectionTimelineToBurst
                        ? burstClock
                        : 0f;

                float orbitRadiusOffset = 0f;
                float orbitStartAngleOffset = 0f;

                if (orbitCfg != null)
                {
                    orbitRadiusOffset =
                        orbitCfg.radiusVary <= 0f
                            ? 0f
                            : orbitCfg.radiusVaryScope == RandomScope.PerShot
                                ? RandomRange(orbitCfg.radiusVary)
                                : orbitRadiusPerBurst;

                    orbitStartAngleOffset =
                        orbitCfg.startAngleVary <= 0f
                            ? 0f
                            : orbitCfg.startAngleVaryScope == RandomScope.PerShot
                                ? RandomRange(orbitCfg.startAngleVary)
                                : orbitAnglePerBurst;
                }

                BulletAdvancedConfig advanced = new BulletAdvancedConfig
                {
                    telegraph = telegraphCfg,
                    homing = homingCfg,
                    orbit = orbitCfg,
                    orbitCenter = orbitCfg != null ? owner : null,
                    orbitRadiusOffset = orbitRadiusOffset,
                    orbitStartAngleOffset = orbitStartAngleOffset,
                    homingSpreadAngle = spreadCompatibilityAngle,
                    homingKeepSpread = homingCfg != null ? Mathf.Clamp01(homingCfg.keepSpread) : 0f,
                    waypointRotation = waypointRotation,
                    waypointOffset = waypointOffset
                };

                if (hasWaypointFlow)
                {
                    // flowSpeedSetting = 0 -> ใช้ความเร็วของกระสุนเอง (Initial Speed / Base Speed)
                    advanced.waypointPath = bulletFlowPath;
                    advanced.waypointEnd = flowEnd;
                    advanced.waypointSpeed =
                        flowSpeedSetting > 0f
                            ? flowSpeedSetting
                            : (usePatternSpeed
                                ? initialSpeed
                                : bullet.DefaultSpeed);
                    advanced.waypointSpeedMode =
                        element.stream != null
                            ? element.stream.speedMode
                            : PathSpeedMode.PathTiming;
                }

                bullet.ConfigureMotion(
                    usePatternSpeed,
                    initialSpeed,
                    element.motion.speedTimeline,
                    element.motion.directionTimeline,
                    element.motion.burstTimeline,
                    patternCallChain,
                    resolvedPath,
                    directionCatchUp,
                    advanced);

                if (telegraphCfg != null && telegraphCfg.showOncePerPattern)
                    dangerLineShown = true;
            }
        }

        private static Vector3 pathTangentFallback(Vector3 value)
        {
            if (value.sqrMagnitude > 0.000001f)
                return value.normalized;
            return Vector3.right;
        }

        private static Vector3 pathHasInitialTangent(Vector3 start, Vector3 control, Vector3 end)
        {
            Vector3 tangent = control - start;
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = end - start;
            return pathTangentFallback(tangent);
        }

        private static Vector3 RotateVector(Vector3 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector3(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos,
                value.z);
        }

        private static float FollowSpreadOffset(float spreadAngle, float totalSpread)
        {
            if (Mathf.Abs(totalSpread) <= 0.0001f)
                return 0f;

            float wrapped = Mathf.DeltaAngle(0f, spreadAngle);
            float half = Mathf.Max(0.0001f, Mathf.Abs(totalSpread) * 0.5f);
            return Mathf.Clamp(wrapped / half, -1f, 1f);
        }

        private Vector3 CalculateBaseSpawnPosition(BulletSpawnSettings spawn)
        {
            switch (spawn.mode)
            {
                case BulletSpawnMode.Offset:
                    return owner.position + (Vector3)spawn.offset;

                default:
                    return owner.position;
            }
        }

        private Vector2 CalculateCenterDirection(
            BulletDirectionSettings settings,
            Vector3 spawnPosition,
            Transform player,
            int repeatIndex,
            int repeatCount)
        {
            Vector2 result;

            switch (settings.mode)
            {
                case BulletDirectionMode.AimAtPlayer:
                case BulletDirectionMode.TowardPlayer:
                    result = DirectionToPlayer(spawnPosition, player);
                    break;

                case BulletDirectionMode.AwayFromPlayer:
                    result = -DirectionToPlayer(spawnPosition, player);
                    break;

                case BulletDirectionMode.Random:
                    result = Rotate(Vector2.right, RandomRange(360f));
                    break;

                case BulletDirectionMode.Rotating:
                    result = AngleToVector(
                        settings.angle +
                        settings.rotationPerRepeat * repeatIndex);
                    break;

                case BulletDirectionMode.RelativeToOwner:
                case BulletDirectionMode.FixedAngle:
                default:
                    result = AngleToVector(settings.angle);
                    break;
            }

            if (settings.spreadDistribution == SpreadDistribution.PerRepeat)
            {
                float repeatSpreadOffset =
                    CalculateSpreadOffset(repeatIndex, repeatCount, settings.spread, settings.spreadShape);

                result = Rotate(result, repeatSpreadOffset);
            }

            if (result.sqrMagnitude < 0.000001f)
                result = Vector2.down;

            return result.normalized;
        }

        private Vector2 DirectionToPlayer(
            Vector3 spawnPosition,
            Transform player)
        {
            if (player == null)
                return Vector2.down;

            Vector2 result =
                (Vector2)player.position -
                (Vector2)spawnPosition;

            if (result.sqrMagnitude < 0.000001f)
                return Vector2.down;

            return result.normalized;
        }

        private float CalculateSpreadOffset(
            int index,
            int count,
            float spread,
            SpreadShape shape)
        {
            if (Mathf.Abs(spread) <= 0.0001f)
                return 0f;

            // Shape แบบสุ่มไม่ขึ้นกับ index/count จึงต้องเช็คก่อน guard (count <= 1)
            // ไม่งั้นยิงนัดเดียว/Repeat เดียวจะไม่สุ่มเลย
            switch (shape)
            {
                case SpreadShape.RandomUniform:
                    // RandomRange(r) = สุ่ม -r..+r เท่าๆ กัน
                    return RandomRange(spread * 0.5f);

                case SpreadShape.RandomCluster:
                {
                    if (random == null) ResetRandom();

                    double u1 = 1.0 - random.NextDouble();
                    double u2 = random.NextDouble();

                    // Box-Muller -> standard normal, clamp ที่ ±3 sigma กันหางยาวเกินขอบ
                    double stdNormal =
                        Math.Sqrt(-2.0 * Math.Log(u1)) *
                        Math.Cos(2.0 * Math.PI * u2);

                    float n = Mathf.Clamp((float)(stdNormal / 3.0), -1f, 1f);
                    return n * spread * 0.5f;
                }
            }

            if (count <= 1)
                return 0f;

            switch (shape)
            {
                case SpreadShape.Gaussian:
                {
                    // t: -1..1 กระจายเท่าๆ กัน แล้ววาร์ปด้วย power curve (sign(t)*t^2)
                    // เพื่อให้ index ที่ห่างกันเท่าๆ กัน แมพไปเป็นมุมที่ "หนาแน่นตรงกลาง ห่างที่ขอบ"
                    float t = (2f * index / (count - 1)) - 1f;
                    float warped = Mathf.Sign(t) * t * t;
                    return warped * spread * 0.5f;
                }

                default: // Even
                    if (Mathf.Abs(Mathf.Abs(spread) - 360f) < 0.0001f)
                        return (spread / count) * index;

                    float step = spread / (count - 1);
                    return -spread * 0.5f + step * index;
            }
        }

        private float GetPerPatternOrbitAngleRandom(OrbitSettings key, float range)
        {
            if (perPatternOrbitAngleValues.TryGetValue(key, out float value))
                return value;

            value = RandomRange(range);
            perPatternOrbitAngleValues[key] = value;
            return value;
        }

        private float GetPerPatternRandom(object key, float range)
        {
            if (perPatternRandomValues.TryGetValue(key, out float value))
                return value;

            value = RandomRange(range);
            perPatternRandomValues[key] = value;
            return value;
        }

        private float RandomRange(float range)
        {
            if (random == null)
                ResetRandom();

            return (float)(random.NextDouble() * 2.0 - 1.0) *
                   Mathf.Abs(range);
        }

        private float RandomUnit01()
        {
            if (random == null)
                ResetRandom();

            return (float)random.NextDouble();
        }

        private static Vector2 AngleToVector(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians));
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }
    }

    // ============================================================
    // LEGACY COMPATIBILITY DATA
    // BossV1 can keep using these types.
    // EnemyBulletPatternV2 does NOT use them.
    // ============================================================

    [Serializable]
    public class ShootElement
    {
        public GameObject bulletPrefab;
        public int bulletCount = 1;
        public float spreadAngle = 0f;
        public bool aimAtPlayer = false;
        public Vector2 baseDirection = Vector2.down;
        public Vector2 firePointOffset = Vector2.zero;

        public bool overrideSpeed = false;
        public float speed = 6f;

        public bool overrideAcceleration = false;
        public float acceleration = 0f;
        public float minSpeed = 0f;
        public float maxSpeed = 50f;
    }

    [Serializable]
    public class ShootBurst
    {
        public List<ShootElement> elements =
            new List<ShootElement>();

        public float delayAfter = 0.5f;
        public int repeatCount = 1;
        public float repeatInterval = 0.1f;
        public float angleOffsetPerRepeat = 0f;
    }

    [Serializable]
    public class ShootGroup
    {
        public string groupName = "New Group";
        public List<ShootBurst> pattern =
            new List<ShootBurst>();

        public bool loopPattern = true;

        [NonSerialized] public int currentBurstIndex;
        [NonSerialized] public float delayTimer;
        [NonSerialized] public bool hasFiredCurrentBurst;
        [NonSerialized] public bool patternFinished;
        [NonSerialized] public int currentRepeatIndex;
        [NonSerialized] public float repeatTimer;

        public void ResetState()
        {
            currentBurstIndex = 0;
            delayTimer = 0f;
            hasFiredCurrentBurst = false;
            patternFinished = false;
            currentRepeatIndex = 0;
            repeatTimer = 0f;
        }
    }

    /// <summary>
    /// Compatibility executor for BossV1.
    /// The old data API stays available, but bullets are now EnemyBulletV2.
    /// </summary>
    public class ShootPatternExecutor
    {
        private readonly List<ShootGroup> groups;

        public ShootPatternExecutor(List<ShootGroup> groups)
        {
            this.groups = groups;
        }

        public void Tick(
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            if (groups == null) return;

            for (int g = 0; g < groups.Count; g++)
                UpdateGroup(
                    groups[g],
                    ownerPosition,
                    playerTransform);
        }

        public void ResetAllGroups()
        {
            if (groups == null) return;

            for (int i = 0; i < groups.Count; i++)
                groups[i]?.ResetState();
        }

        private void UpdateGroup(
            ShootGroup group,
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            if (group == null ||
                group.pattern == null ||
                group.pattern.Count == 0 ||
                group.patternFinished)
                return;

            ShootBurst burst =
                group.pattern[group.currentBurstIndex];

            int totalRepeats =
                Mathf.Max(burst.repeatCount, 1);

            if (!group.hasFiredCurrentBurst)
            {
                FireBurst(
                    burst,
                    0,
                    ownerPosition,
                    playerTransform);

                group.hasFiredCurrentBurst = true;
                group.currentRepeatIndex = 0;
                group.repeatTimer = 0f;
                group.delayTimer = 0f;
                return;
            }

            if (group.currentRepeatIndex < totalRepeats - 1)
            {
                float interval =
                    Mathf.Max(0f, burst.repeatInterval);

                if (interval <= 0f)
                {
                    group.currentRepeatIndex++;

                    FireBurst(
                        burst,
                        group.currentRepeatIndex,
                        ownerPosition,
                        playerTransform);

                    return;
                }

                group.repeatTimer += Time.deltaTime;

                while (group.repeatTimer >= interval &&
                       group.currentRepeatIndex < totalRepeats - 1)
                {
                    group.repeatTimer -= interval;
                    group.currentRepeatIndex++;

                    FireBurst(
                        burst,
                        group.currentRepeatIndex,
                        ownerPosition,
                        playerTransform);
                }

                return;
            }

            group.delayTimer += Time.deltaTime;

            if (group.delayTimer >=
                Mathf.Max(0f, burst.delayAfter))
            {
                group.currentBurstIndex++;
                group.hasFiredCurrentBurst = false;
                group.currentRepeatIndex = 0;
                group.repeatTimer = 0f;

                if (group.currentBurstIndex >=
                    group.pattern.Count)
                {
                    if (group.loopPattern)
                        group.currentBurstIndex = 0;
                    else
                        group.patternFinished = true;
                }
            }
        }

        private void FireBurst(
            ShootBurst burst,
            int repeatIndex,
            Vector3 ownerPosition,
            Transform playerTransform)
        {
            for (int i = 0;
                 i < burst.elements.Count;
                 i++)
            {
                ShootElement element =
                    burst.elements[i];

                if (element == null ||
                    element.bulletPrefab == null)
                    continue;

                Vector3 firePosition =
                    ownerPosition +
                    (Vector3)element.firePointOffset;

                Vector2 center =
                    element.aimAtPlayer
                        ? GetDirectionToPlayer(
                            firePosition,
                            playerTransform)
                        : SafeDirection(
                            element.baseDirection);

                center =
                    Rotate(
                        center,
                        burst.angleOffsetPerRepeat *
                        repeatIndex);

                int count =
                    Mathf.Max(1, element.bulletCount);

                for (int b = 0; b < count; b++)
                {
                    float offset =
                        CalculateAngleOffset(
                            b,
                            count,
                            element.spreadAngle);

                    Vector2 direction =
                        Rotate(center, offset);

                    GameObject obj =
                        PoolManager.Instance.Get(
                            element.bulletPrefab,
                            firePosition,
                            Quaternion.identity);

                    if (obj == null)
                        continue;

                    EnemyBulletV2 bullet =
                        obj.GetComponent<EnemyBulletV2>();

                    if (bullet == null)
                    {
                        PoolManager.Instance.Return(obj);
                        continue;
                    }

                    bullet.SetDirection(direction);

                    if (element.overrideSpeed)
                        bullet.SetSpeed(element.speed);

                    if (element.overrideAcceleration)
                    {
                        bullet.SetAcceleration(
                            element.acceleration,
                            element.minSpeed,
                            element.maxSpeed);
                    }
                }
            }
        }

        private Vector2 GetDirectionToPlayer(
            Vector3 firePosition,
            Transform playerTransform)
        {
            if (playerTransform == null)
                return Vector2.down;

            Vector2 result =
                (Vector2)playerTransform.position -
                (Vector2)firePosition;

            return result.sqrMagnitude < 0.000001f
                ? Vector2.down
                : result.normalized;
        }

        private static Vector2 SafeDirection(
            Vector2 direction)
        {
            return direction.sqrMagnitude < 0.000001f
                ? Vector2.down
                : direction.normalized;
        }

        private static float CalculateAngleOffset(
            int index,
            int count,
            float spreadAngle)
        {
            if (count <= 1)
                return 0f;

            bool fullCircle =
                Mathf.Abs(
                    Mathf.Abs(spreadAngle) - 360f) <
                0.01f;

            if (fullCircle)
                return spreadAngle / count * index;

            float step =
                spreadAngle / (count - 1);

            return -spreadAngle / 2f +
                   step * index;
        }

        private static Vector2 Rotate(
            Vector2 value,
            float degrees)
        {
            float radians =
                degrees * Mathf.Deg2Rad;

            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }
    }
}