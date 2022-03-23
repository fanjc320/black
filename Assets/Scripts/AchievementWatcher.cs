﻿using System;
using System.Collections.Generic;
using Dirichlet.Numerics;
using UnityEngine;

public class AchievementWatcher : MonoBehaviour
{
    [SerializeField]
    string conditionName;

    [SerializeField]
    ToastMessageEx toastMessage;

    UInt128 currentValue;
    Dictionary<string, List<AchievementData>> achievementsDict;

    bool IsFine()
    {
        if (conditionName.Length == 0) return false;
        if (toastMessage == null) return false;

        if (Data.dataSet == null) return false;

        if (BlackContext.instance == null) return false;
        if (BlackContext.instance.AchievementGathered == null) return false;
        if (BlackContext.instance.AchievementRedeemed == null) return false;

        return true;
    }

    void UpdateAchievementProgress()
    {
        if (!IsFine()) return;
        if (!achievementsDict.ContainsKey(conditionName)) return;

        var result = achievementsDict[conditionName].GetAvailableAchievement(
            (UInt128)BlackContext.instance.StageCombo.ToInt(), currentValue);

        Debug.Log("currentValue: " + currentValue);
        Debug.Log("StageCombo: " + BlackContext.instance.StageCombo);
        Debug.Log("result: " + result);
        if (result == null) return;

        Debug.Log("result.Item1: " + result.Item1);
        Debug.Log("result.Item2: " + result.Item2);

        currentValue = (UInt128)BlackContext.instance.StageCombo.ToInt();

        var name = result.Item1.name;
        var title = name.Localized(result.Item1.conditionOldArg.ToLong().Postfixed(),
            result.Item1.conditionNewArg.ToLong().Postfixed());

        var desc = result.Item1.desc;
        var descMsg = desc.Localized(result.Item1.conditionOldArg.ToLong().Postfixed(),
            result.Item1.conditionNewArg.ToLong().Postfixed());
        toastMessage.PlayGoodAnim(title);
    }

    private void Start()
    {
        switch (conditionName)
        {
            case "MaxBlackLevel":
                currentValue = BlackContext.instance.AchievementGathered.MaxBlackLevel;
                break;
            case "MaxColoringCombo":
                currentValue = BlackContext.instance.AchievementGathered.MaxColoringCombo;
                break;
            default:
                break;
        }

        achievementsDict = new Dictionary<string, List<AchievementData>>
        {
            {
                "MaxBlackLevel",
                Data.dataSet.AchievementData_MaxBlackLevel
            },
            {
                "MaxColoringCombo",
                Data.dataSet.AchievementData_MaxColoringCombo
            }
        };

        Debug.Log("CurrentValue: " + currentValue);
    }

    private void Update()
    {
        UpdateAchievementProgress();
    }

}
