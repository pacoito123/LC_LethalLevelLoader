using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;

namespace LethalLevelLoader.NetworkStructs
{
    public struct NetworkItemReference : INetworkSerializable
    {
        private static List<NetworkPrefab> Prefabs => LethalLevelLoaderNetworkManager.networkManager.NetworkConfig.Prefabs.m_Prefabs;

        private uint m_NetworkItemObjectId;
        private const uint s_NullId = uint.MaxValue;

        public uint NetworkItemObjectId
        {
            readonly get => m_NetworkItemObjectId;
            internal set => m_NetworkItemObjectId = value;
        }

        public NetworkItemReference(Item item)
        {
            if (item == null)
            {
                m_NetworkItemObjectId = s_NullId;
                return;
            }

            if (item.spawnPrefab == null || item.spawnPrefab.GetComponent<GrabbableObject>() == false)
            {
                throw new ArgumentException(item.name + "'s Prefab or Prefab GrabbableObject is Missing!");
            }

            m_NetworkItemObjectId = GetIdHashFromItem(item);
        }

        public readonly bool TryGet(out Item item, NetworkManager _ = null)
        {
            item = Resolve(this);
            return (item != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Item Resolve(NetworkItemReference networkItemRef)
        {
            if (networkItemRef.m_NetworkItemObjectId == s_NullId)
                return null;
            return (GetItemFromNetworkPrefabIdHash(networkItemRef.m_NetworkItemObjectId));
        }

        public static implicit operator Item(NetworkItemReference networkItemRef)
        {
            return Resolve(networkItemRef);
        }

        public static implicit operator NetworkItemReference(Item item)
        {
            return new NetworkItemReference(item);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref m_NetworkItemObjectId);
        private static Item GetItemFromNetworkPrefabIdHash(uint idHash)
        {
            for (int i = 0; i < Prefabs.Count; i++)
                if (Prefabs[i].SourcePrefabGlobalObjectIdHash == idHash)
                    if (Prefabs[i].Prefab.TryGetComponent(out GrabbableObject grabbableObject))
                        return (grabbableObject.itemProperties);
            return (null);
        }

        private static uint GetIdHashFromItem(Item item)
        {
            for (int i = 0; i < Prefabs.Count; i++)
                if (Prefabs[i].Prefab == item.spawnPrefab)
                    return (Prefabs[i].SourcePrefabGlobalObjectIdHash);
            return (0);
        }
    }
}
