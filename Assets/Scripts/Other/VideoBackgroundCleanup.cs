using UnityEngine;
using UnityEngine.Video;

namespace MenuSystemV1
{
    /// <summary>
    /// แปะสคริปต์นี้ไว้บน GameObject เดียวกับ VideoPlayer ของพื้นหลังเมนู
    /// (ตัวเดียวกับที่ Render Mode ตั้งเป็น "Render Texture")
    ///
    /// ROOT CAUSE ของปัญหา "วิดีโอค้างไปโผล่ที่ Stage01":
    /// VideoPlayer เล่นแบบ Render Mode = Render Texture โดยอ้างอิง RenderTexture
    /// ที่เป็น Asset ไฟล์จริงในโปรเจกต์ (สร้างจาก Assets > Create > Render Texture)
    /// ไม่ใช่สร้างขึ้นตอนรันไทม์ผ่านโค้ด
    ///
    /// RenderTexture Asset ไม่ได้ผูกอายุ (lifecycle) กับ Scene เลย ต่อให้ GameObject
    /// ของวิดีโอถูกทำลายไปตาม Scene ปกติแล้ว (ไม่มี DontDestroyOnLoad ก็ตาม)
    /// ข้อมูลเฟรมสุดท้ายที่เขียนลงไปใน RenderTexture นั้นยังค้างอยู่ใน GPU memory
    /// ของตัว Asset ต่อไปเรื่อยๆ จนกว่าจะมีคนสั่ง Clear/Release มันทิ้ง
    /// - ถ้ามีอะไรใน Stage01 (Material/RawImage/Renderer) อ้างอิงถึง RenderTexture
    ///   ตัวเดียวกันนี้อยู่ (ไม่ว่าจะตั้งใจหรือไม่) มันจะโชว์เฟรมค้างนั้นออกมาทันที
    ///
    /// FIX:
    /// สคริปต์นี้จะสั่ง Stop() วิดีโอ แล้ว Release() ตัว RenderTexture ทิ้งตอนที่
    /// object ถูกทำลาย (ออกจากเมนู/เปลี่ยน Scene) เพื่อล้างเฟรมค้างไม่ให้เหลือ
    /// อะไรให้ Scene อื่นไปโชว์ต่อได้ - Unity จะสร้าง native texture ใหม่ให้เอง
    /// (ว่างเปล่า) ในครั้งถัดไปที่มีการใช้งาน RenderTexture นี้
    ///
    /// หมายเหตุ: ทำงานแบบ defensive คือ Clear ทิ้งเสมอตอนออกจากเมนู
    /// ไม่ว่า Stage01 จะมีอะไรอ้างอิง RenderTexture นี้อยู่จริงหรือไม่ก็ตาม
    /// เพื่อป้องกันปัญหานี้ล่วงหน้าแม้จะมีคนเผลอเอา Asset ตัวนี้ไปใช้ที่อื่นในอนาคต
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoBackgroundCleanup : MonoBehaviour
    {
        private VideoPlayer videoPlayer;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        // OnDestroy จะถูกเรียกตอน Scene unload ตามปกติ (object นี้ไม่มี
        // DontDestroyOnLoad อยู่แล้ว) - เกิดขึ้นก่อนที่ Scene ใหม่จะเริ่ม Awake
        private void OnDestroy()
        {
            ClearRenderTexture();
        }

        // เผื่อกรณี object ถูกปิด (SetActive(false)) ก่อนที่จะถูกทำลายจริง
        // เช่น ถ้ามี Manager อื่นสั่งปิดวิดีโอไว้ก่อนเปลี่ยน Scene
        private void OnDisable()
        {
            ClearRenderTexture();
        }

        private void ClearRenderTexture()
        {
            if (videoPlayer == null)
                return;

            RenderTexture rt = videoPlayer.targetTexture;

            if (rt == null)
                return;

            // หยุดวิดีโอก่อน กันไม่ให้มีการเขียนเฟรมทับระหว่าง Clear
            if (videoPlayer.isPlaying)
                videoPlayer.Stop();

            // Release native representation ของ RenderTexture ทิ้ง
            // ครั้งถัดไปที่มีใครใช้งาน RenderTexture asset ตัวนี้
            // Unity จะสร้าง texture ใหม่ให้เอง (เนื้อหาว่างเปล่า/ดำ)
            // ไม่ต้องกังวลว่าจะทำลาย Asset ไฟล์ - Release() ล้างแค่ข้อมูลใน GPU memory
            rt.Release();
        }
    }
}