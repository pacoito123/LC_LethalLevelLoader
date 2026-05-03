using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader
{
    [CreateAssetMenu(fileName = "ExtendedFootstepSurface", menuName = "Lethal Level Loader/Extended Content/ExtendedFootstepSurface", order = 27)]
    public class ExtendedFootstepSurface : ExtendedContent
    {
        [field: Header("General Settings")]
        [field: SerializeField] public VanillaSurfaceTags UseVanillaTag { get; set; } = VanillaSurfaceTags.None;
        [field: SerializeField] public FootstepSurface FootstepSurface { get; set; }

        [field: Header("Extended Feature Settings")]
        [field: SerializeField] public List<TerrainWithIndices> AssociatedTerrains { get; set; } = [];
        [field: SerializeField] public bool AllowSinking { get; set; } = true;
        [field: SerializeField] public bool AllowEarthLeviathanEmerge { get; set; } = true;

        [HideInInspector] public Dictionary<string, byte> AssociatedTerrainNames { get; } = [];
        [HideInInspector] public int SurfaceIndex { get; internal set; } = -1;

        [Space(25)]
        [Header("Obsolete (Legacy Fields, Will Be Removed In The Future)")]
        [Obsolete] public FootstepSurface footstepSurface;
        [Obsolete] public List<Material> associatedMaterials;

        internal static ExtendedFootstepSurface Create(FootstepSurface newFootstepSurface, VanillaSurfaceTags useVanillaTag, bool allowSinking, params TerrainWithIndices[] newAssociatedTerrains)
        {
            ExtendedFootstepSurface newExtendedFootstepSurface = ScriptableObject.CreateInstance<ExtendedFootstepSurface>();
            newExtendedFootstepSurface.FootstepSurface = newFootstepSurface;

            if (useVanillaTag is not VanillaSurfaceTags.None)
                newExtendedFootstepSurface.UseVanillaTag = useVanillaTag;
            else
                newExtendedFootstepSurface.ContentType = ContentType.External;

            newExtendedFootstepSurface.AllowSinking = allowSinking;
            if (newAssociatedTerrains != null)
                newExtendedFootstepSurface.AssociatedTerrains = [.. newAssociatedTerrains];
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

        internal void RefreshAssociatedTerrainNames()
        {
            AssociatedTerrainNames.Clear();
            foreach (TerrainWithIndices associatedTerrain in AssociatedTerrains)
                if (!AssociatedTerrainNames.TryAdd(associatedTerrain.terrainName, associatedTerrain.GetIndexMask())) // TODO: Handle duplicate names better...
                    DebugHelper.LogWarning($"Terrain name {associatedTerrain.terrainName} registered more than once in FootstepSurface: {name}", DebugType.Developer);
        }

        internal bool IsTerrainMatch(TerrainData terrainData) => AssociatedTerrainNames.ContainsKey(terrainData.name);
        internal bool IsTerrainMatch(TerrainData terrainData, int terrainLayer) => AssociatedTerrainNames.TryGetValue(terrainData.name, out byte terrainMask) && ((byte)(1 << terrainLayer) & terrainMask) != 0;

        internal override (bool result, string log) TryValidateContent()
        {
            if (FootstepSurface == null)
                return ((false, "FootstepSurface Was Null"));

            if (string.IsNullOrEmpty(FootstepSurface.surfaceTag))
                FootstepSurface.surfaceTag = "Untagged";

            if (UseVanillaTag is VanillaSurfaceTags.None)
            {
                if (FootstepSurface.clips == null || FootstepSurface.clips.Length == 0)
                    return ((false, "FootstepSurface Clips Were Null Or Empty"));
                for (int i = 0; i < FootstepSurface.clips.Length; i++)
                    if (FootstepSurface.clips[i] == null)
                        return ((false, "A FootstepSurface Clip Was Null Or Missing"));
                if (FootstepSurface.hitSurfaceSFX == null)
                    return ((false, "FootstepSurface Hit SFX Was Null Or Missing"));
                for (int i = 0; i < FootstepSurface.jumpLandSFX?.Length; i++)
                    if (FootstepSurface.jumpLandSFX[i] == null)
                        return ((false, "A FootstepSurface Landing SFX Clip Was Null Or Missing"));
            }

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
    public enum VanillaSurfaceTags { None = -1, Concrete, Gravel, Catwalk, Aluminum, Grass, Rock, Puddle, Tiles, Snow, Carpet, Untagged, Wood, Slime, Tree }
}