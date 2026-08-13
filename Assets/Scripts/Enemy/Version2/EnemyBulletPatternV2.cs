using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// Main Enemy bullet-pattern component for V2.
    ///
    /// IMPORTANT:
    /// This component is the owner/runner of the serialized pattern list.
    /// Nested Burst Trigger execution belongs to BulletPatternV2Executor,
    /// so this file deliberately keeps the serialized field layout unchanged.
    /// Existing Inspector/prefab data therefore remains compatible.
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

            executor = new BulletPatternV2Executor(
                patterns,
                transform,
                () => playerTransform);

            ApplyRandomSeed();

            if (ownerEnemy != null)
                ownerEnemy.OnShootWindowOpened += ResetAllGroups;
        }

        private void Start()
        {
            // Player may be spawned after this Enemy.
            // Cache once again instead of searching every frame.
            CachePlayer();
        }

        private void OnDestroy()
        {
            if (ownerEnemy != null)
                ownerEnemy.OnShootWindowOpened -= ResetAllGroups;
        }

        private void Update()
        {
            if (executor == null)
                return;

            if (ownerEnemy != null && !ownerEnemy.CanShootNow)
                return;

            executor.Tick(Time.deltaTime);
        }

        private void CachePlayer()
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

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

    // Compatibility shim for existing EnemyV1 source.
    // Hidden from Add Component; new enemies should use EnemyBulletPatternV2.
    [AddComponentMenu("")]
    [Obsolete("Use EnemyBulletPatternV2 instead.")]
    public class EnemyShooterV1 : EnemyBulletPatternV2
    {
    }
}