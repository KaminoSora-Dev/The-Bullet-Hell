using System;
using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// Enemy bullet-pattern runner.
    /// Enemy owns only one BulletCustomPattern schedule.
    /// Pattern content itself lives entirely in BulletCustomPattern.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyBulletPatternV2 : MonoBehaviour
    {
        [Header("Bullet Pattern Timing")]
        [SerializeField]
        private BulletPatternSchedule patternSchedule =
            new BulletPatternSchedule();

        [Header("Random")]
        [SerializeField]
        private bool useFixedSeed = false;

        [SerializeField]
        private int randomSeed = 12345;

        private Transform playerTransform;
        private EnemyV1 ownerEnemy;
        private BulletPatternV2Executor executor;
        private float patternDelayTimer;

        public BulletCustomPattern CurrentPattern =>
            patternSchedule != null ? patternSchedule.pattern : null;

        private void Awake()
        {
            ownerEnemy = GetComponent<EnemyV1>();
            CachePlayer();
            BuildExecutor();

            if (ownerEnemy != null)
                ownerEnemy.OnShootWindowOpened += ResetAllGroups;
        }

        private void Start()
        {
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

            if (patternDelayTimer > 0f)
            {
                patternDelayTimer -= Time.deltaTime;

                if (patternDelayTimer > 0f)
                    return;
            }

            executor.Tick(Time.deltaTime);
        }

        private void BuildExecutor()
        {
            executor = null;
            patternDelayTimer = 0f;

            if (patternSchedule == null ||
                patternSchedule.pattern == null)
            {
                return;
            }

            List<BulletPatternGroup> groups =
                patternSchedule.pattern.CloneGroups();

            ApplyLoopPolicy(groups, patternSchedule.loop);

            if (groups.Count == 0)
                return;

            var patternCallChain =
                new HashSet<BulletCustomPattern>
                {
                    patternSchedule.pattern
                };

            executor = new BulletPatternV2Executor(
                groups,
                transform,
                () => playerTransform,
                patternCallChain);

            ApplyRandomSeed();

            patternDelayTimer =
                Mathf.Max(0f, patternSchedule.startDelay);
        }

        private static void ApplyLoopPolicy(
            List<BulletPatternGroup> groups,
            bool allowLoop)
        {
            if (allowLoop || groups == null)
                return;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null)
                    groups[i].loopPattern = false;
            }
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
            BuildExecutor();
        }

        public void SetRandomSeed(int seed)
        {
            randomSeed = seed;
            useFixedSeed = true;

            if (executor != null)
                executor.SetSeed(seed);
        }

        public int PatternGroupCount =>
            patternSchedule != null &&
            patternSchedule.pattern != null
                ? patternSchedule.pattern.Groups.Count
                : 0;
    }

    [AddComponentMenu("")]
    [Obsolete("Use EnemyBulletPatternV2 instead.")]
    public class EnemyShooterV1 : EnemyBulletPatternV2
    {
    }
}
