namespace LethalLevelLoader.AssetBundles
{
    public struct LethalBundleManifest
    {
        public static int ManifestVersion { get; } = 1;

        public string bundleName;
        public long timestamp;
        public string[] sceneNames, scenePaths;

        public LethalBundleManifest(string bundleManifest)
        {
            string[] bundleInfo = bundleManifest.Split(';');

            if (bundleInfo.Length > 3)
            {
                bundleName = bundleInfo[0];
                timestamp = long.Parse(bundleInfo[1]);
                sceneNames = bundleInfo[2].Split(',');
                scenePaths = bundleInfo[3].Split(',');
            }
        }

        public LethalBundleManifest() { }

        public override readonly string ToString()
        {
            return bundleName + ';' + timestamp + ';' + string.Join(',', sceneNames) + ';' + string.Join(',', scenePaths);
        }
    }
}