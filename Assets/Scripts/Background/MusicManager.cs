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
    ///
    /// ============================================================
    /// FIX 2: Stage Clock - นาฬิกาเพลง (เพิ่มใหม่)
    /// ============================================================
    /// ปัญหาที่เหลือ: การรอ OnSceneExit ยังทำให้ "จุดที่เรียก Play()"
    /// ตรงกับ StageTimer แต่ไม่รับประกันว่า "เสียงออกจริง" ตรงกัน
    /// (เสียงอาจเริ่มช้ากว่าตอนเรียก Play() เช่น clip ยังไม่ preload /
    /// Streaming / DSP buffer และรอบ Restart ที่ clip ถูก cache แล้ว
    /// จะเริ่มเร็วกว่ารอบแรก) รวมถึงเฟรมสะดุดระหว่างเล่นที่ทำให้
    /// StageTimer (นับด้วย Time.deltaTime ที่ถูก clamp) ตามเสียงไม่ทัน
    ///
    /// วิธีแก้: MusicManager มี "Stage Clock" = เวลาที่เพลงฉากปกติ
    /// เล่นมาแล้วจริง ๆ โดยอิง AudioSettings.dspTime (นาฬิกาเสียงของ Unity)
    /// - จุดเริ่มคำนวณจาก AudioSource.timeSamples ตอนที่เสียงออกจริง
    ///   (ไม่ใช่ตอนเรียก Play()) จึงหักค่า latency ออกให้อัตโนมัติ
    /// - หยุดเดินตอน PauseMusic() และเดินต่อตอน ResumeMusic()
    /// - ไม่ถูก Reset ตอนสลับเป็นเพลงบอส/กลับเพลงปกติ (เดินต่อเนื่อง)
    /// - ถูกล้างตอน StopMusic() (เช่น Restart/เปลี่ยน Scene)
    ///
    /// StageSpawnerV1 อ่านค่านี้ผ่าน TryGetStageClock() เพื่อปรับ StageTimer
    /// ให้ตรงกับเพลงเสมอ
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
        // STAGE CLOCK (เพิ่มใหม่)
        // ============================================================
        [Header("Stage Clock (ให้ StageSpawnerV1 ซิงก์ตามเพลง)")]
        [Tooltip(
            "ถ้ารอเสียงออกจริงนานเกินค่านี้ (วินาที) แล้วยังไม่ออก\n" +
            "จะเริ่มนาฬิกาจากเวลาที่เรียก Play() แทน (กันนาฬิกาไม่เริ่มเลย)"
        )]
        [Min(0.1f)]
        [SerializeField] private float stageClockCalibrateTimeout = 1f;

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

        // ============================================================
        // STAGE CLOCK RUNTIME (เพิ่มใหม่)
        // ============================================================

        // true = นาฬิกาพร้อมใช้งานแล้ว (เสียงออกจริงและคำนวณจุดเริ่มเสร็จ)
        private bool stageClockReady;

        // true = เรียก Play() เพลงฉากปกติไปแล้ว กำลังรอให้เสียงออกจริง
        private bool stageClockWaitingForPlayback;

        // AudioSource ที่เล่นเพลงฉากปกติตัวแรก (ใช้อ่าน timeSamples ตอน calibrate)
        private AudioSource stageClockSource;

        // เวลา dsp ตอนเรียก Play() (ใช้เป็น fallback ตอน timeout)
        private double stageClockPlayCallDsp;

        // เวลา dsp ที่ "sample 0" ของเพลงถูกเล่นจริง
        private double stageClockOriginDsp;

        // เวลา dsp รวมที่เพลงถูก Pause ไป (หักออกจากนาฬิกา)
        private double pausedDspTotal;

        // เวลา dsp ตอนเริ่ม Pause ล่าสุด
        private double pauseStartDsp;

        /// <summary>
        /// true เมื่อ Stage Clock พร้อมใช้งาน (เพลงฉากปกติเล่นออกเสียงจริงแล้ว)
        /// </summary>
        public bool HasStageClock => stageClockReady;

        /// <summary>
        /// อ่านเวลา (วินาที) ที่เพลงฉากปกติเล่นมาแล้วจริง
        /// คืน false ถ้านาฬิกายังไม่พร้อม (เช่น เพลงยังไม่เริ่ม หรือถูก StopMusic)
        /// ค่าจะหยุดนิ่งระหว่าง PauseMusic() และไม่ถูก Reset ตอนสลับเพลงบอส
        /// </summary>
        public bool TryGetStageClock(out float seconds)
        {
            if (!stageClockReady)
            {
                seconds = 0f;
                return false;
            }

            double nowDsp = isPaused ? pauseStartDsp : AudioSettings.dspTime;
            seconds = (float)(nowDsp - stageClockOriginDsp - pausedDspTotal);
            return true;
        }

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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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

            // ----------------------------------------------------------
            // FIX 2: รอให้เสียงออกจริง แล้วคำนวณจุดเริ่มของ Stage Clock
            // ----------------------------------------------------------
            UpdateStageClockCalibration();

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

        // ============================================================
        // STAGE CLOCK CALIBRATION (เพิ่มใหม่)
        // ============================================================
        //
        // เรียกทุกเฟรมจาก Update() - ทำงานเฉพาะช่วงที่เรียก Play() เพลงฉากปกติ
        // ไปแล้วแต่ยังไม่รู้ว่าเสียงออกจริงตอนไหน
        //
        // หลักการ: เมื่อ AudioSource.timeSamples > 0 แปลว่าเสียงเริ่มเดินแล้ว
        // ตำแหน่งปัจจุบันของเพลง = timeSamples / frequency วินาที
        // ดังนั้น "เวลา dsp ที่ sample 0 ถูกเล่น" = dspTime ตอนนี้ - ตำแหน่งนั้น
        // วิธีนี้หัก latency ของการเริ่มเสียงออกให้เองโดยไม่ต้องเดาค่า
        //
        private void UpdateStageClockCalibration()
        {
            if (!stageClockWaitingForPlayback || isPaused)
                return;

            if (stageClockSource == null || stageClockSource.clip == null)
            {
                stageClockWaitingForPlayback = false;
                return;
            }

            double nowDsp = AudioSettings.dspTime;

            if (stageClockSource.isPlaying && stageClockSource.timeSamples > 0)
            {
                double playbackPosition =
                    (double)stageClockSource.timeSamples / stageClockSource.clip.frequency;

                CompleteStageClockCalibration(nowDsp - playbackPosition, "timeSamples");
                return;
            }

            // Fallback: รอนานเกินไปแล้วเสียงยังไม่ขยับ - ใช้เวลาตอนเรียก Play() แทน
            if (nowDsp - stageClockPlayCallDsp >= stageClockCalibrateTimeout)
            {
                CompleteStageClockCalibration(stageClockPlayCallDsp, "timeout-fallback");
            }
        }

        private void CompleteStageClockCalibration(double originDsp, string method)
        {
            stageClockOriginDsp = originDsp;
            pausedDspTotal = 0.0;
            stageClockReady = true;
            stageClockWaitingForPlayback = false;

            if (debugTimingLogs)
            {
                float clockNow;
                TryGetStageClock(out clockNow);

                Debug.Log(
                    $"[TimingDebug][Music] StageClock พร้อมแล้ว (วิธี: {method}) ที่ " +
                    $"realtime={Time.realtimeSinceStartup:F3}s, stageClock={clockNow:F3}s " +
                    $"(ค่านี้คือ latency ระหว่างเรียก Play() กับเสียงออกจริง)"
                );
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

            // --------------------------------------------------------
            // FIX 2: เริ่มจับนาฬิกาเพลงตอนเล่น "เพลงฉากปกติ" ครั้งแรกของ Scene
            // (ครั้งต่อ ๆ ไป เช่น กลับจากเพลงบอส ไม่ Reset นาฬิกา)
            // --------------------------------------------------------
            if (clip == normalStageMusic && !stageClockReady && !stageClockWaitingForPlayback)
            {
                stageClockWaitingForPlayback = true;
                stageClockSource = newSource;
                stageClockPlayCallDsp = AudioSettings.dspTime;
            }

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

            // Stage Clock: จดเวลาที่เริ่ม Pause เพื่อหักออกตอน Resume
            pauseStartDsp = AudioSettings.dspTime;

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

            // Stage Clock: บวกช่วงที่ Pause ไปเข้า pausedDspTotal เพื่อให้นาฬิกาเดินต่อจากเดิม
            pausedDspTotal += AudioSettings.dspTime - pauseStartDsp;

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

            // Stage Clock: ล้างทั้งหมด - เพลงรอบถัดไปจะเริ่มนาฬิกาใหม่จาก 0
            stageClockReady = false;
            stageClockWaitingForPlayback = false;
            stageClockSource = null;
            pausedDspTotal = 0.0;
        }
    }
}