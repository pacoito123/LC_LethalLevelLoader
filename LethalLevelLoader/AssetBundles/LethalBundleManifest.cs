namespace LethalLevelLoader.AssetBundles
{
    public struct LethalBundleManifest
    {
        public static int ManifestVersion { get; } = 2;

        public string fileName;
        public string bundleName;
        public long timestamp;
        public string[] scenePaths;

        public LethalBundleManifest(string bundleManifest)
        {
            string[] bundleInfo = bundleManifest.Split(';');

            if (bundleInfo.Length > 3)
            {
                fileName = bundleInfo[0];
                bundleName = bundleInfo[1];
                timestamp = long.Parse(bundleInfo[2]);
                scenePaths = bundleInfo[3].Split(',');
            }
        }

        public LethalBundleManifest() { }

        public override readonly string ToString()
        {
            return fileName + ';' + bundleName + ';' + timestamp + ';' + string.Join(',', scenePaths);
        }
    }
}