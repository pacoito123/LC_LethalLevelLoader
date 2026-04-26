using System.Collections.Generic;
using UnityEngine;

namespace LethalLevelLoader;

[CreateAssetMenu(fileName = "ExtendedFootstepSurface", menuName = "Lethal Level Loader/Extended Content/ExtendedFootstepSurface", order = 27)]
public class ExtendedFootstepSurface : ExtendedContent
{
    public FootstepSurface footstepSurface;
    public List<Material> associatedMaterials;

    internal override (bool result, string log) TryValidateContent()
    {
        if (footstepSurface == null)
            return ((false, "FootstepSurface Was Null"));
        if (associatedMaterials == null)
            return ((false, "Associated Materials List Was Null"));
        if (associatedMaterials.Count == 0)
            return ((false, "Associated Materials List Was Empty"));
        if (footstepSurface.clips == null)
            return ((false, "FootstepSurface Clips Array Was Null"));
        if (footstepSurface.clips.Length == 0)
            return ((false, "FootstepSurface Clips Array Was Empty"));
        else
            return (base.TryValidateContent());
    }
}
