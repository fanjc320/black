using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StageDetailPopup : MonoBehaviour
{
    // 该组件单独用于重放和解锁新阶段，因此它不使用单例模式。
    //public static StageDetailPopup instance;

    [SerializeField]
    StageButton stageButton;

    [SerializeField]
    Subcanvas subcanvas;

    [SerializeField]
    StageProgress stageProgress;

    [SerializeField]
    GameObject easelExclamationMark;

    [SerializeField]
    BottomTip bottomTip;

    [SerializeField]
    StageLocker stageLocker;

    [SerializeField]
    Text startStageButtonText;

    [SerializeField]
    IslandShader3DController islandShader3DController;

    [SerializeField]
    bool replay;

    public static bool IsAllCleared => BlackContext.Instance.LastClearedStageId >= Data.dataSet.StageSequenceData.Count;

    public float StageLockDetailTime
    {
        get => stageLocker.RemainTime;
        set => stageLocker.RemainTime = value;
    }

    void Start()
    {
        SetInitialBottomTip();
    }

    void OnEnable()
    {
        stageLocker.OnStageLocked += OnStageLocked;
        stageLocker.OnStageUnlocked += OnStageUnlocked;
    }

    void OnDisable()
    {
        stageLocker.OnStageLocked -= OnStageLocked;
        stageLocker.OnStageUnlocked -= OnStageUnlocked;
    }

    // stageId请注意，它是作为 stageIndex 接收的，而不是 。
    // lastClearedStageId如果作为参数输入，则将其设置为要玩的下一个棋盘。
    public async Task OpenPopupAfterLoadingAsync(int stageIndex)
    {
        if (stageIndex < 0) stageIndex = 0;

        // 您是否想破坏您曾经破坏过的游戏？
        replay = stageIndex + 1 <= BlackContext.Instance.LastClearedStageId;

        if (IsAllCleared && replay == false)
        {
            //Debug.LogError("lastClearedStageId exceeds Data.dataSet.StageMetadataList count.");
            ConfirmPopup.Instance.Open(@"\所有关卡均已通关！\n真正的博物馆重建开始时，敬请期待下一次更新!".Localized(),
                ConfirmPopup.Instance.Close);
            return;
        }

        stageProgress.ProgressInt = stageIndex % 5;

        ProgressMessage.Instance.Open(@"\正在准备图片...".Localized());

        // 最后清除的 ID 是从 1 开始的，下面的函数是从 0 开始操作的。
        // 当您检索下一个要播放的舞台时，只需按原样传递 ID 即可。
        //var stageMetadata = await LoadStageMetadataByZeroBasedIndexAsync(stageIndex);
        var stageMetadata = await LoadStageMetadataByZeroBasedIndexAsync(46);

        if (stageMetadata == null)
        {
            // 一个主要问题
            Debug.LogError("Stage metadata is null");
            return;
        }

        islandShader3DController.Initialize(stageMetadata);

        var stageSaveData = StageSaveManager.Load(stageMetadata.name);
        if (stageSaveData != null)
        {
            foreach (var minPoint in stageSaveData.coloredMinPoints)
            {
                if (stageMetadata.StageData.islandDataByMinPoint.TryGetValue(minPoint, out var islandData))
                {
                    islandShader3DController.EnqueueIslandIndex(islandData.index);
                }
                else
                {
                    Debug.LogError($"Island data (min point = {minPoint} cannot be found in StageData.");
                }
            }
        }

        ProgressMessage.Instance.Close();
        var resumed = stageButton.SetStageMetadata(stageMetadata);
        subcanvas.Open();

        // 如果您在玩时完成一个阶段，则不应该有任何等待时间。
        // 前期没有等待时间。
        //if (replay || resumed || stageMetadata.StageSequenceData.skipLock)
        {
            stageLocker.Unlock();
        }
        //else
        {
            //stageLocker.Lock();
        }

        //stageProgress.Show(replay == false);

        if (easelExclamationMark != null)
        {
            easelExclamationMark.SetActive(false);
        }

        if (bottomTip != null)
        {
            if (BlackContext.Instance.LastClearedStageId == 0)
            {
                bottomTip.SetMessage("\\单击“开始”开始着色~!".Localized());
                bottomTip.OpenSubcanvas();
            }
            else if (BlackContext.Instance.LastClearedStageId == 1)
            {
                bottomTip.SetMessage("\\好工作！让我们赶紧为下一个阶段上色吧.".Localized());
                bottomTip.OpenSubcanvas();
            }
            else if (BlackContext.Instance.LastClearedStageId == 4)
            {
                bottomTip.SetMessage("\\这个阶段是有时间限制的‘入门阶段’！大胆试试吧!!!".Localized());
                bottomTip.OpenSubcanvas();
            }
        }
    }

    public static async Task<StageMetadata> LoadStageMetadataByZeroBasedIndexAsync(ScInt zeroBasedIndex)
    {
        if (zeroBasedIndex < 0 || zeroBasedIndex >= Data.dataSet.StageMetadataLocList.Count)
        {
            Debug.LogError($"Stage index {zeroBasedIndex} (zero-based) is out of range");
            return null;
        }
        
        var stageMetadataLoc = Data.dataSet.StageMetadataLocList[zeroBasedIndex];
        if (stageMetadataLoc == null)
        {
            Debug.LogError($"Stage metadata at index {zeroBasedIndex} (zero-based) is null");
            return null;
        }

        var stageMetadata = await Addressables.LoadAssetAsync<StageMetadata>(stageMetadataLoc).Task;
        if (stageMetadata == null)
        {
            Debug.LogError($"Stage metadata with zero based index {zeroBasedIndex} is null");
            return null;
        }

        stageMetadata.StageIndex = zeroBasedIndex;
        return stageMetadata;
    }

    [UsedImplicitly]
    public void OpenPopup()
    {
    }

    [UsedImplicitly]
    void ClosePopup()
    {
        if (easelExclamationMark != null)
        {
            easelExclamationMark.SetActive(IsAllCleared == false);
        }

        SetInitialBottomTip();
    }

    void SetInitialBottomTip()
    {
        if (bottomTip == null) return;

        if (BlackContext.Instance.LastClearedStageId == 0)
        {
            bottomTip.SetMessage("\\触摸画架以检查您想要着色的图片.".Localized());
        }
        else
        {
            bottomTip.CloseSubcanvas();
        }
    }

    public void OnStageStartButton()
    {
        Sound.Instance.PlayButtonClick();

        if (stageLocker.Locked == false
#if DEV_BUILD
            || Application.isEditor && Keyboard.current[Key.LeftShift].isPressed
#endif
        )
        {
            if (replay)
            {
                var stageMetadata = stageButton.GetStageMetadata();
                var stageTitle = Data.dataSet.StageSequenceData[stageMetadata.StageIndex].title;

                ConfirmPopup.Instance.OpenYesNoPopup(
                    @"\'{0}' 您想开始舞台吗？\n\n您可以随时从设置菜单返回博物馆.".Localized(stageTitle),
                    GoToMain, ConfirmPopup.Instance.Close);
            }
            else
            {
                GoToMain();
            }
        }
        else if (PlatformAdMobAds.Instance != null)
        {
            var adContext = new BlackAdContext(stageLocker.Unlock);
            PlatformAdMobAds.Instance.TryShowRewardedAd(adContext);
        }
        else
        {
            ConfirmPopup.Instance.OpenSimpleMessage(
                Application.isEditor
                    ? "Ad not supported on this platform. Click the button while Left Shift key."
                    : "Ad Mob instance not found.");
        }
    }

    void GoToMain()
    {
        stageButton.SetStageMetadataToCurrent(replay);
        SaveLoadManager.Save(BlackContext.Instance, ConfigPopup.Instance, Sound.Instance, Data.Instance,
            null);
        SceneManager.LoadScene("Main");
    }

    void OnStageUnlocked()
    {
        startStageButtonText.text = @"\现在开始".Localized();
    }

    void OnStageLocked()
    {
        startStageButtonText.text = @"\观看广告后立即开始".Localized();
    }
}