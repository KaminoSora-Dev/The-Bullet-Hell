using UnityEngine;
using BossSystem;
using GameFlowSystem;

namespace AudioSystem
{
    /// <summary>
    /// ตัวกลางเล่นเพลงพื้นหลัง (BGM) - Singleton ตัวเดียว
    /// เล่นเพลงฉากปกติตั้งแต่ Start() แล้วสลับไปเพลงบอสอัตโนมัติตอน BossV2 ตัวไหนก็ตาม Activate()
    /// (ฟัง static event ของ BossV2 - ไม่ต้องลาก reference ผูก Boss ตัวใดตัวหนึ่งใน Inspector เลย)
    ///
    /// ใช้ AudioSource คู่ (A/B) สลับกัน Crossfade แทนการตัดเพลงทันที - ปรับ volume ผ่าน Update() ทีละเฟรม
    /// ตาม convention ของโปรเจกต์ (ไม่ใช้ Coroutine)
    ///
    /// ============================================================
    /// FIX: Sync กับ StageTimer (2026)
    /// ============================================================
    /// ปัญหาเดิม: AudioSource.Play() เดินตามเวลาจริงเสมอ ไม่สนใจ
    /// Time.timeScale เลย ในขณะที่ LoadingScreenManager Freeze
    /// (timeScale = 0) ระหว่าง Fade-in/Async Load/Fade-out ทั้งหมด
    /// แต่ Scene ใหม่ (รวม MusicManager) Awake/Start ไปแล้วตั้งแต่ตอน
    /// Async Load เสร็จ - ซึ่งเกิดก่อน FinishTransition() มาก ทำให้
    /// เพลงเริ่มเล่นเดินหน้าไปก่อน StageTimer จะเริ่มนับจริง
    /// (StageTimer หยุดนิ่งตาม Time.deltaTime ที่ = 0 ตอน timeScale = 0)
    ///
    /// วิธีแก้: รอ LoadingScreenManager.OnSceneExit (จุดเดียวกับที่
    /// timeScale กลับเป็น 1 จริง) ก่อนค่อยเริ่มเล่นเพลง แทนที่จะเล่น
    /// ทันทีใน Start()
    /// </summary>
    public class MusicManager : MonoBehaviour
    {
        public static MusicManager Instance { get; private set; }

        [Header("Audio Sources (ลาก 2 ตัวที่วางไว้ล่วงหน้าเป็น Child ของตัวนี้)")]
        [Tooltip("ต้องตั้ง Loop = true บนตัว AudioSource เองทั้งคู่")]
        [SerializeField] private AudioSource sourceA;
        [SerializeField] private AudioSource sourceB;

        [Header("Clips")]
        [SerializeField] private AudioClip normalStageMusic;
        [SerializeField] private AudioClip bossMusic;

        [Header("Crossfade")]
        [Min(0f)]
        [SerializeField] private float crossfadeDuration = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.7f;

        [Tooltip("เล่นเพลงฉากปกติทันทีตอน Start() หรือไม่ - ปิดถ้าอยากให้ระบบอื่นสั่ง PlayNormalStageMusic() เอง")]
        [SerializeField] private bool playNormalMusicOnStart = true;

        // ============================================================
        // DEBUG (ชั่วคราว - ไล่บัคซิงก์ระหว่างเพลงกับ StageTimer)
        // ============================================================
        [Header("Debug")]
        [Tooltip("เปิด Log ชั่วคราวเพื่อไล่บัคซิงก์ระหว่างเพลงกับ StageTimer - ปิดได้เมื่อมั่นใจว่าเสถียรแล้ว")]
        [SerializeField] private bool debugTimingLogs = true;

        private bool wasTimeScaleZero;

        private AudioSource activeSource;
        private AudioSource inactiveSource;

        private bool isCrossfading;
        private float crossfadeTimer;
        private bool isPaused;

        // ============================================================
        // FIX B: รอ Loading Screen จบก่อนเริ่มเพลง
        // ============================================================
        //
        // true = Start() เจอว่ากำลังอยู่ระหว่าง Scene Transition อยู่
        // จึงยังไม่เล่นเพลงทันที - จะเล่นตอน OnSceneExit ยิงแทน
        //
        private bool pendingAutoPlayOnLoadFinished;

        // ============================================================
        // FIX A: Auto Pause/Resume ตาม Time.timeScale (Safety Net)
        // ============================================================
        //
        // true = ระบบนี้เป็นคนสั่ง Pause เอง (ไม่ใช่ระบบภายนอกอย่าง
        // GameFlowManager) - ใช้แยกกับ isPaused ปกติ เพื่อไม่ให้
        // Resume ซ้ำซ้อนกันตอน timeScale กลับมา
        //
        private bool autoPausedByTimeScale;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            activeSource = sourceA;
            inactiveSource = sourceB;

            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] Awake - realtime={Time.realtimeSinceStartup:F3}s, " +
                    $"timeScale={Time.timeScale}"
                );
            }
        }

        private void OnEnable()
        {
            BossV2.OnAnyBossActivated += HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny += HandleBossDefeated;

            // FIX B: ฟัง OnSceneExit ของ LoadingScreenManager (ถ้ามี)
            // เพื่อรู้ว่า Loading Screen จบจริงเมื่อไหร่ (จุดเดียวกับที่
            // Time.timeScale กลับเป็น 1 และ StageTimer เริ่มเดินจริง)
            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.OnSceneExit += HandleLoadingSceneExit;
        }

        private void OnDisable()
        {
            BossV2.OnAnyBossActivated -= HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny -= HandleBossDefeated;

            if (LoadingScreenManager.Instance != null)
                LoadingScreenManager.Instance.OnSceneExit -= HandleLoadingSceneExit;
        }

        private void Start()
        {
            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] Start - realtime={Time.realtimeSinceStartup:F3}s, " +
                    $"timeScale={Time.timeScale}, playNormalMusicOnStart={playNormalMusicOnStart}"
                );
            }

            if (!playNormalMusicOnStart)
                return;

            // --------------------------------------------------------
            // FIX B: ถ้ากำลังอยู่กลาง Scene Transition (โหลดผ่าน
            // LoadingScreenManager) ให้ "รอ" จนกว่า Loading Screen จะจบ
            // จริง แทนที่จะเล่นทันที - เพราะ AudioSource.Play() เดินตาม
            // เวลาจริงเสมอ ไม่ว่า timeScale จะเป็น 0 อยู่หรือไม่
            // --------------------------------------------------------
            if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading)
            {
                pendingAutoPlayOnLoadFinished = true;

                if (debugTimingLogs)
                {
                    Debug.Log(
                        "[TimingDebug][Music] Start - กำลังอยู่ระหว่าง Loading Screen " +
                        "→ เลื่อนการเล่นเพลงไปรอ OnSceneExit แทน"
                    );
                }

                return;
            }

            // ไม่ได้มาจาก Loading Screen (เช่นกด Play ตรงจาก Editor) - เล่นทันทีเหมือนเดิม
            PlayNormalStageMusic();
        }

        private void HandleLoadingSceneExit()
        {
            if (!pendingAutoPlayOnLoadFinished)
                return;

            pendingAutoPlayOnLoadFinished = false;

            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] LoadingScreenManager.OnSceneExit → เริ่มเล่นเพลงจริงที่ " +
                    $"realtime={Time.realtimeSinceStartup:F3}s, timeScale={Time.timeScale} " +
                    "(ควรตรงกับจุดที่ StageTimer เริ่มเดินพอดี)"
                );
            }

            PlayNormalStageMusic();
        }

        private void Update()
        {
            // ----------------------------------------------------------
            // DEBUG: จับจังหวะที่ Time.timeScale สลับ 0 <-> ไม่ใช่ 0
            // ----------------------------------------------------------
            if (debugTimingLogs)
            {
                bool timeScaleIsZeroLog = Time.timeScale <= 0f;
                if (timeScaleIsZeroLog != wasTimeScaleZero)
                {
                    Debug.Log(timeScaleIsZeroLog
                        ? $"[TimingDebug][Music] timeScale → 0 ที่ realtime={Time.realtimeSinceStartup:F3}s"
                        : $"[TimingDebug][Music] timeScale → {Time.timeScale} (resume) ที่ realtime={Time.realtimeSinceStartup:F3}s, activeSource.time={activeSource.time:F3}s");
                    wasTimeScaleZero = timeScaleIsZeroLog;
                }
            }

            // ----------------------------------------------------------
            // FIX A: Auto Pause/Resume ตาม Time.timeScale (Safety Net)
            // ----------------------------------------------------------
            //
            // ครอบคลุมเคสอื่นที่ timeScale = 0 นอกเหนือจาก Loading
            // (เช่น ลืมเรียก PauseMusic() ตอนเพิ่มระบบ Freeze ใหม่ในอนาคต)
            //
            // สำคัญ: เช็ค anySourcePlaying ด้วย - ถ้ายังไม่มีเพลงเล่นอยู่เลย
            // (เช่นตอนกำลังรอ Loading Screen จบตาม FIX B ด้านบน) จะไม่ auto
            // pause เด็ดขาด ไม่งั้น isPaused จะค้างเป็น true แล้วไปบล็อก
            // CrossfadeTo() ตอน OnSceneExit เรียก PlayNormalStageMusic()
            //
            bool timeScaleIsZero = Time.timeScale <= 0f;
            bool anySourcePlaying =
                (activeSource != null && activeSource.isPlaying) ||
                (inactiveSource != null && inactiveSource.isPlaying);

            if (timeScaleIsZero && !isPaused && !autoPausedByTimeScale && anySourcePlaying)
            {
                autoPausedByTimeScale = true;
                PauseMusic();
            }
            else if (!timeScaleIsZero && autoPausedByTimeScale)
            {
                autoPausedByTimeScale = false;
                ResumeMusic();
            }

            if (!isCrossfading) return;

            crossfadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(crossfadeTimer / Mathf.Max(crossfadeDuration, 0.0001f));

            activeSource.volume = Mathf.Lerp(0f, musicVolume, t);
            inactiveSource.volume = Mathf.Lerp(musicVolume, 0f, t);

            if (t >= 1f)
            {
                isCrossfading = false;
                inactiveSource.Stop();
                inactiveSource.volume = 0f;
            }
        }

        private void HandleBossActivated() => PlayBossMusic();
        private void HandleBossDefeated() => PlayNormalStageMusic();

        public void PlayNormalStageMusic() => CrossfadeTo(normalStageMusic);
        public void PlayBossMusic() => CrossfadeTo(bossMusic);

        /// <summary>
        /// สลับไปเล่นคลิปที่กำหนด - ถ้าคลิปเดิมกำลังเล่นอยู่แล้วจะไม่ทำอะไร (กันเพลงกระตุกซ้ำเวลามีบอสหลายตัว activate ใกล้กัน)
        /// </summary>
        public void CrossfadeTo(AudioClip clip)
        {
            if (isPaused)
                return;

            if (clip == null)
                return;

            if (activeSource.clip == clip && activeSource.isPlaying)
                return;

            AudioSource newSource = inactiveSource;
            AudioSource oldSource = activeSource;

            newSource.clip = clip;
            newSource.volume = 0f;
            newSource.Play();

            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] CrossfadeTo → Play() clip='{clip.name}' ที่ " +
                    $"realtime={Time.realtimeSinceStartup:F3}s, timeScale={Time.timeScale}, " +
                    $"newSource.time={newSource.time:F3}s"
                );
            }

            activeSource = newSource;
            inactiveSource = oldSource;

            isCrossfading = true;
            crossfadeTimer = 0f;
        }

        public void SetMusicVolume(float volume01)
        {
            musicVolume = Mathf.Clamp01(volume01);
        }

        public void PauseMusic()
        {
            if (isPaused)
                return;

            isPaused = true;

            if (sourceA != null && sourceA.isPlaying)
                sourceA.Pause();

            if (sourceB != null && sourceB.isPlaying)
                sourceB.Pause();

            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] PauseMusic() เรียกที่ realtime={Time.realtimeSinceStartup:F3}s " +
                    $"(autoPausedByTimeScale={autoPausedByTimeScale})"
                );
            }
        }

        public void ResumeMusic()
        {
            if (!isPaused)
                return;

            isPaused = false;

            if (sourceA != null)
                sourceA.UnPause();

            if (sourceB != null)
                sourceB.UnPause();

            if (debugTimingLogs)
            {
                Debug.Log(
                    $"[TimingDebug][Music] ResumeMusic() เรียกที่ realtime={Time.realtimeSinceStartup:F3}s " +
                    $"(autoPausedByTimeScale={autoPausedByTimeScale})"
                );
            }
        }
        public void StopMusic()
        {
            if (sourceA != null)
                sourceA.Stop();

            if (sourceB != null)
                sourceB.Stop();

            sourceA.volume = 0f;
            sourceB.volume = 0f;

            isCrossfading = false;
            crossfadeTimer = 0f;
            isPaused = false;
            autoPausedByTimeScale = false;
            pendingAutoPlayOnLoadFinished = false;
        }
    }
}