using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// Definition-only container for one reusable bullet pattern.
    /// This component does NOT run Update/FixedUpdate and does not own runtime state.
    ///
    /// Attach it to a GameObject, name that GameObject clearly, and let
    /// Boss/Enemy/Burst Trigger reference this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Enemy System/Bullet Custom Pattern")]
    public class BulletCustomPattern : MonoBehaviour
    {
        [Header("Pattern Identity")]
        [Tooltip(
            "ชื่อ Pattern สำหรับอ่านใน Inspector/Log\n" +
            "สามารถตั้งชื่อ GameObject ให้ตรงกันเพื่อหา Pattern ได้ง่าย"
        )]
        [SerializeField] private string patternName = "New Pattern";

        [Header("Pattern")]
        [Tooltip(
            "ทุก Group/Burst/Timeline ของ Pattern นี้อยู่ที่นี่\n" +
            "ไม่ต้องสร้าง GameObject แยกต่อ Burst"
        )]
        [SerializeField]
        private List<BulletPatternGroup> groups =
            new List<BulletPatternGroup>();

        public string PatternName => patternName;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(patternName))
                    return patternName;

                return gameObject != null
                    ? gameObject.name
                    : "Unnamed Pattern";
            }
        }

        public IReadOnlyList<BulletPatternGroup> Groups =>
            groups;

        public List<BulletPatternGroup> CloneGroups()
        {
            var clones =
                new List<BulletPatternGroup>();

            if (groups == null)
                return clones;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null)
                    clones.Add(groups[i].Clone());
            }

            return clones;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(patternName) &&
                gameObject != null)
            {
                patternName = gameObject.name;
            }
        }
#endif
    }
}
