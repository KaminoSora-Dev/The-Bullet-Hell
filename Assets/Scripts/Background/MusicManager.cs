using UnityEngine;
using BossSystem;

namespace AudioSystem
{
    /// <summary>
    /// ตัวกลางเล่นเพลงพื้นหลัง (BGM) - Singleton ตัวเดียว
    /// เล่นเพลงฉากปกติตั้งแต่ Start() แล้วสลับไปเพลงบอสอัตโนมัติตอน BossV2 ตัวไหนก็ตาม Activate()
    /// (ฟัง static event ของ BossV2 - ไม่ต้องลาก reference ผูก Boss ตัวใดตัวหนึ่งใน Inspector เลย)
    ///
    /// ใช้ AudioSource คู่ (A/B) สลับกัน Crossfade แทนการตัดเพลงทันที - ปรับ volume ผ่าน Update() ทีละเฟรม
    /// ตาม convention ของโปรเจกต์ (ไม่ใช้ Coroutine)
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

        private AudioSource activeSource;
        private AudioSource inactiveSource;

        private bool isCrossfading;
        private float crossfadeTimer;
        private bool isPaused;

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
        }

        private void OnEnable()
        {
            BossV2.OnAnyBossActivated += HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny += HandleBossDefeated;
        }

        private void OnDisable()
        {
            BossV2.OnAnyBossActivated -= HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny -= HandleBossDefeated;
        }

        private void Start()
        {
            if (playNormalMusicOnStart)
                PlayNormalStageMusic();
        }

        private void Update()
        {
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
        }
    }
}
