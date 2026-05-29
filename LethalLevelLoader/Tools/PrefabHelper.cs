using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace LethalLevelLoader
{
    public static class PrefabHelper
    {
        internal static Lazy<GameObject> _prefabParent;
        internal static GameObject prefabParent => _prefabParent.Value;

        static PrefabHelper()
        {
            _prefabParent = new Lazy<GameObject>(static () =>
            {
                GameObject parent = new GameObject("LethalLibGeneratedPrefabs")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                parent.SetActive(false);

                return parent;
            });
        }

        public static GameObject CreatePrefab(string name)
        {
            GameObject prefab = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            prefab.transform.SetParent(prefabParent.transform);

            return prefab;
        }

        public static GameObject CreateNetworkPrefab(string name)
        {
            GameObject prefab = CreatePrefab(name);
            prefab.AddComponent<NetworkObject>();

            byte[] hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(Assembly.GetCallingAssembly().GetName().Name + name));
            prefab.GetComponent<NetworkObject>().GlobalObjectIdHash = BitConverter.ToUInt32(hash, 0);

            //LethalLevelLoaderNetworkManager.RegisterNetworkPrefab(prefab);
            return prefab;
        }
    }
}
