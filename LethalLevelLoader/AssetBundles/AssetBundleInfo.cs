using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LethalLevelLoader.Compatibility;
using UnityEngine;

namespace LethalLevelLoader.AssetBundles
{
    public enum AssetBundleType { Unknown, Standard, Streaming }
    public enum AssetBundleLoadingStatus { None, Loading, Unloading }

    public class AssetBundleInfo(MonoBehaviour newCoroutineHandler, string filePath, string fileName)
    {
        private bool hasInitialized;
        private AssetBundle assetBundle;
        private readonly MonoBehaviour coroutineHandler = newCoroutineHandler;
        private AssetBundleCreateRequest activeLoadRequest;
        private AssetBundleUnloadOperation activeUnloadRequest;

        private Stopwatch bundleLoadStopwatch = new Stopwatch();
        private Stopwatch bundleUnloadStopwatch = new Stopwatch();

        public string LastLoadTime => AssetBundleUtilities.GetStopWatchTime(bundleLoadStopwatch);
        public float LastTimeLoaded { get; private set; }
        public string LastUnloadTime => AssetBundleUtilities.GetStopWatchTime(bundleUnloadStopwatch);
        public float LastTimeUnloaded { get; private set; }



        private List<string> allAssetPaths = new List<string>();
        private List<string> streamingBundleScenePaths = new List<string>();
        private List<string> sceneNames = new List<string>();

        public string AssetBundleName { get; private set; } = "UNKNOWN";
        public AssetBundleType AssetBundleMode { get; private set; }
        public bool IsAssetBundleLoaded => (assetBundle != null);

        public string AssetBundleFileName { get; private set; } = fileName;
        public string AssetBundleFilePath { get; private set; } = filePath;

        public bool IsHotReloadable { get; set; }

        public float ActiveProgress
        {
            get
            {
                if (activeLoadRequest != null)
                    return (activeLoadRequest.progress);
                else if (activeUnloadRequest != null)
                    return (activeUnloadRequest.progress);
                else if (IsAssetBundleLoaded)
                    return (1f);
                else
                    return (0f);
            }
        }

        public AssetBundleLoadingStatus ActiveLoadingStatus
        {
            get
            {
                if (activeLoadRequest != null)
                    return (AssetBundleLoadingStatus.Loading);
                else if (activeUnloadRequest != null)
                    return (AssetBundleLoadingStatus.Unloading);
                return (AssetBundleLoadingStatus.None);
            }
        }

        public ExtendedEvent<AssetBundleInfo> OnBundleLoaded = new ExtendedEvent<AssetBundleInfo>();
        public ExtendedEvent<AssetBundleInfo> OnBundeUnloaded = new ExtendedEvent<AssetBundleInfo>();

        public AssetBundleInfo(MonoBehaviour newCoroutineHandler, string filePath) : this(newCoroutineHandler, filePath, "UNKNOWN")
        {
            AssetBundleFileName = filePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)[^1];
        }

        public void Initialize()
        {
            if (hasInitialized) return;
            hasInitialized = true;

            // Initialize() is called early for bundles in the known scene bundles dictionary, before they're loaded.
            if (assetBundle == null && AssetBundleLoader.knownSceneBundles.TryGetValue(AssetBundleFileName, out LethalBundleManifest bundleManifest))
            {
                IsHotReloadable = true;

                AssetBundleMode = AssetBundleType.Streaming;
                AssetBundleName = bundleManifest.bundleName;

                sceneNames = [.. Array.ConvertAll(bundleManifest.scenePaths, AssetBundleUtilities.GetSceneName)];
                streamingBundleScenePaths.AddRange(bundleManifest.scenePaths);
                allAssetPaths.AddRange(bundleManifest.scenePaths);

                return;
            }
            // ...

            AssetBundleName = assetBundle.name;
            sceneNames = AssetBundleUtilities.GetSceneNamesFromLoadedAssetBundle(assetBundle);
            if (assetBundle.isStreamedSceneAssetBundle)
            {
                AssetBundleMode = AssetBundleType.Streaming;
                streamingBundleScenePaths = [.. assetBundle.GetAllScenePaths()];
                allAssetPaths = [.. streamingBundleScenePaths];

                AssetBundleLoader.knownSceneBundles[AssetBundleFileName] = new LethalBundleManifest()
                {
                    fileName = AssetBundleFilePath[(AssetBundleFilePath.LastIndexOf(Path.DirectorySeparatorChar) + 1)..],
                    bundleName = AssetBundleName,
                    timestamp = File.GetLastWriteTime(AssetBundleFilePath).Ticks,
                    scenePaths = [.. streamingBundleScenePaths]
                };

                DebugHelper.Log("Adding " + AssetBundleFileName + " to known bundles.", DebugType.Developer);
            }
            else
            {
                AssetBundleMode = AssetBundleType.Standard;
                allAssetPaths = [.. assetBundle.GetAllAssetNames()];
            }
        }

        public bool TryLoadBundle()
        {
            if (IsAssetBundleLoaded == true)
            {
                OnBundleLoaded.Invoke(this); //Feels a little strange but if something requests a bundle to be loaded and expects this event to fire in response we wanna fire it in the event it's already loaded. (might change later) 
                return (true);
            }
            else if (IsAssetBundleLoaded == false && activeLoadRequest == null)
            {
                coroutineHandler.StartCoroutine(LoadBundleRequest());
                return (true);
            }
            else
            {
                DebugHelper.Log("Failed To Load: " + AssetBundleFileName, DebugType.User);
                if (DawnLibCompatibility.Enabled)
                    DawnLibCompatibility.RefreshLocalClientBundleState(3); // Error
                return (false);
            }
        }

        public bool TryUnloadBundle()
        {
            if (IsHotReloadable == false) return (false);
            else if (IsAssetBundleLoaded == false)
            {
                OnBundeUnloaded.Invoke(this);  //Feels a little strange but if something requests a bundle to be unloaded and expects this event to fire in response we wanna fire it in the event it's already unloaded. (might change later) 
                return (true);
            }
            else if (activeUnloadRequest == null)
            {
                coroutineHandler.StartCoroutine(UnloadBundleRequest());
                return (true);
            }
            else
            {
                DebugHelper.Log("Failed To Unload: " + AssetBundleFileName, DebugType.User);
                return (false);
            }
        }

        private IEnumerator LoadBundleRequest()
        {
            bundleLoadStopwatch = Stopwatch.StartNew();
            string combinedPath = Path.Combine(Application.streamingAssetsPath, AssetBundleFilePath);
            activeLoadRequest = AssetBundle.LoadFromFileAsync(combinedPath);
            yield return activeLoadRequest;
            if (assetBundle != null || (activeLoadRequest.isDone && activeLoadRequest.assetBundle != null))
            {
                assetBundle = activeLoadRequest.assetBundle;
                if (hasInitialized == false)
                    Initialize();
                activeLoadRequest = null;
                bundleLoadStopwatch.Stop();
                LastTimeLoaded = Time.time;
                DebugHelper.Log(AssetBundleFileName + " Loaded (" + LastLoadTime + ")!", DebugType.User);
                OnBundleLoaded.Invoke(this);
            }
            else
            {
                activeLoadRequest = null;
                bundleLoadStopwatch.Stop();
                LastTimeLoaded = Time.time;
                DebugHelper.LogError("AssetBundleInfo: " + AssetBundleFileName + " failed to load or is already loaded. Skipping...", DebugType.User);
                if (hasInitialized == false)
                    AssetBundleLoader.Instance.AssetBundleInfos.Remove(this);
            }
        }

        private static readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

        private IEnumerator UnloadBundleRequest()
        {
            bundleUnloadStopwatch = Stopwatch.StartNew();
            yield return waitForEndOfFrame; //Might remove later but stopped unity freeze when you tried to load and unload a bundle on the same frame (Confirmed Unity bug on our version)
            activeUnloadRequest = assetBundle.UnloadAsync(true);
            yield return activeUnloadRequest;
            if (activeUnloadRequest.isDone)
            {
                UnityEngine.Object.Destroy(assetBundle);
                assetBundle = null; // I think we need to do this so it isn't deemed missing (?)
                activeUnloadRequest = null;
                bundleUnloadStopwatch.Stop();
                LastTimeUnloaded = Time.time;
                DebugHelper.Log(AssetBundleFileName + " Unloaded (" + LastUnloadTime + ")", DebugType.User);
                OnBundeUnloaded.Invoke(this);
            }

        }

        ////////// AssetBundle Middle-Man'd Functions //////////

        public List<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            if (AssetBundleMode is AssetBundleType.Unknown or AssetBundleType.Streaming)
                return ([]);

            if (IsAssetBundleLoaded == false || assetBundle == null)
                return ([]);

            return ([.. assetBundle.LoadAllAssets<T>()]);
        }

        public List<string> GetSceneNames() => [.. sceneNames];

        public bool Contains(string sceneNameOrPath)
        {
            if (IsAssetBundleLoaded == false) return (false);
            if (AssetBundleMode == AssetBundleType.Standard) return (false);
            if (string.IsNullOrEmpty(sceneNameOrPath)) return (false);
            return (streamingBundleScenePaths.Contains(sceneNameOrPath) || sceneNames.Contains(sceneNameOrPath));
        }

        public bool Contains(UnityEngine.Object unityObject)
        {
            if (IsAssetBundleLoaded == false) return (false);
            if (AssetBundleMode == AssetBundleType.Streaming) return (false);
            if (unityObject == null || unityObject.name == null) return (false);
            return (assetBundle.Contains(unityObject.name));
        }
    }
}
