using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;

namespace LethalLevelLoader
{
    public struct NetworkEnemyTypeReference : INetworkSerializable
    {
        private static List<NetworkPrefab> Prefabs => LethalLevelLoaderNetworkManager.networkManager.NetworkConfig.Prefabs.m_Prefabs;

        private uint m_NetworkEnemyTypeObjectId;
        private const uint s_NullId = uint.MaxValue;

        public uint NetworkEnemyTypeObjectId
        {
            readonly get => m_NetworkEnemyTypeObjectId;
            internal set => m_NetworkEnemyTypeObjectId = value;
        }

        public NetworkEnemyTypeReference(EnemyType enemy)
        {
            if (enemy == null)
            {
                m_NetworkEnemyTypeObjectId = s_NullId;
                return;
            }

            if (enemy.enemyPrefab == null || enemy.enemyPrefab.GetComponent<EnemyAI>() == false)
            {
                throw new ArgumentException(enemy.name + "'s Prefab or Prefab GrabbableObject is Missing!");
            }

            m_NetworkEnemyTypeObjectId = GetIdHashFromEnemyType(enemy);
        }

        public readonly bool TryGet(out EnemyType enemy, NetworkManager _ = null)
        {
            enemy = Resolve(this);
            return (enemy != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static EnemyType Resolve(NetworkEnemyTypeReference networkEnemy)
        {
            if (networkEnemy.m_NetworkEnemyTypeObjectId == s_NullId)
                return null;
            return (GetEnemyTypeFromNetworkPrefabIdHash(networkEnemy.m_NetworkEnemyTypeObjectId));
        }

        public static implicit operator EnemyType(NetworkEnemyTypeReference networkEnemyRef)
        {
            return Resolve(networkEnemyRef);
        }

        public static implicit operator NetworkEnemyTypeReference(EnemyType enemy)
        {
            return new NetworkEnemyTypeReference(enemy);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref m_NetworkEnemyTypeObjectId);

        private static EnemyType GetEnemyTypeFromNetworkPrefabIdHash(uint idHash)
        {
            for (int i = 0; i < Prefabs.Count; i++)
                if (Prefabs[i].SourcePrefabGlobalObjectIdHash == idHash)
                    if (Prefabs[i].Prefab.TryGetComponent(out EnemyAI enemyAI))
                        return (enemyAI.enemyType);
            return (null);
        }

        private static uint GetIdHashFromEnemyType(EnemyType enemy)
        {
            for (int i = 0; i < Prefabs.Count; i++)
                if (Prefabs[i].Prefab == enemy.enemyPrefab)
                    return (Prefabs[i].SourcePrefabGlobalObjectIdHash);
            return (0);
        }
    }
}
