using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using ConditionalDebug;
using Dirichlet.Numerics;
using MessagePack;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class SaveLoadManager : MonoBehaviour, IPlatformSaveLoadManager
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum SaveReason
    {
        Quit,
        Pause,
        AutoSave,
        BeforeStage
    }

    const int LatestVersion = 4;
    static readonly string localSaveFileName = "save.dat";

    public static SaveLoadManager Instance;

    // 총 maxSaveDataSlot개의 저장 슬롯이 있고, 이를 돌려가며 쓴다.
    public static readonly int maxSaveDataSlot = 9;
    static readonly string saveDataSlotKey = "Save Data Slot";

    static byte[] lastSaveDataArray;

    [SerializeField]
    BlackContext blackContext;

    [SerializeField]
    NetworkTime networkTime;

    public static string SaveFileName => GetSaveLoadFilePathName(GetSaveSlot() + 1);

    public static string LoadFileName => GetSaveLoadFilePathName(GetSaveSlot());

    string IPlatformSaveLoadManager.GetLoadOverwriteConfirmMessage(byte[] bytes)
    {
        return BlackPlatform.GetLoadOverwriteConfirmMessage(BlackPlatform.Instance.GetCloudMetadataFromBytes(bytes));
    }

    string IPlatformSaveLoadManager.GetSaveOverwriteConfirmMessage(byte[] bytes)
    {
        return BlackPlatform.GetSaveOverwriteConfirmMessage(BlackPlatform.Instance.GetCloudMetadataFromBytes(bytes));
    }

    bool IPlatformSaveLoadManager.IsLoadRollback(byte[] bytes)
    {
        return BlackPlatform.IsLoadRollback(BlackPlatform.Instance.GetCloudMetadataFromBytes(bytes));
    }

    bool IPlatformSaveLoadManager.IsSaveRollback(byte[] bytes)
    {
        return BlackPlatform.IsSaveRollback(BlackPlatform.Instance.GetCloudMetadataFromBytes(bytes));
    }

    void IPlatformSaveLoadManager.SaveBeforeCloudSave()
    {
        BlackPlatform.Instance.SaveBeforeCloudSave();
    }

    static int PositiveMod(int x, int m)
    {
        return (x % m + m) % m;
    }

    void Start()
    {
        // 加载保存数据 *** 在任何其他初始化之前必须完成的任务 ***
        Load(blackContext);
    }

    static string GetSaveLoadFilePathName(int saveDataSlot)
    {
        return Path.Combine(Application.persistentDataPath, GetSaveLoadFileNameOnly(saveDataSlot));
    }

    public static string GetSaveLoadFileNameOnly(int saveDataSlot)
    {
        saveDataSlot = PositiveMod(saveDataSlot, maxSaveDataSlot);
        // 하위 호환성을 위해 0인 경우 기존 이름을 쓴다.
        return saveDataSlot == 0 ? localSaveFileName : $"save{saveDataSlot}.dat";
    }

    static int GetSaveSlot()
    {
        return PlayerPrefs.GetInt(saveDataSlotKey, 0);
    }

    // 저장 슬롯 증가 (성공적인 저장 후 항상 1씩 증가되어야 함)
    public static void IncreaseSaveDataSlotAndWrite()
    {
        var oldSaveDataSlot = GetSaveSlot();
        var newSaveDataSlot = PositiveMod(oldSaveDataSlot + 1, maxSaveDataSlot);
        ConDebug.Log($"Increase save data slot from {oldSaveDataSlot} to {newSaveDataSlot}...");
        PlayerPrefs.SetInt(saveDataSlotKey, newSaveDataSlot);
        PlayerPrefs.Save();
        ConDebug.Log($"Increase save data slot from {oldSaveDataSlot} to {newSaveDataSlot}... OKAY");
    }

    // 저장 슬롯 감소 (불러오기 실패 후 항상 1씩 감소되어야 함)
    public static void DecreaseSaveDataSlotAndWrite()
    {
        var oldSaveDataSlot = GetSaveSlot();
        var newSaveDataSlot = PositiveMod(oldSaveDataSlot - 1, maxSaveDataSlot);
        ConDebug.Log($"Decrease save data slot from {oldSaveDataSlot} to {newSaveDataSlot}...");
        PlayerPrefs.SetInt(saveDataSlotKey, newSaveDataSlot);
        PlayerPrefs.Save();
        ConDebug.Log($"Decrease save data slot from {oldSaveDataSlot} to {newSaveDataSlot}... OKAY");
    }

    static void ResetSaveDataSlotAndWrite()
    {
        lastSaveDataArray = null;
        PlayerPrefs.SetInt(saveDataSlotKey, 0);
        PlayerPrefs.Save();
    }

    internal static void DeleteSaveFileAndReloadScene()
    {
        // From MSDN: If the file to be deleted does not exist, no exception is thrown.
        ConDebug.Log("DeleteSaveFileAndReloadScene");
        DeleteAllSaveFiles();
        Splash.LoadSplashScene();
    }

    public static void DeleteAllSaveFiles()
    {
        for (var i = 0; i < maxSaveDataSlot; i++)
        {
            File.Delete(GetSaveLoadFilePathName(i));
        }
        
        // 모든 Persistent 파일 삭제... 괜찮은가?
        foreach (var filePath in Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories))
        {
            File.Delete(filePath);
        }

        ResetSaveDataSlotAndWrite();
    }

    public static bool Save(IBlackContext context, ConfigPopup configPopup, Sound sound, Data data, StageSaveData wipStageSaveData)
    {
        // 에디터에서 간혹 게임 플레이 시작할 때 Load도 호출되기도 전에 Save가 먼저 호출되기도 한다.
        // (OnApplicationPause 통해서)
        // 실제 기기에서도 이럴 수 있나? 이러면 망인데...
        // 그래서 플래그를 하나 추가한다. 이 플래그는 로드가 제대로 한번 됐을 때 true로 변경된다.
        if (context == null || context.LoadedAtLeastOnce == false)
        {
            Debug.LogWarning(
                "****** Save() called before first Load(). There might be an error during Load(). Save() will be skipped to prevent losing your save data.");
            return false;
        }

        var blackSaveData = new BlackSaveData
        {
            version = LatestVersion,
            lastClearedStageId = BlackContext.Instance.LastClearedStageId,
            lastClearedStageIdEvent = BlackContext.Instance.LastClearedStageIdEvent,
            goldScUInt128 = BlackContext.Instance.Gold,
            clearedDebrisIndexList = BlackContext.Instance.GetDebrisState(),
            pendingGoldScUInt128 = BlackContext.Instance.PendingGold,
            bgmAudioVolume = 1.0f,
            sfxAudioVolume = 1.0f,
            muteBgmAudioSource = Sound.Instance.BgmAudioSourceActive == false,
            muteSfxAudioSource = Sound.Instance.SfxAudioSourceActive == false,
            maxBlackLevelGathered = BlackContext.Instance.AchievementGathered.MaxBlackLevel,
            maxBlackLevelRedeemed = BlackContext.Instance.AchievementRedeemed.MaxBlackLevel,
            maxColoringComboGathered = BlackContext.Instance.AchievementGathered.MaxColoringCombo,
            maxColoringComboRedeemed = BlackContext.Instance.AchievementRedeemed.MaxColoringCombo,
            //stageLockRemainTime = StageDetail.Instance.StageLockDetailTime,
            wipStageSaveData = wipStageSaveData,
            performanceMode = ConfigPopup.Instance.IsPerformanceModeOn,
        };

        return SaveBlackSaveData(blackSaveData);
    }

    static void WriteAllBytesAtomically(string filePath, byte[] bytes)
    {
        var temporaryPath = CreateNewTempPath();
        using (var tempFile = File.Create(temporaryPath, 4 * 1024, FileOptions.WriteThrough))
        {
            tempFile.Write(bytes, 0, bytes.Length);
            tempFile.Close();
        }

        File.Delete(filePath);
        File.Move(temporaryPath, filePath);
    }

    static string CreateNewTempPath()
    {
        return Path.Combine(Application.temporaryCachePath, Guid.NewGuid().ToString());
    }

    static bool SaveBlackSaveData(BlackSaveData blackSaveData)
    {
        //ConDebug.LogFormat("Start Saving JSON Data: {0}", JsonUtility.ToJson(blackSaveData));
        var saveDataArray = MessagePackSerializer.Serialize(blackSaveData, Data.DefaultOptions);
        ConDebug.LogFormat("Saving path: {0}", SaveFileName);
        if (lastSaveDataArray != null && lastSaveDataArray.SequenceEqual(saveDataArray))
            ConDebug.LogFormat("Saving skipped since there is no difference made compared to last time saved.");
        else
            try
            {
                // 진짜 쓰자!!
                WriteAllBytesAtomically(SaveFileName, saveDataArray);

                // 마지막 저장 데이터 갱신
                lastSaveDataArray = saveDataArray;
                ConDebug.Log($"{SaveFileName} Saved. (written to disk)");

                // 유저 서비스를 위해 필요할 수도 있으니까 개발 중일 때는 base64 인코딩 버전 세이브 파일도 저장한다.
                // 실서비스 버전에서는 불필요한 기능이다.
                if (Application.isEditor)
                {
                    var base64Path = SaveFileName + ".base64.txt";
                    ConDebug.LogFormat("Saving path (base64): {0}", base64Path);
                    File.WriteAllText(base64Path, Convert.ToBase64String(saveDataArray));
                    ConDebug.Log($"{base64Path} Saved. (written to disk)");
                }

                IncreaseSaveDataSlotAndWrite();
                var lastBlackLevel = blackSaveData.lastClearedStageId;
                var gem = (blackSaveData.freeGemScUInt128 + blackSaveData.paidGemScUInt128).ToUInt128()
                    .ToClampedLong();
                BlackLogManager.Add(BlackLogEntry.Type.GameSaved, lastBlackLevel, gem);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("Writing to disk failed!!!");
                ConfirmPopup.Instance.Open("Writing to disk failed!!!");
                BlackLogManager.Add(BlackLogEntry.Type.GameSaveFailure, 0, 0);
                return false;
            }

        return true;
    }

    static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
    {
        if (val.CompareTo(min) < 0) return min;
        if (val.CompareTo(max) > 0) return max;
        return val;
    }

    static void Load(IBlackContext context)
    {
        // 尝试加载所有保存槽直至成功。
        var exceptionList = new List<Exception>();

        for (var i = 0; i < maxSaveDataSlot; i++)
            try
            {
                if (LoadInternal(context))
                {
                    // 已正确读取其中一个保存文件。
                    if (i != 0)
                        // 但是，如果多次失败，则会显示错误消息。
                        Debug.LogError($"Save data rolled back {i} time(s)...!!!");

                    // 比赛可以继续进行。不管是不是回滚，我都读过……
                    return;
                }

                // 可能不是因为发生异常而失败，但也有可能是失败。
                // 无论如何，失败就是失败。
                // 转到上一个槽。
                exceptionList.Add(new Exception("Black Save Data Load Exception"));
                DecreaseSaveDataSlotAndWrite();
            }
            catch (NotSupportedBlackSaveDataVersionException e)
            {
                // 不支持的保存文件版本
                Debug.LogWarning(e.ToString());
                exceptionList.Add(e);
                DecreaseSaveDataSlotAndWrite();
            }
            catch (SaveFileNotFoundException e)
            {
                // 本身没有保存文件吗？
                Debug.LogWarning(e.ToString());
                exceptionList.Add(e);
                DecreaseSaveDataSlotAndWrite();
            }
            catch (PurchaseCountBanException e)
            {
                // BAN
                Debug.LogWarning(e.ToString());
                exceptionList.Add(e);
                break;
            }
            catch (LocalUserIdBanException e)
            {
                // BAN
                Debug.LogWarning(e.ToString());
                exceptionList.Add(e);
                break;
            }
            catch (Exception e)
            {
                // 读取保存文件时发生未知异常
                // 发生了非常错误的事情...
                // 这可是大事啊~~
                // 转到上一个槽。
                Debug.LogException(e);
                exceptionList.Add(e);
                DecreaseSaveDataSlotAndWrite();
                BlackLogManager.Add(BlackLogEntry.Type.GameLoadFailure, 0, GetSaveSlot());
            }

        if (exceptionList.All(e => e.GetType() == typeof(SaveFileNotFoundException)))
        {
            // 没有保存文件。
            // 我是新用户~~~让风铃响~~~~~~
            ProcessNewUser(context, exceptionList[0]);
        }
        else if (exceptionList.Any(e => e.GetType() == typeof(NotSupportedBlackSaveDataVersionException)))
        {
            var exception = (NotSupportedBlackSaveDataVersionException) exceptionList.FirstOrDefault(e =>
                e.GetType() == typeof(NotSupportedBlackSaveDataVersionException));
            if (exception != null)
            {
                // 这个问题可以通过升级到新版本来解决。
                ProcessCriticalLoadErrorPrelude(exceptionList);
                ProcessUpdateNeededError(exception.SaveFileVersion);
            }
            else
            {
                Debug.LogError("...?");
                ProcessWtfError(exceptionList);
            }
        }
        else
        {
            Debug.LogError("All save files cannot be loaded....T.T");
            ProcessWtfError(exceptionList);
        }
    }

    static void ProcessWtfError(List<Exception> exceptionList)
    {
        // W.T.F.
        var st = ProcessCriticalLoadErrorPrelude(exceptionList);
        ProcessCriticalLoadError(exceptionList, st);
    }

    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    static bool LoadInternal(IBlackContext context)
    {
        var blackSaveData = LoadBlackSaveData();

        // 这是保存数据本身有错误的情况。
        if (blackSaveData.version < 1) return false;

        var oldVersion = blackSaveData.version;
        // 迁移到最新版本数据
        MigrateBlackSaveData(blackSaveData);

        if (blackSaveData.version == LatestVersion)
        {
            // GOOD!
        }
        else if (blackSaveData.version > LatestVersion)
        {
            if (Application.isEditor)
                Debug.LogError(
                    "NotSupportedBlackSaveDataVersionException should be thrown at this point in devices. In editor, you can proceed without error...");
            else
                // 存档版本高吗？看来最新版本保存的云保存文件是在旧版本客户端中从云端加载的。
                throw new NotSupportedBlackSaveDataVersionException(blackSaveData.version);
        }
        else
        {
            throw new Exception(
                $"[CRITICAL ERROR] Latest version {LatestVersion} not match save file latest version field {blackSaveData.version}!!!");
        }

        // 以下代码的操作根据是否确定作弊模式而变化。
        // 让我们尽早开始吧。
        // (比如分配context.LastDailyRewardRedeemedIndex时是否注册排行榜等)
        context.CheatMode = blackSaveData.cheatMode;
        context.WaiveBan = blackSaveData.waiveBan;

        // 如果检测到欺诈用户，则首先需要此信息，因此请先加载它。
        if (blackSaveData.userPseudoId <= 0) blackSaveData.userPseudoId = NewUserPseudoId();

        context.UserPseudoId = blackSaveData.userPseudoId;
        context.LastConsumedServiceIndex = blackSaveData.lastConsumedServiceIndex;
        context.LastClearedStageId = blackSaveData.lastClearedStageId;
        context.LastClearedStageIdEvent = blackSaveData.lastClearedStageIdEvent;
        context.SetGold(blackSaveData.goldScUInt128);
        context.SetDebrisState(blackSaveData.clearedDebrisIndexList);
        context.SetStageLockRemainTime(blackSaveData.stageLockRemainTime);

        // 过滤掉欺诈用户。
        // 但如果过滤掉的用户不是欺诈用户，可以联系开发团队解决。
        // 这样释放的用户决定将context.waiveBan设置为true。//waive放弃，waiveBan解禁
        // 那么这个例程将根本不起作用。
        if (context.WaiveBan == false)
        {
            var targetIdList = new string[]
            {
            };
            foreach (var targetId in targetIdList)
                if (blackSaveData.localUserDict != null && blackSaveData.localUserDict.Keys.Contains(targetId))
                    throw new LocalUserIdBanException(targetId);

            var revokedReceiptList = new string[]
            {
            };
            foreach (var receipt in revokedReceiptList)
                if (blackSaveData.verifiedProductReceipts != null &&
                    blackSaveData.verifiedProductReceipts.Contains(receipt))
                    throw new RevokedReceiptException(receipt);
        }

        context.SetGold(blackSaveData.goldScUInt128);
        context.SetGemZero();
        context.AddFreeGem(blackSaveData.freeGemScUInt128);
        context.AddPaidGem(blackSaveData.paidGemScUInt128);

        // 恢复宝石变化动画。
        var gemBigInt = context.Gem;
        BlackLogManager.Add(BlackLogEntry.Type.GemToLoaded, 0,
            gemBigInt < long.MaxValue ? (long) gemBigInt : long.MaxValue);

        // 暂时关闭插槽容量变化动画。

        context.SetPendingGold(blackSaveData.pendingGoldScUInt128);
        context.PendingFreeGem = blackSaveData.pendingFreeGemScUInt128;


        context.StashedRewardJsonList = blackSaveData.stashedRewardJsonList;

        context.LastDailyRewardRedeemedTicksList =
            blackSaveData.lastDailyRewardRedeemedTicksList ??
            new List<ScLong> {blackSaveData.lastDailyRewardRedeemedTicks};
        context.NoAdsCode = blackSaveData.noAdsCode;

        ConDebug.Log(
            $"Last Daily Reward Redeemed Index {context.LastDailyRewardRedeemedIndex} / DateTime (UTC) {new DateTime(context.LastDailyRewardRedeemedTicks, DateTimeKind.Utc)}");

        context.ApplyPendingGold();
        context.ApplyPendingFreeGem();

        // 成就
        context.AchievementGathered = new AchievementRecord1(false);
        context.AchievementRedeemed = new AchievementRecord1(false);

        context.AchievementGathered.MaxBlackLevel = blackSaveData.maxBlackLevelGathered;
        context.AchievementRedeemed.MaxBlackLevel = blackSaveData.maxBlackLevelRedeemed;

        context.AchievementGathered.MaxColoringCombo = blackSaveData.maxColoringComboGathered;
        context.AchievementRedeemed.MaxColoringCombo = blackSaveData.maxColoringComboRedeemed;

        AchievePopup.Instance.UpdateAchievementProgress();

        // === Config ===
        Sound.Instance.BgmAudioSourceActive = blackSaveData.muteBgmAudioSource == false;
        Sound.Instance.SfxAudioSourceActive = blackSaveData.muteSfxAudioSource == false;
        Sound.Instance.BgmAudioSourceVolume = blackSaveData.bgmAudioVolume;
        Sound.Instance.SfxAudioSourceVolume = blackSaveData.sfxAudioVolume;

        Sound.Instance.BgmAudioVolume = blackSaveData.bgmAudioVolume;
        Sound.Instance.SfxAudioVolume = blackSaveData.sfxAudioVolume;

        ConfigPopup.Instance.IsNotchOn = blackSaveData.notchSupport;
        ConfigPopup.Instance.IsBottomNotchOn = blackSaveData.bottomNotchSupport;
        ConfigPopup.Instance.IsPerformanceModeOn = blackSaveData.performanceMode;
        ConfigPopup.Instance.IsAlwaysOnOn = blackSaveData.alwaysOn;
        ConfigPopup.Instance.IsBigScreenOn = blackSaveData.bigScreen;

        // Toggle 回调仅在值更改时调用，因此强制调用一次。
        ConfigPopup.SetPerformanceMode(ConfigPopup.Instance.IsPerformanceModeOn);

        if (context.CheatMode) BlackLogManager.Add(BlackLogEntry.Type.GameCheatEnabled, 0, 0);

        switch (blackSaveData.languageCode)
        {
            case BlackLanguageCode.Tw:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Tw);
                break;
            case BlackLanguageCode.Ch:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ch);
                break;
            case BlackLanguageCode.Ja:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ja);
                break;
            case BlackLanguageCode.En:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.En);
                break;
            default:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ko);
                break;
        }

        context.NoticeData = blackSaveData.noticeData ?? new NoticeData();
        context.SaveFileLoaded = true;

        if (Application.isEditor) Admin.SetNoticeDbPostfixToDev();

        NoticeManager.Instance.CheckNoticeSilently();

        // 인앱 상품 구매 내역 디버그 정보
        ConDebug.Log("=== Purchased Begin ===");
        if (blackSaveData.purchasedProductDict != null)
            foreach (var kv in blackSaveData.purchasedProductDict)
                ConDebug.Log($"PURCHASED: {kv.Key} = {kv.Value}");

        ConDebug.Log("=== Purchased End ===");

        // 인앱 상품 영수증 디버그 정보
        ConDebug.Log("=== Purchased Receipt ID Begin ===");
        if (blackSaveData.purchasedProductReceipts != null)
            foreach (var kv in blackSaveData.purchasedProductReceipts)
            foreach (var kvv in kv.Value)
                ConDebug.Log($"PURCHASED RECEIPT ID: {kv.Key} = {kvv}");

        ConDebug.Log("=== Purchased Receipt ID End ===");

        // 인앱 상품 영수증 (검증 완료) 디버그 정보
        ConDebug.Log("=== VERIFIED Receipt ID Begin ===");
        if (blackSaveData.verifiedProductReceipts != null)
            foreach (var v in blackSaveData.verifiedProductReceipts)
                ConDebug.Log($"\"VERIFIED\" RECEIPT ID (THANK YOU!!!): {v}");

        ConDebug.Log("=== VERIFIED Receipt ID End ===");

        context.LocalUserDict = blackSaveData.localUserDict;
        if (context.LocalUserDict != null)
            foreach (var kv in context.LocalUserDict)
                ConDebug.Log(kv.Value);

        context.LoadedAtLeastOnce = true;
        BlackLogManager.Add(BlackLogEntry.Type.GameLoaded, context.LastClearedStageId,
            context.Gem < long.MaxValue ? (long) context.Gem : long.MaxValue);

        return true;
    }

    // ReSharper disable once InvertIf
    static void MigrateBlackSaveData(BlackSaveData blackSaveData)
    {
        if (blackSaveData == null) {
            throw new ArgumentNullException(nameof(blackSaveData));
        }

        if (blackSaveData.version == 1) {
            ConDebug.LogFormat("Upgrading save file version from {0} to {1}", blackSaveData.version,
                blackSaveData.version + 1);

            blackSaveData.version++;
        }
        
        if (blackSaveData.version == 2) {
            ConDebug.LogFormat("Upgrading save file version from {0} to {1}", blackSaveData.version,
                blackSaveData.version + 1);

            blackSaveData.lastClearedStageIdEvent = 0;

            blackSaveData.version++;
        }
        
        if (blackSaveData.version == 3) {
            ConDebug.LogFormat("Upgrading save file version from {0} to {1}", blackSaveData.version,
                blackSaveData.version + 1);

            blackSaveData.lastClearedStageIdEvent = -1;

            blackSaveData.version++;
        }
    }

    static BlackSaveData LoadBlackSaveData()
    {
        ConDebug.Log($"Reading the save file {LoadFileName}...");
        try
        {
            var saveDataArray = File.ReadAllBytes(LoadFileName);
            ConDebug.Log($"Loaded on memory. ({saveDataArray.Length:n0} bytes)");
            return MessagePackSerializer.Deserialize<BlackSaveData>(saveDataArray, Data.DefaultOptions);
        }
        catch (FileNotFoundException)
        {
            throw new SaveFileNotFoundException();
        }
        catch (IsolatedStorageException)
        {
            throw new SaveFileNotFoundException();
        }
    }

    static string ProcessCriticalLoadErrorPrelude(List<Exception> exceptionList)
    {
        Debug.LogErrorFormat("Load: Unknown exception thrown: {0}", exceptionList[0]);
        var t = new StackTrace();
        Debug.LogErrorFormat(t.ToString());
        // 메인 게임 UI 요소를 모두 숨긴다. (아주 심각한 상황. 이 상태로는 무조건 게임 진행은 불가하다.)
        if (BlackContext.Instance.CriticalErrorHiddenCanvasList != null)
            foreach (var canvas in BlackContext.Instance.CriticalErrorHiddenCanvasList)
                canvas.enabled = false;

        return t.ToString();
    }

    public static void ProcessCriticalLoadError(List<Exception> exceptionList, string st)
    {
        BlackLogManager.Add(BlackLogEntry.Type.GameCriticalError, 0, 0);
        ChangeLanguageBySystemLanguage();
        ConfirmPopup.Instance.OpenTwoButtonPopup(
            @"\$중대한 오류 안내$"
                .Localized(), () => UploadSaveFileAsync(exceptionList, st, false),
            () => AskAgainToReportSaveData(exceptionList, st), @"\중대한 오류 발생".Localized(), "\\예".Localized(),
            "\\아니요".Localized());
    }

    static void ProcessUpdateNeededError(int saveFileVersion)
    {
        ChangeLanguageBySystemLanguage();
        ConfirmPopup.Instance.Open(
            @"\$강제 업데이트 안내$".Localized(saveFileVersion,
                LatestVersion), () =>
            {
                // 컬러뮤지엄 앱 상세 페이지로 보낸다.
                Platform.Instance.RequestUserReview();
            });
    }

    static void ChangeLanguageBySystemLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Korean:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ko);
                break;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ch);
                break;
            case SystemLanguage.ChineseTraditional:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Tw);
                break;
            case SystemLanguage.Japanese:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.Ja);
                break;
            case SystemLanguage.English:
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.En);
                break;
            default:
                ConDebug.Log($"Not supported system language {Application.systemLanguage}. Fallback to English...");
                ConfigPopup.Instance.EnableLanguage(BlackLanguageCode.En);
                break;
        }
    }

    public static void EnterRecoveryCode(List<Exception> exceptionList, string st, bool notCriticalError)
    {
        ConfirmPopup.Instance.OpenInputFieldPopup("\\안내 받은 복구 코드를 입력해 주십시오.".Localized(), () =>
        {
            ConfirmPopup.Instance.Close();
            ErrorReporter.Instance.ProcessRecoveryCode(exceptionList, st, ConfirmPopup.Instance.InputFieldText);
        }, () =>
        {
            if (notCriticalError == false)
                ProcessCriticalLoadError(exceptionList, st);
            else
                ConfirmPopup.Instance.Close();
        }, "\\복구 코드".Localized(), Header.Normal, "", "");
    }

    static void AskAgainToReportSaveData(List<Exception> exceptionList, string st)
    {
        ConfirmPopup.Instance.OpenTwoButtonPopup(
            @"\$업로드 불가 시 게임 진행 불가 안내$".Localized(), () =>
            {
                ConfigPopup.Instance.OpenCommunity();
                ProcessCriticalLoadError(exceptionList, st);
            }, () => EnterRecoveryCode(exceptionList, st, false), "\\중대한 오류 발생".Localized(), "\\공식 카페 이동".Localized(),
            "\\복구 코드 입력".Localized());
    }

    static async void UploadSaveFileAsync(List<Exception> exceptionList, string st, bool notCriticalError)
    {
        ConfirmPopup.Instance.Close();
        await ErrorReporter.Instance.UploadSaveFileIncidentAsync(exceptionList, st, notCriticalError);
    }

    static void ProcessNewUser(IBlackContext context, Exception e)
    {
        ConDebug.LogFormat("Load: Save file not found: {0}", e.ToString());
        ResetData(context);
        ConDebug.Log("Your OS language is " + Application.systemLanguage);
        ChangeLanguageBySystemLanguage();
        ShowFirstInstallWelcomePopup();
        ConDebug.Log("loadedAtLeastOnce set to true");
        context.LoadedAtLeastOnce = true;
    }

    static void CloseConfirmPopupAndCheckNoticeSilently()
    {
        ConfirmPopup.Instance.Close();
        NoticeManager.Instance.CheckNoticeSilently();
    }

    static void ShowFirstInstallWelcomePopup()
    {
    }

    static int NewUserPseudoId()
    {
        return BlackRandom.Range(100000000, 1000000000);
    }

    static void ResetData(IBlackContext context)
    {
        context.SetGold(0);
        context.SetGemZero();
        BlackLogManager.Add(BlackLogEntry.Type.GemToZero, 0, 0);
        context.AchievementGathered = new AchievementRecord1(false);
        context.AchievementRedeemed = new AchievementRecord1(false);
        context.UserPseudoId = NewUserPseudoId();
        context.NoticeData = new NoticeData();
        context.LastDailyRewardRedeemedIndex = 0;
        context.LastDailyRewardRedeemedTicks = DateTime.MinValue.Ticks;
        context.LastConsumedServiceIndex = 0;
        context.SaveFileLoaded = true;
        context.LastClearedStageId = 0;
        context.LastClearedStageIdEvent = -1;
        context.StageClearTimeList = new List<ScFloat>();
        context.NextStagePurchased = false;
        context.CoinAmount = 0;
        context.LastFreeCoinRefilledTicks = 0;
        context.SlowMode = false;
        context.CoinUseCount = 0;
        context.LastStageFailed = false;
        context.StashedRewardJsonList = new List<ScString>();
        context.LastDailyRewardRedeemedTicksList = new List<ScLong> {0};
        context.NoAdsCode = 0;

        if (SystemInfo.deviceModel.IndexOf("iPhone", StringComparison.Ordinal) >= 0)
        {
            var screenRatio = 1.0 * Screen.height / (1.0 * Screen.width);
            if (2.1 < screenRatio && screenRatio < 2.2)
                ConfigPopup.Instance.IsNotchOn = true;
            else
                ConfigPopup.Instance.IsNotchOn = false;
        }
        else
        {
            ConfigPopup.Instance.IsNotchOn = false;
        }

        Sound.Instance.BgmAudioSourceActive = true;
        Sound.Instance.SfxAudioSourceActive = true;

        // 아마 상단 노치가 필요한 모델은 하단도 필요하겠지...?
        ConfigPopup.Instance.IsBottomNotchOn = ConfigPopup.Instance.IsNotchOn;

        if (Application.isMobilePlatform == false)
        {
            ConfigPopup.Instance.IsPerformanceModeOn = true;
        }

        BlackLogManager.Add(BlackLogEntry.Type.GameReset, 0, 0);
    }
}