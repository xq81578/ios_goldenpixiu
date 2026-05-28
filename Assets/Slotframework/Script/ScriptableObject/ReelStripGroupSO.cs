using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ReelStripGroupData", menuName = "ScriptableObjects/ReelStripGroupData", order = 1)]
public class ReelStripGroupSO : ScriptableObject
{
    public int MaxCombIndex;
    public List<ReelStripGroup> ReelStripGroups = new();
    public List<ReelStripCombination> ReelStripCombinations = new();

    public ReelStripGroup GetReelStripGroup(int index)
    {
        if (index < 0 || index >= ReelStripGroups.Count)
        {
            return null;
        }

        return ReelStripGroups[index];
    }

    public ReelStrip GetReelStrip(int combIndex, int reelIndex)
    {
        if (combIndex < 0 || combIndex >= ReelStripCombinations.Count)
        {
            return null;
        }

        var reelStripCombination = ReelStripCombinations[combIndex];
        if (reelIndex < 0 || reelIndex >= reelStripCombination.ReelStripGroup.Count)
        {
            return null;
        }

        return ReelStripGroups[reelStripCombination.ReelStripGroup[reelIndex]].ReelStrips[reelIndex];
    }
}

[Serializable]
public class ReelStripGroup
{
    public List<ReelStrip> ReelStrips = new();
}

[Serializable]
public class ReelStrip
{
    public List<string> Symbols = new();
}

[Serializable]
public class ReelStripCombination
{
    public List<int> ReelStripGroup = new();
}