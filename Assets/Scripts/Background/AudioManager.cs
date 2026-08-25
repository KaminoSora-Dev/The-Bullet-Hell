using UnityEngine;
using UnityEngine.Audio;

namespace AudioSystem
{
    /// <summary>
    /// ตัวกลางจัดการการตั้งค่า Audio ระดับ Global ของเกม
    /// เช่น SFX Volume, สถานะ Pause และจุดกลางสำหรับเล่นเสียง SFX (PlaySFX)
    ///
    /// การเล่นเสียงของแต่ละ Object (กระสุน, ศัตรู, บอส ฯลฯ) ให้เรียกผ่าน
    /// AudioManager.Instance.PlaySFX(clip) แทนการสร้าง AudioSource เอง
    /// หรือใช้ AudioSource.PlayClipAtPoint() เพราะการสร้าง/ทำลาย AudioSource
    /// ถี่ๆ (เช่น กระสุนโดนพร้อมกันหลายสิบลูกในเฟรมเดียว) คือสาเหตุหลักที่ทำให้เกิด
    /// AudioSampleProvider buffer overflow (sample frames ถูกทิ้ง)
    ///
    /// ระบบนี้แก้ด้วย 2 กลไก:
    /// 1) SFX Pool - AudioSource จำนวนคงที่ วนใช้ซ้ำ (Round-Robin) ไม่สร้างใหม่ทุกครั้ง
    /// 2) Throttle ต่อคลิป - คลิปเดียวกันจะไม่เล่นซ้ำถี่เกินกำหนด กันเสียงเดียวกัน
    ///    ยิงเข้า Audio Thread พร้อมกันหลายสิบครั้งในเฟรมเดียว
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

        [Header("SFX Playback (ป้องกัน Buffer Overflow)")]
        [Tooltip(
            "AudioMixerGroup สำหรับเสียง SFX ที่เล่นผ่าน PlaySFX() " +
            "ปล่อยว่างได้ถ้ายังไม่มี Mixer Group สำหรับ SFX"
        )]
        [SerializeField] private AudioMixerGroup sfxMixerGroup;

        [Tooltip(
            "จำนวน AudioSource ที่เตรียมไว้ล่วงหน้าสำหรับเล่น SFX พร้อมกันทั้งหมดในเกม " +
            "ต้องมากกว่าผลรวมของ maxConcurrentPerClip ของเสียงทุกชนิดที่อาจเล่นพร้อมกัน " +
            "(bullet hell ที่มีศัตรู/กระสุนเยอะ แนะนำ 16-24 ตัว ไม่ควรเกิน ~32)"
        )]
        [SerializeField] private int sfxPoolSize = 16;

        [Tooltip(
            "จำนวนครั้งสูงสุดที่คลิปเสียง 'เดียวกัน' จะเล่นพร้อมกันได้ในเวลาเดียวกัน " +
            "(ค่า default ถ้าไม่ระบุตอนเรียก PlaySFX) เช่น เสียงยิงกระสุนเล่นซ้อนกันได้ไม่เกิน 4 ครั้ง " +
            "ถ้าครบแล้วเสียงที่เกินมาจะถูกข้ามไปเลย ไม่เล่นทับ ไม่เข้าคิว " +
            "นี่คือกลไกหลักที่กัน AudioSampleProvider buffer overflow ตอนกระสุน/ศัตรูเสกพร้อมกันเยอะๆ"
        )]
        [SerializeField] private int defaultMaxConcurrentPerClip = 4;

        private AudioSource[] sfxPool;
        private int sfxPoolCursor;

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

            BuildSfxPool();
            ApplySfxVolume();
        }

        /// <summary>
        /// สร้าง AudioSource ล่วงหน้าตามจำนวน sfxPoolSize
        /// เป็นลูกของ AudioManager เอง จะได้ DontDestroyOnLoad ตามไปด้วย
        /// </summary>
        private void BuildSfxPool()
        {
            if (sfxPoolSize < 1)
                sfxPoolSize = 1;

            sfxPool = new AudioSource[sfxPoolSize];

            for (int i = 0; i < sfxPoolSize; i++)
            {
                GameObject voiceObj = new GameObject("SfxVoice_" + i);
                voiceObj.transform.SetParent(transform);

                AudioSource source = voiceObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;

                // 2D เหมาะกับเกม bullet hell แบบมุมมองบนลงล่างทั่วไป
                // ถ้าอยากได้เสียงตามตำแหน่งจริงๆ ค่อยปรับ spatialBlend ทีหลัง
                source.spatialBlend = 0f;
                source.outputAudioMixerGroup = sfxMixerGroup;

                sfxPool[i] = source;
            }
        }

        /// <summary>
        /// จุดกลางสำหรับเล่นเสียง SFX ทุกจุดในเกม (กระสุน, ศัตรู, บอส, UI ฯลฯ)
        /// ให้เรียกผ่านจุดนี้แทนการสร้าง AudioSource เอง หรือ PlayClipAtPoint
        ///
        /// ป้องกัน Buffer Overflow ด้วย Hard Concurrency Cap: ถ้าคลิปเดียวกันนี้
        /// กำลังเล่นซ้อนกันอยู่ครบ maxConcurrentPerClip แล้ว (เช่น เสียงยิงกระสุน
        /// เล่นซ้อนอยู่ 4 ครั้งพร้อมกัน) การเรียกครั้งใหม่จะถูก "ข้ามไปเลย" ไม่เล่นทับ
        /// ไม่เข้าคิว เพื่อไม่ให้ตอนเสกกระสุน/ศัตรูพร้อมกันเยอะๆ ยัดเสียงเข้า
        /// Audio Thread เกินที่มันรับไหว
        /// </summary>
        /// <param name="clip">คลิปเสียงที่จะเล่น</param>
        /// <param name="volumeScale">ตัวคูณ Volume เฉพาะเสียงนี้ (0-1) เผื่ออยากให้บางเสียงเบากว่าปกติ</param>
        /// <param name="pitch">Pitch ของเสียง ปกติ 1 = ปกติ</param>
        /// <param name="maxConcurrentPerClip">
        /// จำนวนครั้งสูงสุดที่คลิปนี้เล่นซ้อนกันได้ ณ เวลาเดียวกัน
        /// ปล่อยเป็นค่า default (-1) เพื่อใช้ defaultMaxConcurrentPerClip จาก Inspector
        /// หรือใส่ค่าเฉพาะเจาะจง เช่น เสียงระเบิดบอสอาจอยากให้เล่นซ้อนได้แค่ 1-2
        /// </param>
        public void PlaySFX(
            AudioClip clip,
            float volumeScale = 1f,
            float pitch = 1f,
            int maxConcurrentPerClip = -1)
        {
            if (clip == null)
                return;

            if (sfxPool == null || sfxPool.Length == 0)
                return;

            int cap = maxConcurrentPerClip > 0
                ? maxConcurrentPerClip
                : defaultMaxConcurrentPerClip;

            if (cap < 1)
                cap = 1;

            // --- Hard Concurrency Cap ---
            // นับ voice ใน Pool ที่ "กำลังเล่นคลิปตัวเดียวกันนี้อยู่จริง" ตอนนี้
            // ถ้าครบ cap แล้ว ให้ข้ามการเล่นครั้งนี้ไปเลย ไม่เล่นทับ ไม่เล่นเพิ่ม
            int playingSameClipCount = 0;

            for (int i = 0; i < sfxPool.Length; i++)
            {
                AudioSource voice = sfxPool[i];

                if (voice == null)
                    continue;

                if (voice.isPlaying && voice.clip == clip)
                {
                    playingSameClipCount++;

                    if (playingSameClipCount >= cap)
                        return; // ครบโควต้าเสียงนี้แล้ว ไม่เล่นเพิ่ม
                }
            }

            AudioSource freeVoice = GetFreeVoice();

            freeVoice.pitch = pitch;
            freeVoice.volume = Mathf.Clamp01(sfxVolume * volumeScale);
            freeVoice.clip = clip;
            freeVoice.Play();
        }

        /// <summary>
        /// หา AudioSource ที่ว่าง (ไม่ได้กำลังเล่นอะไรอยู่) จาก Pool ก่อน
        /// ถ้าไม่มีตัวว่างเลย (Pool เต็มทุกตัว) จะวนแบบ Round-Robin แล้วแย่งเล่นทับ
        /// ตัวที่เก่าที่สุดแทน เพราะ Pool มีขนาดจำกัดตาม sfxPoolSize
        /// </summary>
        private AudioSource GetFreeVoice()
        {
            for (int i = 0; i < sfxPool.Length; i++)
            {
                AudioSource voice = sfxPool[i];

                if (voice != null && !voice.isPlaying)
                    return voice;
            }

            AudioSource fallback = sfxPool[sfxPoolCursor];
            sfxPoolCursor = (sfxPoolCursor + 1) % sfxPool.Length;
            return fallback;
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

            // เสียงจาก Pool ต้อง Pause เสมอ ไม่ว่าจะตั้ง sfxMixerGroup ไว้หรือไม่
            PauseSfxPool();

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

            ResumeSfxPool();

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
            StopSfxPool();

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

        private void PauseSfxPool()
        {
            if (sfxPool == null)
                return;

            for (int i = 0; i < sfxPool.Length; i++)
            {
                AudioSource voice = sfxPool[i];

                if (voice == null)
                    continue;

                if (voice.isPlaying)
                    voice.Pause();
            }
        }

        private void ResumeSfxPool()
        {
            if (sfxPool == null)
                return;

            for (int i = 0; i < sfxPool.Length; i++)
            {
                AudioSource voice = sfxPool[i];

                if (voice == null)
                    continue;

                voice.UnPause();
            }
        }

        private void StopSfxPool()
        {
            if (sfxPool == null)
                return;

            for (int i = 0; i < sfxPool.Length; i++)
            {
                AudioSource voice = sfxPool[i];

                if (voice == null)
                    continue;

                voice.Stop();
            }
        }
    }
}