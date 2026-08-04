using Unity.Netcode;
using UnityEngine.SceneManagement;
using static LethalLevelLoader.LethalLevelLoaderNetworkManager;

namespace LethalLevelLoader
{
    public struct NetworkSceneInfo : INetworkSerializable
    {
        private uint m_levelSceneIndex;
        private StringContainer m_levelScenePath;


        public readonly string LevelScenePath => m_levelScenePath.SomeText;
        public readonly int LevelSceneIndex => (int)m_levelSceneIndex;
        public readonly int SceneIndex
        {
            get
            {
                NetworkScenePatcher.TryGetSceneIndex((int)m_levelSceneIndex, LevelScenePath, out int returnIndex);
                return (returnIndex);
            }
        }

        public readonly bool IsLoaded
        {
            get
            {
                if (Origin is SceneOrigin.Build)
                    return (true);
                if (AssetBundleLoader.TryGetAssetBundleInfo(LevelScenePath, out AssetBundleInfo info))
                    return (info.IsLoaded);
                else
                    return (false);
            }
        }

        public enum SceneOrigin : byte { Build, Bundle }
        public readonly SceneOrigin Origin
        {
            get
            {
                if (SceneIndex >= SceneManager.sceneCountInBuildSettings)
                    return (SceneOrigin.Bundle);
                else
                    return (SceneOrigin.Build);
            }
        }

        public NetworkSceneInfo(int levelSceneIndex, string levelScenePath)
        {
            m_levelScenePath = new StringContainer
            {
                SomeText = levelScenePath
            };
            m_levelSceneIndex = (uint)levelSceneIndex;
        }


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_levelSceneIndex);
            serializer.SerializeNetworkSerializable(ref m_levelScenePath);
        }
    }
}
