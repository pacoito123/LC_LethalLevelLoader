using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ContentTag", menuName = "Lethal Level Loader/Utility/ContentTag", order = 11)]
    public class ContentTag : ScriptableObject
    {
        public string contentTagName = string.Empty;
        public Color contentTagColor = Color.white;

        public static ContentTag Create(string tag, Color color)
        {
            ContentTag contentTag = CreateInstance<ContentTag>();
            contentTag.contentTagName = tag;
            contentTag.contentTagColor = color;
            contentTag.name = tag;
            return (contentTag);
        }

        public static ContentTag Create(string tag)
        {
            return (Create(tag, Color.white));
        }
    }
}
