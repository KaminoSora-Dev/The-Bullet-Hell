using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    /// <summary>
    /// ตัวกลางจัดการการตั้งค่า Audio ระดับ Global ของเกม
    /// เช่น SFX Volume และสถานะ Pause
    ///
    /// การเล่นเสียงของแต่ละ Object ให้ Object นั้นจัดการ AudioSource ของตัวเอง
    /// โดยกำหนด AudioMixerGroup ผ่าน AudioSource ใน Inspector
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Mixer")]
        [Tooltip("AudioMixer หลักของเกม")]
        [SerializeField] private AudioMixer audioMixer;

        [Tooltip(
            "ชื่อ Exposed Parameter สำหรับ SFX Volume ใน AudioMixer " +
            "เช่น SFXVolume"
        )]
        [SerializeField] private string sfxVolumeParameter = "SFXVolume";

        [Range(0f, 1f)]
        [SerializeField] private float sfxVolume = 1f;

        private bool isPaused;

        public float SfxVolume => sfxVolume;
        public bool IsPaused => isPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Manager ทุกตัวในโปรเจกต์ต้อง DontDestroyOnLoad
            // ไม่งั้นพอเปลี่ยน Scene แล้ว Instance จะถูกทำลายทิ้ง
            // ทำให้ระบบที่เรียก AudioManager.Instance จาก Scene อื่นพัง
            DontDestroyOnLoad(gameObject);

            ApplySfxVolume();
        }

        public void SetSfxVolume(float volume01)
        {
            sfxVolume = Mathf.Clamp01(volume01);
            ApplySfxVolume();
        }

        private void ApplySfxVolume()
        {
            if (audioMixer == null)
                return;

            if (string.IsNullOrWhiteSpace(sfxVolumeParameter))
                return;

            float decibelVolume;

            if (sfxVolume <= 0.0001f)
                decibelVolume = -80f;
            else
                decibelVolume =
                    Mathf.Log10(sfxVolume) * 20f;

            audioMixer.SetFloat(
                sfxVolumeParameter,
                decibelVolume
            );
        }

        public void PauseSFX()
        {
            if (isPaused)
                return;

            isPaused = true;

            AudioSource[] sources =
                FindObjectsOfType<AudioSource>();

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (source == null)
                    continue;

                if (source.outputAudioMixerGroup == null)
                    continue;

                if (source.isPlaying)
                    source.Pause();
            }
        }

        public void ResumeSFX()
        {
            if (!isPaused)
                return;

            isPaused = false;

            AudioSource[] sources =
                FindObjectsOfType<AudioSource>();

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (source == null)
                    continue;

                if (source.outputAudioMixerGroup == null)
                    continue;

                source.UnPause();
            }
        }

        public void StopAllSFX()
        {
            AudioSource[] sources =
                FindObjectsOfType<AudioSource>();

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];

                if (source == null)
                    continue;

                if (source.outputAudioMixerGroup == null)
                    continue;

                source.Stop();
            }

            isPaused = false;
        }
    }
}