using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ExtendedFootstepSurface", menuName = "Lethal Level Loader/Extended Content/ExtendedFootstepSurface", order = 27)]
    public class ExtendedFootstepSurface : ExtendedContent
    {
        [field: Header("General Settings")]
        [field: SerializeField] public FootstepSurface FootstepSurface { get; set; }

        [field: Header("Extended Feature Settings")]
        [field: Tooltip("Allow Quicksand to affect players while on this ExtendedFootstepSurface.")]
        [field: SerializeField] public bool AllowSinking { get; set; } = true;
        [field: Tooltip("Allow Earth Leviathans to emerge out of this ExtendedFootstepSurface.")]
        [field: SerializeField] public bool AllowEarthLeviathanEmerge { get; set; } = true;
        [field: Tooltip("Allow Masked to use this ExtendedFootstepSurface, otherwise GameObject tag is used.")]
        [field: SerializeField] public bool AllowMaskedFootsteps { get; set; } = true;

        [HideInInspector] public Dictionary<string, byte> AssociatedTerrainNames { get; } = [];
        [HideInInspector] public int SurfaceIndex { get; internal set; } = -1;

        [Space(25)]
        [Header("Obsolete (Legacy Fields, Will Be Removed In The Future)")]
        [Obsolete] public FootstepSurface footstepSurface;
        [Obsolete] public List<Material> associatedMaterials;

        internal static ExtendedFootstepSurface Create(FootstepSurface newFootstepSurface, bool allowSinking, params TerrainWithIndices[] newAssociatedTerrains)
        {
            ExtendedFootstepSurface newExtendedFootstepSurface = CreateInstance<ExtendedFootstepSurface>();
            newExtendedFootstepSurface.FootstepSurface = newFootstepSurface;
            newExtendedFootstepSurface.AllowSinking = allowSinking;
            for (int i = 0; i < newAssociatedTerrains?.Length; i++)
            {
                TerrainWithIndices terrainWithIndices = newAssociatedTerrains[i];
                if (!string.IsNullOrEmpty(terrainWithIndices.terrainName) && terrainWithIndices.layerIndices?.Length > 0)
                    newExtendedFootstepSurface.AssociatedTerrainNames[terrainWithIndices.terrainName] = terrainWithIndices.GetIndexMask();
            }
            return (newExtendedFootstepSurface);
        }

        internal void Initialize()
        {
            name = FootstepSurface.surfaceTag.Sanitized(toLower: false) + "ExtendedFootstepSurface";

            if (ContentType is ContentType.Custom)
            {
                string identifier = (ExtendedMod.ModMergeSetting) switch
                {
                    ModMergeSetting.MatchingAuthorName => ExtendedMod.AuthorName,
                    ModMergeSetting.MatchingModName => ExtendedMod.ModName,
                    _ => UniqueIdentificationName
                };

                FootstepSurface.surfaceTag = identifier + '/' + FootstepSurface.surfaceTag;
            }
        }

        internal bool IsTerrainMatch(TerrainData terrainData) => AssociatedTerrainNames.ContainsKey(terrainData.name);
        internal bool IsTerrainMatch(TerrainData terrainData, int terrainLayer) => AssociatedTerrainNames.TryGetValue(terrainData.name, out byte terrainMask) && ((byte)(1 << terrainLayer) & terrainMask) != 0;

        internal override (bool result, string log) TryValidateContent()
        {
            if (FootstepSurface == null)
                return ((false, "FootstepSurface Was Null"));

            if (string.IsNullOrEmpty(FootstepSurface.surfaceTag))
                FootstepSurface.surfaceTag = "Untagged";

            return (base.TryValidateContent());
        }

        internal override void ConvertObsoleteValues()
        {
            if (footstepSurface != null && (footstepSurface.clips?.Length > 0 || footstepSurface.jumpLandSFX?.Length > 0 || footstepSurface.hitSurfaceSFX != null))
            {
                DebugHelper.LogWarning("ExtendedFootstepSurface.footstepSurface is Obsolete and will be removed in following releases, Please use ExtendedFootstepSurface.FootstepSurface instead.", DebugType.Developer);
                FootstepSurface = new()
                {
                    surfaceTag = footstepSurface.surfaceTag,
                    clips = [.. footstepSurface.clips ?? []],
                    hitSurfaceSFX = footstepSurface.hitSurfaceSFX,
                    jumpLandSFX = [.. footstepSurface.jumpLandSFX ?? []],
                };
                footstepSurface = null;
            }
        }
    }

    [Serializable]
    public struct TerrainWithIndices(string terrainName, params int[] layerIndices)
    {
        public string terrainName = terrainName;
        [Range(0, 7)]
        public int[] layerIndices = layerIndices;

        public readonly byte GetIndexMask()
        {
            HashSet<int> indices = [.. layerIndices];
            int removedIndices = indices.RemoveWhere(index => index < 0 || index > 7);
            if (removedIndices > 0)
                DebugHelper.LogWarning($"Removed {removedIndices} indices for Terrain: {terrainName}", DebugType.Developer);

            byte indexMask = 0;
            foreach (int index in indices)
                indexMask += (byte)(1 << index);

            return indexMask;
        }
    }

    // All Vanilla FootstepSurface tags.
    public enum VanillaSurfaceTags : sbyte { None = -1, Concrete, Gravel, Catwalk, Aluminum, Grass, Rock, Puddle, Tiles, Snow, Carpet, Untagged, Wood, Slime, Tree }
}