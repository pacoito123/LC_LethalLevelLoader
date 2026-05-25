using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;

namespace LethalLevelLoader
{
    public struct NetworkExtendedLevelReference : INetworkSerializable
    {
        private static List<ExtendedLevel> Levels => PatchedContent.ExtendedLevels;

        private uint m_ExtendedLevelId;
        private const uint s_NullId = uint.MaxValue;

        public uint ExtendedLevelId
        {
            readonly get => m_ExtendedLevelId;
            internal set => m_ExtendedLevelId = value;
        }

        public NetworkExtendedLevelReference(ExtendedLevel level)
        {
            if (level == null)
            {
                m_ExtendedLevelId = s_NullId;
                return;
            }

            if (level.SelectableLevel == null)
            {
                throw new ArgumentException(level.name + "'s SelectableLevel is Missing!");
            }

            m_ExtendedLevelId = GetIndexIDFromExtendedLevel(level);
        }

        public readonly bool TryGet(out ExtendedLevel level, NetworkManager _ = null)
        {
            level = Resolve(this);
            return (level != null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ExtendedLevel Resolve(NetworkExtendedLevelReference level)
        {
            if (level.m_ExtendedLevelId == s_NullId)
                return null;
            return (GetExtendedLevelFromIndexID(level.ExtendedLevelId));
        }

        public static implicit operator ExtendedLevel(NetworkExtendedLevelReference levelRef)
        {
            return Resolve(levelRef);
        }

        public static implicit operator NetworkExtendedLevelReference(ExtendedLevel level)
        {
            return new NetworkExtendedLevelReference(level);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref m_ExtendedLevelId);

        private static ExtendedLevel GetExtendedLevelFromIndexID(uint indexID)
        {
            for (int i = 0; i < Levels.Count; i++)
                if (Levels[i].SelectableLevel.levelID == indexID)
                    return (Levels[i]);
            return (null);
        }

        private static uint GetIndexIDFromExtendedLevel(ExtendedLevel level) => (uint)level.SelectableLevel.levelID;
    }
}
