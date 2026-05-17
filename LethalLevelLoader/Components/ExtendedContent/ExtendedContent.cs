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
    }

    [Serializable]
    public class StringWithRarity
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        [Range(0, 300)]
        private int _rarity;

        [HideInInspector] public string Name { get { return (_name); } set { _name = value; } }
        [HideInInspector] public int Rarity { get { return (_rarity); } set { _rarity = value; } }
        [HideInInspector] public StringWithRarity(string newName, int newRarity) { _name = newName; _rarity = newRarity; }
    }

    [Serializable]
    public class Vector2WithRarity
    {
        [SerializeField] private Vector2 _minMax;
        [SerializeField] private int _rarity;

        [HideInInspector] public float Min { get { return (_minMax.x); } set { _minMax.x = value; } }
        [HideInInspector] public float Max { get { return (_minMax.y); } set { _minMax.y = value; } }
        [HideInInspector] public int Rarity { get { return (_rarity); } set { _rarity = value; } }

        public Vector2WithRarity(Vector2 vector2, int newRarity)
        {
            _minMax.x = vector2.x;
            _minMax.y = vector2.y;
            _rarity = newRarity;
        }

        public Vector2WithRarity(float newMin, float newMax, int newRarity)
        {
            _minMax.x = newMin;
            _minMax.y = newMax;
            _rarity = newRarity;
        }
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
