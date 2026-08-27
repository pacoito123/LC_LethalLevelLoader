using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    public struct AssetBundleInfo
    {
        public string DirectoryPath { get; private set; }
        private readonly List<string> scenePathsInBundle = new List<string>();
        private readonly List<string> sceneNamesInBundle = new List<string>();
        private readonly AssetBundle assetBundle;

        public readonly bool IsLoaded => (assetBundle != null);
        public readonly bool IsSceneBundle => (sceneNamesInBundle.Count > 0);

        //We are taking in bundle for now but this might change once better
        public AssetBundleInfo(string directory, AssetBundle newBundle)
        {
            DirectoryPath = directory;
            assetBundle = newBundle;
            if (assetBundle != null && assetBundle.isStreamedSceneAssetBundle)
                foreach (string scene in assetBundle.GetAllScenePaths())
                    scenePathsInBundle.Add(scene);

            foreach (string scene in scenePathsInBundle)
                sceneNamesInBundle.Add(scene[(scene.LastIndexOf('/') + 1)..].Replace(".unity", string.Empty, StringComparison.OrdinalIgnoreCase));
            foreach (string scene in sceneNamesInBundle)
                DebugHelper.Log("AssetBundleInfo Has Scene: " + scene, DebugType.User);
        }

        public readonly AssetBundle LoadAndOrGetBundle()
        {
            if (IsLoaded)
                return (assetBundle);
            //else
            //load bundle stuffs

            return (null);
        }

        public readonly bool ContainsScene(string scenePath) => (sceneNamesInBundle.Contains(scenePath));
    }
}
