using UnityEngine;

namespace PlayerSystem
{
    /// <summary>
    /// แปะไว้บน Player Prefab (GameObject เดียวกับ PlayerControllerV1) - ไม่ต้องแก้ไข PlayerControllerV1.cs เลย
    ///
    /// ตอน Scene ใหม่เริ่มเล่น จะเช็คกับ PlayerStatsManager ว่ามีตำแหน่งที่เซฟไว้จาก Scene ก่อนหน้าหรือไม่
    /// (PlayerStatsManager จับตำแหน่งให้อัตโนมัติตอน Scene เก่ากำลังจะเปลี่ยน ผ่าน LoadingScreenManager.OnSceneEnter)
    ///
    /// ถ้ามี -> ย้าย Player ไปตำแหน่งนั้นทันที (ต่อเนื่องข้ามด่านตามที่ต้องการ)
    /// ถ้าไม่มี (เช่นเข้าด่านแรกสุดของเกมหลังกด New Game) -> ปล่อยให้ Player อยู่ตำแหน่ง Default ที่วางไว้ใน Scene ตามปกติ
    /// </summary>
    public class PlayerPositionRestorer : MonoBehaviour
    {
        private void Start()
        {
            if (PlayerStatsManager.Instance == null) return;

            if (PlayerStatsManager.Instance.TryConsumeSavedPosition(out Vector3 savedPosition))
            {
                transform.position = savedPosition;
            }
        }
    }
}
