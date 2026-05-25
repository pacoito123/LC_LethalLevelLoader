using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class ContentTagManager
    {
        internal static readonly Dictionary<string, List<ContentTag>> globalContentTagDictionary = new Dictionary<string, List<ContentTag>>();
        internal static readonly Dictionary<string, List<ExtendedContent>> globalContentTagExtendedContentDictionary = new Dictionary<string, List<ExtendedContent>>();

        internal static readonly Dictionary<string, ContentTag> createdTags = [];

        internal static void PopulateContentTagData()
        {
            globalContentTagDictionary.Clear();
            globalContentTagExtendedContentDictionary.Clear();

            ExtendedMod[] allExtendedMods = [PatchedContent.VanillaMod, .. PatchedContent.ExtendedMods];
            foreach (ExtendedMod extendedMod in allExtendedMods)
                foreach (ExtendedContent extendedContent in extendedMod.ExtendedContents)
                    foreach (ContentTag contentTag in extendedContent.ContentTags)
                    {
                        if (!globalContentTagDictionary.TryGetValue(contentTag.contentTagName, out List<ContentTag> contentTagsList))
                            globalContentTagDictionary.Add(contentTag.contentTagName, [contentTag]);
                        else if (!contentTagsList.Contains(contentTag))
                            contentTagsList.Add(contentTag);

                        if (!globalContentTagExtendedContentDictionary.TryGetValue(contentTag.contentTagName, out List<ExtendedContent> extendedContentList))
                            globalContentTagExtendedContentDictionary.Add(contentTag.contentTagName, [extendedContent]);
                        else if (!extendedContentList.Contains(extendedContent))
                            extendedContentList.Add(extendedContent);
                    }

            if (Settings.debugType <= DebugType.Developer) return;
            string debugString = "Global Tag Dictionary Report:";
            foreach (KeyValuePair<string, List<ContentTag>> globalContentTagPair in globalContentTagDictionary)
                debugString += $"\n\t- Tag: {globalContentTagPair.Key}, Found Matching ContentTags: {globalContentTagPair.Value.Count}";
            DebugHelper.Log(debugString, DebugType.Developer);
        }

        internal static List<ContentTag> CreateNewContentTags(List<string> tags)
        {
            HashSet<ContentTag> returnList = new HashSet<ContentTag>();
            foreach (string tag in tags)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                if (!createdTags.TryGetValue(tag, out ContentTag createdTag))
                {
                    createdTag = ContentTag.Create(tag);
                    createdTags.Add(tag, createdTag);
                }
                returnList.Add(createdTag);
            }
            return ([.. returnList]);
        }

        public static List<ExtendedContent> GetAllExtendedContentsByTag(string tag)
        {
            return globalContentTagExtendedContentDictionary.TryGetValue(tag, out List<ExtendedContent> extendedContents) ? extendedContents : [];
        }

        public static bool TryGetContentTagColour(ExtendedContent extendedContent, string tag, out Color color)
        {
            color = Color.white;
            foreach (ContentTag contentTag in extendedContent.ContentTags)
                if (string.Equals(contentTag.contentTagName, tag, StringComparison.Ordinal))
                {
                    color = contentTag.contentTagColor;
                    return (true);
                }
            return (false);
        }

        internal static void MergeAllExtendedModTags()
        {
            Dictionary<string, ContentTag> uniqueTags = new Dictionary<string, ContentTag>();
            HashSet<ContentTag> duplicateTags = new HashSet<ContentTag>();
            foreach (ExtendedMod extendedMod in PatchedContent.ExtendedMods)
            {
                foreach (ExtendedContent extendedContent in extendedMod.ExtendedContents)
                    for (int i = 1; i < extendedContent.ContentTags.Count; i++) // First tag can be assumed to always be correct, thus skippable.
                    {
                        ContentTag contentTag = extendedContent.ContentTags[i];
                        string tagName = contentTag.contentTagName.ToLowerInvariant();
                        if (uniqueTags.TryGetValue(tagName, out ContentTag uniqueTag) && contentTag != uniqueTag)
                        {
                            duplicateTags.Add(contentTag);
                            extendedContent.ContentTags[i] = uniqueTag;
                            continue;
                        }
                        uniqueTags[tagName] = contentTag;
                    }
                uniqueTags.Clear();
            }
            foreach (ContentTag duplicateTag in duplicateTags)
            {
                if (duplicateTag == ExtendedMod.VanillaContentTag || duplicateTag == ExtendedMod.CustomContentTag) continue;
                UnityEngine.Object.Destroy(duplicateTag);
            }
        }
    }
}
