using System.Collections.Generic;
using UnityEngine;
using PlayerSystem;
using EnemySystem;
using BossSystem;

namespace AudioSystem
{
    /// <summary>
    /// ตัวกลางเล่นเสียงเอฟเฟกต์ (SFX) ทั้งหมดของเกม - Singleton ตัวเดียว
    ///
    /// ทำงานแบบ "ฟัง event" ล้วนๆ ไม่มี logic เกมอยู่ในนี้เลย:
    /// - เสียงยิงผู้เล่น / ตาย / ใช้ Spell -> ฟังจาก PlayerControllerV1 (หา instance เดียวใน Scene ตอน Start)
    /// - เสียง Enemy Spawn/Death -> ฟังจาก static event ของ EnemyV1 (ไม่ต้องผูกทีละตัว)
    /// - เสียงยิงกระสุนศัตรูแต่ละแบบ -> ฟังจาก static event ของ EnemyBulletV2 (คลิปมาจาก field บน Bullet Prefab เอง)
    /// - เสียง Boss เข้าฉาก/ตาย -> ฟังจาก static event ของ BossV2
    ///
    /// ไม่ใช้ AudioSource.PlayClipAtPoint (มันสร้าง/ทำลาย GameObject ชั่วคราวเองภายใน ขัดกับกฎ "ห้าม Instantiate/Destroy รันไทม์"
    /// ของโปรเจกต์นี้) - ใช้ AudioSource ที่วางไว้ล่วงหน้าใน Inspector วนใช้ซ้ำแทน (เหมือนหลักการ Pool)
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX Sources (ลาก AudioSource ที่วางไว้ล่วงหน้าเป็น Child ของตัวนี้ใน Scene มาใส่)")]
        [Tooltip("จำนวนช่องยิ่งเยอะ ยิ่งเล่นเสียงซ้อนกันได้เยอะโดยไม่ตัดเสียงเก่าทิ้ง แนะนำ 6-10 ช่องสำหรับ bullet hell")]
        [SerializeField] private List<AudioSource> sfxSources = new List<AudioSource>();

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        [Header("Player SFX")]
        [Tooltip("ไม่ได้ผูกไว้ในนี้ - เสียงยิง/ตาย/spell ผูกที่ AudioClip ด้านล่าง แล้วระบบจะฟัง event จาก PlayerControllerV1 เอง")]
        [SerializeField] private AudioClip playerShootSfx;
        [SerializeField] private AudioClip playerDeathSfx;
        [SerializeField] private AudioClip playerBombSfx;
        [SerializeField] private AudioClip playerRespawnSfx;

        [Header("Boss SFX (ใช้ตัวเดียวกันทุกบอส)")]
        [SerializeField] private AudioClip bossAppearSfx;
        [SerializeField] private AudioClip bossDefeatedSfx;
        [SerializeField] private AudioClip spellCardStartSfx;

        private int nextSourceIndex;
        private PlayerControllerV1 cachedPlayer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (sfxSources.Count == 0)
            {
                Debug.LogWarning("AudioManager: ไม่ได้ลาก AudioSource ไว้ใน sfxSources เลย - จะไม่มีเสียง SFX เล่นเลยจนกว่าจะเพิ่ม");
            }
        }

        private void OnEnable()
        {
            // static events - subscribe ครั้งเดียวจบ ไม่ต้องรู้จัก instance ของ Enemy/Bullet/Boss ทีละตัว
            EnemyV1.OnAnyEnemySpawnSfx += PlaySFX;
            EnemyV1.OnAnyEnemyDeathSfx += PlaySFX;
            EnemyBulletV2.OnAnyBulletSpawnSfx += PlaySFX;

            BossV2.OnAnyBossActivated += HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny += HandleBossDefeated;
            BossV2.OnAnySpellCardStarted += HandleSpellCardStarted;
        }

        private void OnDisable()
        {
            EnemyV1.OnAnyEnemySpawnSfx -= PlaySFX;
            EnemyV1.OnAnyEnemyDeathSfx -= PlaySFX;
            EnemyBulletV2.OnAnyBulletSpawnSfx -= PlaySFX;

            BossV2.OnAnyBossActivated -= HandleBossActivated;
            BossV2.OnAnyBossDefeatedAny -= HandleBossDefeated;
            BossV2.OnAnySpellCardStarted -= HandleSpellCardStarted;

            UnsubscribePlayer();
        }

        private void Start()
        {
            // Player ไม่ได้ Pool และมีตัวเดียวใน Scene เสมอ - หา instance ตรงๆ แล้ว subscribe ทีเดียวพอ
            // (ต่างจาก Enemy/Bullet ที่มีหลายสิบ instance หมุนเวียนจาก Pool เลยต้องใช้ static event แทน)
            cachedPlayer = FindObjectOfType<PlayerControllerV1>();

            if (cachedPlayer == null)
            {
                Debug.LogWarning("AudioManager: หา PlayerControllerV1 ใน Scene ไม่เจอ - จะไม่มีเสียงผู้เล่นเล่นเลย");
                return;
            }

            cachedPlayer.OnShoot += HandlePlayerShoot;
            cachedPlayer.OnDied += HandlePlayerDied;
            cachedPlayer.OnBombUsed += HandlePlayerBomb;
            cachedPlayer.OnRespawned += HandlePlayerRespawned;
        }

        private void UnsubscribePlayer()
        {
            if (cachedPlayer == null) return;

            cachedPlayer.OnShoot -= HandlePlayerShoot;
            cachedPlayer.OnDied -= HandlePlayerDied;
            cachedPlayer.OnBombUsed -= HandlePlayerBomb;
            cachedPlayer.OnRespawned -= HandlePlayerRespawned;
        }

        private void HandlePlayerShoot() => PlaySFX(playerShootSfx);
        private void HandlePlayerDied() => PlaySFX(playerDeathSfx);
        private void HandlePlayerBomb() => PlaySFX(playerBombSfx);
        private void HandlePlayerRespawned() => PlaySFX(playerRespawnSfx);

        private void HandleBossActivated() => PlaySFX(bossAppearSfx);
        private void HandleBossDefeated() => PlaySFX(bossDefeatedSfx);
        private void HandleSpellCardStarted(string spellCardName) => PlaySFX(spellCardStartSfx);

        /// <summary>
        /// เล่นเสียง SFX แบบ round-robin ผ่านช่อง AudioSource ที่เตรียมไว้ - ปลอดภัยเรียกด้วย clip = null (จะไม่ทำอะไรเลย)
        /// เรียกได้จากภายนอกด้วย เช่น UI ปุ่มกด หรือระบบอื่นในอนาคตที่อยากเล่นเสียงเอง
        /// </summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || sfxSources.Count == 0) return;

            AudioSource source = sfxSources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sfxSources.Count;

            source.volume = sfxVolume;
            source.pitch = 1f;
            source.PlayOneShot(clip);
        }

        /// <summary>เหมือน PlaySFX แต่ปรับ pitch เพิ่มเอง เช่นอยากสุ่ม pitch กันเสียงซ้ำจำเจตอนกระสุนยิงถี่ๆ</summary>
        public void PlaySFXWithPitch(AudioClip clip, float pitch)
        {
            if (clip == null || sfxSources.Count == 0) return;

            AudioSource source = sfxSources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sfxSources.Count;

            source.volume = sfxVolume;
            source.pitch = pitch;
            source.PlayOneShot(clip);
        }

        public void SetSfxVolume(float volume01)
        {
            sfxVolume = Mathf.Clamp01(volume01);
        }
    }
}
