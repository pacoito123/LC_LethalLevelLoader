using System;
using System.Collections.Generic;

namespace LethalLevelLoader
{
    public class MoonsCataloguePage(List<ExtendedLevelGroup> newExtendedLevelGroups)
    {
        public List<ExtendedLevelGroup> ExtendedLevelGroups { get; } = [.. newExtendedLevelGroups];
        public List<ExtendedLevel> ExtendedLevels
        {
            get
            {
                if (field == null || field.Count == 0)
                {
                    field = new List<ExtendedLevel>();
                    foreach (ExtendedLevelGroup group in ExtendedLevelGroups)
                        field.AddRange(group.extendedLevelsList);
                }
                return (field);
            }
        }

        public void RebuildLevelGroups(List<ExtendedLevelGroup> newExtendedLevelGroups, int splitCount)
        {
            List<ExtendedLevel> convertedList = new List<ExtendedLevel>();
            foreach (ExtendedLevelGroup extendedLevelGroup in newExtendedLevelGroups)
                convertedList.AddRange(extendedLevelGroup.extendedLevelsList);
            RebuildLevelGroups(convertedList.ToArray(), splitCount);
        }

        public void RebuildLevelGroups(List<ExtendedLevel> newExtendedLevels, int splitCount)
        {
            RebuildLevelGroups(newExtendedLevels.ToArray(), splitCount);
        }

        public void RebuildLevelGroups(ExtendedLevel[] newExtendedLevels, int splitCount)
        {
            ExtendedLevelGroups.Clear();
            ExtendedLevelGroups.AddRange(TerminalManager.GetExtendedLevelGroups(newExtendedLevels, splitCount));
        }
    }

    [Serializable]
    public class ExtendedLevelGroup(List<ExtendedLevel> newExtendedLevels)
    {
        public List<ExtendedLevel> extendedLevelsList = [.. newExtendedLevels];

        public int AverageCalculatedDifficulty
        {
            get
            {
                if (field == -1)
                    field = GetAverageCalculatedDifficulty();
                return (field);
            }
        } = -1;

        public ExtendedLevelGroup(List<SelectableLevel> newSelectableLevels) : this(newSelectableLevels.ConvertAll(LevelManager.GetExtendedLevel)) { }

        public int GetAverageCalculatedDifficulty()
        {
            if (extendedLevelsList.Count == 0) return 0;
            int riskLevelSum = 0;
            foreach (ExtendedLevel level in extendedLevelsList)
                riskLevelSum += level.CalculatedDifficultyRating;
            return (riskLevelSum / extendedLevelsList.Count);
        }

        internal struct ExtendedLevelGroupDifficultyComparer : IComparer<ExtendedLevelGroup>
        {
            public readonly int Compare(ExtendedLevelGroup a, ExtendedLevelGroup b) => a.AverageCalculatedDifficulty - b.AverageCalculatedDifficulty;
        }
    }
}
