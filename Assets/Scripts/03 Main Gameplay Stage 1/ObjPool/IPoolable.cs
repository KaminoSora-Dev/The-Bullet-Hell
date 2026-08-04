namespace PoolingSystem
{
    /// <summary>
    /// ให้ MonoBehaviour ที่จะถูกจัดการโดย PoolManager implement interface นี้
    /// เพื่อรับรู้จังหวะ "ถูกหยิบมาใช้" กับ "ถูกคืนกลับ pool" แล้วจัดการ reset state ของตัวเอง
    ///
    /// ทำไมต้องมี interface นี้แทนที่จะให้ PoolManager ไปยุ่งกับ field ภายในของแต่ละคลาสตรงๆ:
    /// PoolManager ไม่ควรรู้จัก/แก้ field เฉพาะของ EnemyBulletV1, EnemyV1 ฯลฯ เพราะแต่ละคลาส
    /// มี state ที่ต้อง reset ไม่เหมือนกันเลย (กระสุนแค่ direction, Enemy มีทั้ง HP/movement/blink)
    /// ให้แต่ละคลาส "รู้จักตัวเองดีที่สุด" เป็นคนจัดการ reset ของตัวเอง ผ่าน method มาตรฐานที่ PoolManager เรียกได้เหมือนกันหมด
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// เรียกโดย PoolManager.Get() หลังจากตั้งตำแหน่ง/เปิด GameObject แล้ว
        /// ให้ reset state ทุกอย่างที่เกี่ยวกับ "รอบการเล่นนี้" กลับเป็นค่าเริ่มต้นตรงนี้
        /// </summary>
        void OnSpawnFromPool();

        /// <summary>
        /// เรียกโดย PoolManager.Return() ก่อนจะปิด GameObject กลับเข้า pool
        /// ส่วนใหญ่ไม่ต้องทำอะไรมาก แต่เผื่อไว้ให้คลาสที่มี state ค้าง (เช่น coroutine, timer) มาเคลียร์ตรงนี้ได้
        /// </summary>
        void OnReturnToPool();
    }
}