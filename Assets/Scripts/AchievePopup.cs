using System;
using System.Collections.Generic;
using System.Linq;
using Dirichlet.Numerics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

public class AchievePopup : MonoBehaviour
{
    public static AchievePopup Instance;

    //为确定显示顺序而创建的数组
    static readonly string [] priority =
    {
        "MaxBlackLevel",
        "MaxColoringCombo",
    };

    public RectTransform scrollViewRect;

    [SerializeField]
    Subcanvas subcanvas;

#if UNITY_EDITOR
    void OnValidate()
    {
        subcanvas = GetComponent<Subcanvas>();
    }
#endif

    [UsedImplicitly]
    void OpenPopup()
    {
        UpdateAchievementProgress();
    }

    [UsedImplicitly]
    void ClosePopup()
    {

    }

    void UpdateAchievementUI(List<Tuple<AchievementData, bool>> achievements)
    {
        if (BlackContext.Instance.AchievementNewImage)
            BlackContext.Instance.AchievementNewImage.GetComponent<Image>().enabled = achievements.Any(e => e.Item2);

        if (subcanvas.IsOpen == false) return;

        var entries = scrollViewRect.GetComponentsInChildren<AchievementEntry>(true);

        for (var idx = 0; idx < achievements.Count; idx++)
        {
            var entry = entries[idx];
            var data = achievements[idx].Item1;
            var canBeRedeemed = achievements[idx].Item2;

            UInt128 currentValue = 0;
            entry.gameObject.SetActive(true);
            // entry.image.sprite = Atlas.Load(data.sprite);
            // entry.image.color = Color.white;

            entry.achievementName.text = data.name.Localized(data.conditionOldArg.ToLong().Postfixed(),
                data.conditionNewArg.ToLong().Postfixed());
            entry.achievementDesc.text = data.desc.Localized(data.conditionOldArg.ToLong().Postfixed(),
                data.conditionNewArg.ToLong().Postfixed(), currentValue.Postfixed());

            entry.redeemButton.gameObject.SetActive(true);
            entry.redeemButton.interactable = canBeRedeemed;

            entry.rewardGemText.text = data.RewardGemString;
            entry.rewardGemText.gameObject.SetActive(false);
            entry.rewardGemText.gameObject.SetActive(true);

            entry.achievementData = data;
        }

        for (var idx = achievements.Count; idx < entries.Length; idx++)
        {
            entries[idx].gameObject.SetActive(false);
        }
    }

    public void UpdateAchievementProgress(string updateGroupKey = "")
    {
        var gathered = BlackContext.Instance.AchievementGathered;
        var redeemed = BlackContext.Instance.AchievementRedeemed;
        if (gathered == null || redeemed == null) return;

        if (BlackContext.Instance == null) return;
        if (Data.dataSet == null) return;

        var achievementsDict = new Dictionary<string, List<AchievementData>>
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

        var achievements = new List<Tuple<AchievementData, bool>>();

        foreach (var key in priority)
        {
            if (!achievementsDict.ContainsKey(key)) continue;

            var result = achievementsDict[key].GetAvailableAchievement(
                gathered.GetValue(key), redeemed.GetValue(key));
            if (result == null) continue;

            achievements.Add(new Tuple<AchievementData, bool>(result.Item1, true));
            achievementsDict.Remove(key);
        }

        foreach (var key in priority)
        {
            if (!achievementsDict.ContainsKey(key)) continue;

            var result = achievementsDict[key].GetOngoingAchievement(
                gathered.GetValue(key), redeemed.GetValue(key));
            if (result == null) continue;

            achievements.Add(new Tuple<AchievementData, bool>(result.Item1, false));
            achievementsDict.Remove(key);
        }

        UpdateAchievementUI(achievements);
    }
}