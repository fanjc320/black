using ConditionalDebug;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AchievementEntry : MonoBehaviour
{
    public AchievementData achievementData;
    public Text achievementDesc;
    public Text achievementName;

    [SerializeField]
    AchievementRedeemButton achievementRedeemButton;

    public Image image;
    public Button redeemButton;

    [SerializeField]
    Image rewardGemImage;

    public Text rewardGemText;

    //按钮回调
    public void Redeem()
    {
        if (achievementRedeemButton != null && achievementRedeemButton.RepeatedThresholdSatisfied)
        {
            // 在连续购买状态下，防止触摸结束时增加一个购买的症状。
        }
        else
        {
            RedeemInternal();
        }
    }

    public void RedeemInternal()
    {
        // 禁用的意思是没有满足得到补偿的条件。
        if (redeemButton.interactable == false) return;

        // add reward
        // TODO 动画放在下一个版本中
        // var clonedGameObject = InstantiateLocalized.InstantiateLocalize(rewardGemImage.gameObject,
        //     BlackContext.Instance.AnimatedIncrementParent, true);
        BlackContext.Instance.AddGoldSafe((ulong) achievementData.rewardGem.ToLong());
        ConDebug.Log((ulong) achievementData.rewardGem.ToLong());

        // BlackContext.Instance.IncreaseGemAnimated(achievementData.rewardGem, clonedGameObject,
        //     BlackLogEntry.Type.GemAddAchievement, achievementData.id);
        Sound.Instance.PlaySoftTada();

        // update redeemed stat
        if (achievementData.condition == "maxBlackLevel")
            BlackContext.Instance.AchievementRedeemed.MaxBlackLevel = (ulong) achievementData.conditionNewArg.ToLong();
        else if (achievementData.condition == "maxColoringCombo")
            BlackContext.Instance.AchievementRedeemed.MaxColoringCombo = (ulong) achievementData.conditionNewArg.ToLong();
        else
            Debug.LogErrorFormat("Unknown achievement condition: {0}", achievementData.condition);

        // refresh achievement popup
        // *** 没有必要在这里重新更新。BlackContext.Instance.achievementRedeemed的属性发生变化时
        // *** 默认更新。在这里做同样的事情就是做两次。
        // GetComponentInParent<AchievementPopup>().UpdateAchievementTab();
        SaveLoadManager.Save(BlackContext.Instance, ConfigPopup.Instance, Sound.Instance, Data.Instance, null);
    }
}