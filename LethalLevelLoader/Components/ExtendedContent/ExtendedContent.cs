using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public class ExtendedContent : ScriptableObject
    {
        public ExtendedMod ExtendedMod { get; internal set; }
        public ContentType ContentType { get; internal set; } = ContentType.Vanilla;
        [field: SerializeField] public List<ContentTag> ContentTags { get; internal set; } = new List<ContentTag>();

        public string ModName => ExtendedMod.ModName;
        public string AuthorName => ExtendedMod.AuthorName;

        public string UniqueIdentificationName => AuthorName.ToLowerInvariant() + "." + ModName.ToLowerInvariant() + "." + name.ToLowerInvariant();

        [Obsolete] public List<string> ContentTagStrings { get; internal set; } = [];

        internal virtual void TryCreateMatchingProperties()
        {

        }

        internal virtual (bool result, string log) TryValidateContent()
        {
            int removedTags = ContentTags.RemoveAll(tag => tag == null || string.IsNullOrEmpty(tag.contentTagName));
            if (removedTags > 0)
                DebugHelper.LogWarning($"Removed '{removedTags}' missing or empty tags in ExtendedContent: {name}", DebugType.User);
            return ((true, string.Empty));
        }

        internal virtual void ConvertObsoleteValues()
        {

        }

        public bool TryGetTag(string tag)
        {
            foreach (ContentTag contentTag in ContentTags)
                if (contentTag.contentTagName == tag)
                    return (true);
            return (false);
        }

        public bool TryGetTag(string tag, out ContentTag returnTag)
        {
            returnTag = null;
            foreach (ContentTag contentTag in ContentTags)
                if (contentTag.contentTagName == tag)
                {
                    returnTag = contentTag;
                    return (true);
                }
            return (false);
        }

        public bool TryAddTag(string tag)
        {
            if (TryGetTag(tag) == false)
            {
                ContentTags.Add(ContentTag.Create(tag));
                return (true);
            }
            return (false);
        }

        internal struct ExtendedContentComparer : IComparer<ExtendedContent>
        {
            public readonly int Compare(ExtendedContent a, ExtendedContent b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public class StringWithRarity(string newName, int newRarity)
    {
        [SerializeField]
        private string _name = newName;

        [SerializeField]
        [Range(0, 300)]
        private int _rarity = newRarity;

        [HideInInspector] public string Name { get => (_name); set => _name = value; }
        [HideInInspector] public int Rarity { get => (_rarity); set => _rarity = value; }
    }

    [Serializable]
    public class Vector2WithRarity(float newMin, float newMax, int newRarity)
    {
        [SerializeField] private Vector2 _minMax = new(newMin, newMax);
        [SerializeField] private int _rarity = newRarity;

        [HideInInspector] public float Min { get => (_minMax.x); set => _minMax.x = value; }
        [HideInInspector] public float Max { get => (_minMax.y); set => _minMax.y = value; }
        [HideInInspector] public int Rarity { get => (_rarity); set => _rarity = value; }

        public Vector2WithRarity(Vector2 vector2, int newRarity) : this(vector2.x, vector2.y, newRarity) { }
    }

    [Serializable]
    public struct ClipWithRarity(AnimationClip clip, int rarity)
    {
        [SerializeField]
        private AnimationClip _clip = clip;

        [SerializeField]
        [Range(0, 300)]
        private int _rarity = rarity;

        [HideInInspector] public AnimationClip Clip { readonly get => (_clip); set => _clip = value; }
        [HideInInspector] public int Rarity { readonly get => (_rarity); set => _rarity = value; }
    }
}
