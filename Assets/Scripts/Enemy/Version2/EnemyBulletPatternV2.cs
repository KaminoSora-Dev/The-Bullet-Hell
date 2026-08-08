using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// Main Enemy bullet-pattern component for V2.
    ///
    /// Replace EnemyShooterV1 on an Enemy with this component.
    /// Pattern data is stored directly on this GameObject, so there is no
    /// separate ScriptableObject/Runner setup required for this first V2.
    /// </summary>
    public class EnemyBulletPatternV2 : MonoBehaviour
    {
        [Header("Pattern")]
        [SerializeField]
        private List<BulletPatternGroup> patterns =
            new List<BulletPatternGroup>();

        

        [Header("Random")]
        [SerializeField]
        private bool useFixedSeed = false;

        [SerializeField]
        private int randomSeed = 12345;

        private Transform playerTransform;
        private EnemyV1 ownerEnemy;
        private BulletPatternV2Executor executor;

        private void Awake()
        {
            ownerEnemy = GetComponent<EnemyV1>();

            CachePlayer();

            executor =
                new BulletPatternV2Executor(
                    patterns,
                    transform,
                    () => playerTransform);

            ApplyRandomSeed();

            if (ownerEnemy != null)
                ownerEnemy.OnShootWindowOpened +=
                    ResetAllGroups;
        }

        private void Start()
        {
            // Some setups spawn Player after the Enemy.
            // Cache once again without doing a scene search every frame.
            CachePlayer();
        }

        private void OnDestroy()
        {
            if (ownerEnemy != null)
                ownerEnemy.OnShootWindowOpened -=
                    ResetAllGroups;
        }

        private void Update()
        {
            if (executor == null)
                return;

            if (ownerEnemy != null &&
                !ownerEnemy.CanShootNow)
                return;

            executor.Tick(Time.deltaTime);
        }

        private void CachePlayer()
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag(
                    "Player");

            playerTransform =
                playerObject != null
                    ? playerObject.transform
                    : null;
        }

        private void ApplyRandomSeed()
        {
            if (executor == null)
                return;

            if (useFixedSeed)
                executor.SetSeed(randomSeed);
            else
                executor.ResetRandom();
        }

        public void ResetAllGroups()
        {
            if (executor == null)
                return;

            executor.ResetAllGroups();
            ApplyRandomSeed();
        }

        public void SetRandomSeed(int seed)
        {
            randomSeed = seed;
            useFixedSeed = true;

            if (executor != null)
                executor.SetSeed(seed);
        }

        public int PatternGroupCount =>
            patterns != null
                ? patterns.Count
                : 0;
    }

    // Compatibility shim for the existing EnemyV1 source.
    // It is hidden from Add Component and should not be added manually.
    // New enemies should use EnemyBulletPatternV2.
    [AddComponentMenu("")]
    [Obsolete("Use EnemyBulletPatternV2 instead.")]
    public class EnemyShooterV1 :
        EnemyBulletPatternV2
    {
    }
}
