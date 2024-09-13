using System;
using System.Collections;
using System.Collections.Generic;
using ConditionalDebug;
using Dirichlet.Numerics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GridWorld : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    static bool Verbose => false;

    readonly HashSet<uint> coloredMinPoints = new HashSet<uint>();

    [SerializeField]
    GameObject animatedCoinPrefab;

    [SerializeField]
    int coin;

    [SerializeField]
    RectTransform coinIconRt;

    [SerializeField]
    TextMeshProUGUI coinText;

    public Dictionary<uint, int> coloredIslandCountByColor = new Dictionary<uint, int>();

    [SerializeField]
    PlayableDirector finaleDirector;

    [SerializeField]
    public ComboEffect comboEffector;

    [SerializeField]
    ScInt gold = 0;

    [SerializeField]
    IslandLabelSpawner islandLabelSpawner;

    [SerializeField]
    PaletteButtonGroup paletteButtonGroup;

    Canvas rootCanvas;

    [SerializeField]
    RectTransform rt;

    StageData stageData;

    [SerializeField]
    StageSaveManager stageSaveManager;

    [SerializeField]
    Texture2D tex;

    [SerializeField]
    MainGame mainGame;

    [SerializeField]
    IslandShader3DController islandShader3DController;
    
    public Texture2D Tex => tex;

    public int TexSize => Tex.width;

    public string StageName { get; set; } = "TestStage";

    public int Coin
    {
        get => coin;
        set
        {
            coin = value;
            coinText.text = coin.ToString();
        }
    }

    public int Gold => gold;

    public void OnPointerDown(PointerEventData eventData)
    {
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.dragging == false)
        {
            if (Verbose) ConDebug.Log($"World position 1 = {eventData.pointerCurrentRaycast.worldPosition}");
            transform.InverseTransformPoint(eventData.pointerCurrentRaycast.worldPosition);
            if (Verbose) ConDebug.Log($"World position 2 = {eventData.pointerPressRaycast.worldPosition}");

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, Camera.main,
                out var localPoint))
            {
                //用户选择的调色板颜色
                var currentColorUint = paletteButtonGroup.CurrentPaletteColorUint;//4282861898

                var a1Tex = mainGame.StageMetadata.A1Tex;//"004-OTB-FSNB-DIT-A1 (UnityEngine.Texture2D)"
                var a2Tex = mainGame.StageMetadata.A2Tex;//"004-OTB-FSNB-DIT-A2 (UnityEngine.Texture2D)"

                ConvertLocalPointToIxy(localPoint, out var ix, out var iy);//"(-125.26, 50.47)" 515 844
                var a1Float = a1Tex.GetPixel(ix, iy);//"RGBA(1.000, 1.000, 1.000, 0.522)"
                var a2Float = a2Tex.GetPixel(ix, iy);//"RGBA(1.000, 1.000, 1.000, 0.031)"
                if (Verbose)
                {
                    ConDebug.Log($"Local Point: {localPoint}, IXY: ({ix},{iy}), A1f={a1Float}, A2f={a2Float}");
                }

                var a1 = (int) (a1Float.a * 255);//133
                var a2 = (int) (a2Float.a * 255);//8
                var paletteIndex = a1 & ((1 << 6) - 1);//5
                var islandIndex = ((a1 >> 6) & 0x3) | (a2 << 2);//34
                
                var fillResult = FillResult.NotDetermined;
                
                if (currentColorUint == PaletteButtonGroup.InvalidPaletteColor)
                {
                    fillResult = FillResult.NoPaletteSelected;
                }
                else if (islandIndex == 0 && paletteIndex == 0)
                {
                    fillResult = FillResult.Outline;
                }
                else
                {
                    if (islandIndex <= 0 || islandIndex >= 1 + stageData.CachedIslandDataList.Count)//34, 50
                    {
                        Debug.LogError("Out of range island index. [LOGIC ERROR]");
                        fillResult = FillResult.OutsideOfCanvas;
                    }

                    if (paletteIndex <= 0 || paletteIndex >= 1 + stageData.CachedPaletteArray.Length)
                    {
                        Debug.LogError("Out of range island index. [LOGIC ERROR]");
                        fillResult = FillResult.OutsideOfCanvas;
                    }
                }

                if (fillResult == FillResult.NotDetermined)
                {
                    if (islandIndex > 0 && paletteIndex > 0)//34,5
                    {
                        // 用户选择的栏的正确答案颜色
                        var solutionColorUint = stageData.CachedIslandDataList[islandIndex - 1].IslandData.rgba;//4282861898

                        // 用户选择的栏中的Min Point
                        var fillMinPointUint = stageData.CachedIslandDataList[islandIndex - 1].MinPoint;//44040687

                        if (solutionColorUint == currentColorUint)//4282861898==4282861898
                        {
                            if (islandLabelSpawner.ContainsMinPoint(fillMinPointUint))
                            {
                                fillResult = FillResult.Good;

                                // 实际上渲染填充。
                                islandShader3DController.SetIslandIndex(islandIndex);//34

                                UpdatePaletteBySolutionColor(fillMinPointUint, solutionColorUint, false);//44040687,4282861898,
                            }
                            else
                            {
                                // 已经涂好的格子。
                                fillResult = FillResult.AlreadyFilled;
                            }
                        }
                        else
                        {
                            fillResult = FillResult.WrongColor;
                        }
                    }
                    else
                    {
                        fillResult = FillResult.Outline;
                    }
                }

                if (fillResult == FillResult.Good)
                {
                    // 特别硬币获得演出-因为是还没有完成的功能，所以从上市规格中去掉吧。
                    //StartAnimateFillCoin(localPoint);

                    Sound.Instance.PlayFillOkay();

                    BlackContext.Instance.StageCombo++;
                    comboEffector.Play(BlackContext.Instance.StageCombo);

                    // 这次涂的是最后一个格子吗？（都涂好了吗？）
                    if (IsLabelByMinPointEmpty
#if DEV_BUILD
                        || (Application.isEditor && Keyboard.current[Key.LeftShift].isPressed)
#endif
                    )
                    {
                        StartFinale();
                    }
                }
                else
                {
                    // 仅在尝试使用错误的调色板按钮进行涂色时显示错误
                    if (fillResult == FillResult.WrongColor)
                    {
                        // TODO: 需要使用以下消息翻译密钥进行响应
                        ToastMessage.Instance.PlayWarnAnim("请重新确认颜色。");
                        
                        if (!BlackContext.Instance.ComboAdminMode)
                        {
                            BlackContext.Instance.StageCombo = 0;
                        }
                    }
                }

                if (Verbose) ConDebug.Log($"Local position = {localPoint}");
            }
        }
    }

    bool IsLabelByMinPointEmpty => islandLabelSpawner.IsLabelByMinPointEmpty;

    void StartFinale()
    {
        mainGame.DeactivateTime();
        finaleDirector.Play(finaleDirector.playableAsset);
        UpdateLastClearedStageIdAndSave();
    }

    void UpdateLastClearedStageIdAndSave()
    {
        if (!StageButton.CurrentStageMetadata)
        {
            if (Verbose)
                ConDebug.Log(
                    "Current stage metadata is not set. Last cleared stage ID will not be updated. (Did you start the play mode from Main scene?)");
            return;
        }

        var stageName = StageButton.CurrentStageMetadata.name;
        for (var i = 0; i < Data.dataSet.StageSequenceData.Count; i++)
        {
            if (Data.dataSet.StageSequenceData[i].stageName == stageName)
            {
                var oldClearedStageId = BlackContext.Instance.LastClearedStageId;
                var newClearedStageId = i + 1;

                BlackContext.Instance.LastClearedStageId = Mathf.Max(oldClearedStageId, newClearedStageId);

                // 舞台通关取得了进展。给予补偿。
                if (newClearedStageId > oldClearedStageId)
                {
                    // 关卡舞台将追加金币。
                    RewardGoldAmount = new UInt128(newClearedStageId % 5 == 0 ? 3 : 1);
                    BlackContext.Instance.AddPendingGold(RewardGoldAmount);

                    BlackContext.Instance.AchievementGathered.MaxBlackLevel =
                        (UInt128) BlackContext.Instance.LastClearedStageId.ToInt();
                }
            }
        }

        var combo = (UInt128) BlackContext.Instance.StageCombo.ToInt();
        if (BlackContext.Instance.AchievementGathered.MaxColoringCombo < combo)
        {
            BlackContext.Instance.AchievementGathered.MaxColoringCombo = combo;
        }

        SaveLoadManager.Save(BlackContext.Instance, ConfigPopup.Instance, Sound.Instance, Data.Instance, null);
    }

    public UInt128 RewardGoldAmount { get; private set; }

#if UNITY_EDITOR
    void OnValidate()
    {
        rt = GetComponent<RectTransform>();
    }
#endif

    void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        Coin = 0;
    }

    public void LoadTexture(Texture2D inputTexture, StageData inStageData)
    {
        tex = inputTexture;
        stageData = inStageData;
    }

    // 填充调色板信息后，请检索进度
    //可以用正确的颜色填充。
    internal void ResumeGame()
    {
        try
        {
            var stageSaveData = StageSaveManager.Load(StageName) ?? stageSaveManager.CreateStageSaveData(StageName);

            // 已经全部涂好了，如果是通过重播功能进来的，就不需要修复了。
            if (stageData.islandDataByMinPoint.Count <= stageSaveData.coloredMinPoints.Count
                && StageButton.CurrentStageMetadataReplay)
            {
                stageSaveData = stageSaveManager.CreateStageSaveData(StageName);
            }
            
            stageSaveManager.RestoreCameraState(stageSaveData);
            RestorePaletteAndFillState(stageSaveData.coloredMinPoints);
            mainGame.SetRemainTime(stageSaveData.remainTime);
            if (IsLabelByMinPointEmpty)
            {
                StartFinale();
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            DeleteSaveFileAndReloadScene();
        }
    }

    public void DeleteSaveFileAndReloadScene()
    {
        DeleteSaveFile();
        SceneManager.LoadScene("Main");
    }

    public void DeleteSaveFile()
    {
        StageSaveManager.DeleteSaveFile(StageName);
    }
    
    void UpdatePaletteBySolutionColor(uint fillMinPointUint, uint solutionColorUint, bool batch)
    {
        islandLabelSpawner.DestroyLabelByMinPoint(fillMinPointUint);//3866689

        coloredMinPoints.Add(fillMinPointUint);

        if (coloredIslandCountByColor.TryGetValue(solutionColorUint, out var coloredIslandCount))//4294901246,0
            coloredIslandCount++;
        else
            coloredIslandCount = 1;

        coloredIslandCountByColor[solutionColorUint] = coloredIslandCount;//[4294901246]=1
        paletteButtonGroup.UpdateColoredCount(solutionColorUint, coloredIslandCount, batch); //4294901246,1,false
    }

    enum FillResult
    {
        NotDetermined,
        Good,
        Outline,
        WrongColor,
        AlreadyFilled,
        NoPaletteSelected,
        OutsideOfCanvas,
    }

    void ConvertLocalPointToIxy(Vector2 localPoint, out int ix, out int iy)
    {
        var rect = rt.rect;
        var w = rect.width;
        var h = rect.height;

        ix = (int) ((localPoint.x + w / 2) / w * Tex.width);
        iy = (int) ((localPoint.y + h / 2) / h * Tex.height);

        if (Verbose) ConDebug.Log($"w={w} / h={h}");
    }

    void RestorePaletteAndFillState(HashSet<uint> inColoredMinPoints)
    {
        if (Verbose)
        {
            ConDebug.Log($"Starting batch fill of {inColoredMinPoints.Count} points");
        }

        if (inColoredMinPoints.Count <= 0) return;
        
        foreach (var minPoint in inColoredMinPoints)
        {
            if (stageData.islandDataByMinPoint.TryGetValue(minPoint, out var islandData))
            {
                islandShader3DController.EnqueueIslandIndex(islandData.index);
                UpdatePaletteBySolutionColor(minPoint, islandData.rgba, true);
            }
            else
            {
                Debug.LogError($"Island data (min point = {minPoint} cannot be found in StageData.");
            }
        }
    }

    void StartAnimateFillCoin(Vector2 localPoint)
    {
        var animatedCoin = Instantiate(animatedCoinPrefab, transform).GetComponent<AnimatedCoin>();
        animatedCoin.Rt.anchoredPosition = localPoint;
        animatedCoin.TargetRt = coinIconRt;
        var animatedCoinTransform = animatedCoin.transform;
        animatedCoinTransform.SetParent(rootCanvas.transform, true);
        animatedCoinTransform.localScale = Vector3.one;
        animatedCoin.GridWorld = this;
        gold++;
    }

    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            WriteStageSaveData();
        }
    }

    public void WriteStageSaveData()
    {
        //每个阶段的进度数据
        stageSaveManager.Save(StageName, coloredMinPoints, mainGame.GetRemainTime());

        // 完整存储数据
        SaveLoadManager.Save(BlackContext.Instance, ConfigPopup.Instance, Sound.Instance, Data.Instance, null);
    }

    void OnApplicationQuit()
    {
        WriteStageSaveData();
    }
}