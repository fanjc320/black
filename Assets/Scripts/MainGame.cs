using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using ConditionalDebug;
using UnityEngine;
#if ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainGame : MonoBehaviour
{
    static bool Verbose => true;

    static readonly int ColorTexture = Shader.PropertyToID("ColorTexture");

    public static MainGame Instance;

    [SerializeField]
    GridWorld gridWorld;

    [SerializeField]
    IslandLabelSpawner islandLabelSpawner;

    [SerializeField]
    NameplateGroup nameplateGroup;

    [SerializeField]
    PaletteButtonGroup paletteButtonGroup;

    [SerializeField]
    CanvasGroup achieveGroup;

    [SerializeField]
    PinchZoom pinchZoom;

    StageData stageData;

    [SerializeField]
    StageMetadata stageMetadata;

    [SerializeField]
    TargetImage targetImage;

    [SerializeField]
    Image targetImageOutline;

    [SerializeField]
    GameObject timeGroup;

    [SerializeField]
    Text timeText;

    [SerializeField]
    float remainTime;

    [SerializeField]
    IslandShader3DController islandShader3DController;

    [SerializeField]
    SinglePaletteRenderer singlePaletteRenderer;

    public StageMetadata StageMetadata => stageMetadata;

    public bool CanInteractPanAndZoom => islandLabelSpawner.IsLabelByMinPointEmpty == false;

    async void Start()
    {
        Application.runInBackground = false;

        if (gridWorld == null) return;

        // 如果你是从Lobby神那里过来的，你会满足这个条件的。
        if (StageButton.CurrentStageMetadata != null)
        {
            stageMetadata = StageButton.CurrentStageMetadata;//"047 (StageMetadata)"

            if (Verbose)
            {
                ConDebug.Log($"Stage metadata specified by StageButton: {stageMetadata.name}");
            }
        }

        if (stageMetadata == null)
        {
#if ADDRESSABLES
            while (Data.dataSet == null || Data.dataSet.StageMetadataLocList == null)
            {
                await Task.Yield();
            }
            //StageMetadataLocList[47] "Assets/Stages/001/001.asset"-"Assets/Stages/047/047.asset"
            //stageMetadata = await Addressables.LoadAssetAsync<StageMetadata>(Data.dataSet.StageMetadataLocList[0]).Task;
            ConDebug.Log($"MainGame Start StageMetadataLocList.Count: {Data.dataSet.StageMetadataLocList.Count()}");
            stageMetadata = await Addressables.LoadAssetAsync<StageMetadata>(Data.dataSet.StageMetadataLocList[50]).Task;
#endif
        }
        ConDebug.Log($"MainGame Start Stage metadata specified by StageButton: {stageMetadata.name}");
        using (var stream = new MemoryStream(stageMetadata.RawStageData.bytes))
        {
            var formatter = new BinaryFormatter();
            stageData = (StageData) formatter.Deserialize(stream);
            stream.Close();
        }
//        -islandDataByMinPoint    Count = 6   System.Collections.Generic.Dictionary<uint, IslandData>
//+ [0] "[0, IslandData]"   System.Collections.Generic.KeyValuePair<uint, IslandData>
//+ [1] "[2752530, IslandData]" System.Collections.Generic.KeyValuePair<uint, IslandData>
//+ [2] "[3866689, IslandData]" System.Collections.Generic.KeyValuePair<uint, IslandData>
//+ [3] "[4784241, IslandData]" System.Collections.Generic.KeyValuePair<uint, IslandData>
//+ [4] "[5898252, IslandData]" System.Collections.Generic.KeyValuePair<uint, IslandData>
//+ [5] "[7077943, IslandData]" System.Collections.Generic.KeyValuePair<uint, IslandData>

        stageData.islandCountByColor = stageData.islandDataByMinPoint.GroupBy(g => g.Value.rgba)
            .ToDictionary(g => g.Key, g => g.Count());//islandCountByColor:[0],"[4294901246, 3]";[1],"[4287221774, 1]";[2],"[4290008205, 1]";[3],"[4283189238, 1]"

        if (Verbose)
        {
            ConDebug.Log($"{stageData.islandDataByMinPoint.Count} islands loaded.");
        }

        var maxIslandPixelArea = stageData.islandDataByMinPoint.Max(e => e.Value.pixelArea);
        if (Verbose)
        {
            foreach (var mp in stageData.islandDataByMinPoint)
            {
                ConDebug.Log($"Island: Key={mp.Key} PixelArea={mp.Value.pixelArea}");
            }

            ConDebug.Log($"Max island pixel area: {maxIslandPixelArea}");
        }

        //var skipBlackMaterial = Instantiate(stageMetadata.SkipBlackMaterial);
        //var colorTexture = Instantiate((Texture2D) skipBlackMaterial.GetTexture(ColorTexture));
        //skipBlackMaterial.SetTexture(ColorTexture, colorTexture);

        gridWorld.LoadTexture(stageMetadata.A1Tex, stageData);

        gridWorld.StageName = stageMetadata.name;//"047"
        nameplateGroup.ArtistText = stageMetadata.StageSequenceData.artist;//"MrHup"
        nameplateGroup.TitleText = stageMetadata.StageSequenceData.title;//"인공 도시"
        nameplateGroup.DescText = stageMetadata.StageSequenceData.desc;//"인공지능 기술로 그린 그림. 이제 인간이 설 자리는 어디인가?"

        //targetImage.SetTargetImageMaterial(skipBlackMaterial);

        // 玩家一个一个渲染彩色单元格的组件。
        //屏幕上显示的是所有着色单元格的累积形式。
        //为此使用Render Texture
        islandShader3DController.Initialize(stageMetadata);

        // 用特定颜色绘制玩家所选调色板的所有单元格的元件。
        //让游戏玩得更轻松。
        //但是从第4阶段开始打开。
        singlePaletteRenderer.gameObject.SetActive(stageMetadata.StageIndex >= 3);
        singlePaletteRenderer.Initialize(stageMetadata);

        targetImageOutline.material = stageMetadata.SdfMaterial;//"047-SDF (UnityEngine.Material)"
        // 如果没有SDF Material，干脆不让这个形象出现吧。
        targetImageOutline.enabled = stageMetadata.SdfMaterial != null;

        paletteButtonGroup.CreatePalette(stageData);//创造色彩盘按钮

        islandLabelSpawner.CreateAllLabels(stageData);

        remainTime = stageMetadata.StageSequenceData.remainTime;

        if (stageMetadata.StageSequenceData.remainTime > 0)
        {
            ActivateTime();
        }
        else
        {
            DeactivateTime();
        }

        if (Verbose)
        {
            ConDebug.Log($"Tex size: {gridWorld.TexSize}");
            ConDebug.Log($"CanInteractPanAndZoom = {CanInteractPanAndZoom}");
        }

        gridWorld.ResumeGame();
    }

    public void ResetCamera()
    {
        var targetImageTransform = targetImage.transform;
        targetImageTransform.localPosition = new Vector3(0, 0, targetImageTransform.localPosition.z);
        pinchZoom.ResetZoom();
    }

    public void ResetStage()
    {
        gridWorld.DeleteSaveFileAndReloadScene();
    }

    public void GoToLobby()
    {
        if (gridWorld != null) gridWorld.WriteStageSaveData();

        SceneManager.LoadScene("Lobby");
    }

    public void OnFinishConfirmButton()
    {
        Sound.Instance.PlayButtonClick();

        if (gridWorld != null) gridWorld.WriteStageSaveData();

        if (gridWorld.RewardGoldAmount > 0)
        {
            ConfirmPopup.Instance.Open(@"\클리어를 축하합니다. {0}골드를 받았습니다.".Localized(gridWorld.RewardGoldAmount),
                () => SceneManager.LoadScene("Lobby"));

            Sound.Instance.PlaySoftTada();
        }
        else
        {
            SceneManager.LoadScene("Lobby");
        }
    }

    public void ToggleComboAdminMode()
    {
        BlackContext.Instance.ComboAdminMode = !BlackContext.Instance.ComboAdminMode;
    }

    public void LoadMuseumScene()
    {
        if (gridWorld != null) gridWorld.WriteStageSaveData();

        // ReSharper disable once Unity.LoadSceneUnknownSceneName
        SceneManager.LoadScene("Museum");
    }

    // ReSharper disable once UnusedMember.Global
    public void AchievePopup(bool show)
    {
        if (show) achieveGroup.Show();
        else achieveGroup.Hide();
    }

    void Update()
    {
        if (timeGroup.gameObject.activeInHierarchy)
        {
            remainTime = Mathf.Max(0, remainTime - Time.deltaTime);
            GetMinutesSeconds(TimeSpan.FromSeconds(remainTime), out var minutes, out var seconds);
            timeText.text = @"\남은 시간".Localized() + "\n" + $"{minutes:00}:{seconds:00}";

            if (remainTime <= 0)
            {
                DeactivateTime();
                gridWorld.DeleteSaveFile();
                ConfirmPopup.Instance.Open("제한 시간이 지났습니다. 처음부터 다시 시작해야합니다.",
                    () => SceneManager.LoadScene("Lobby"));
            }
        }
    }

    static void GetMinutesSeconds(TimeSpan totalElapsedTimeSpan, out int minutes, out int seconds)
    {
        int.TryParse(totalElapsedTimeSpan.ToString("%m"), out minutes);
        int.TryParse(totalElapsedTimeSpan.ToString("%s"), out seconds);
    }

    public void DeactivateTime()
    {
        timeGroup.gameObject.SetActive(false);
    }

    void ActivateTime()
    {
        timeGroup.gameObject.SetActive(true);
    }

    public void SetRemainTime(float f)
    {
        remainTime = f;
    }

    public float GetRemainTime()
    {
        return remainTime;
    }

    public void OpenResetStageConfirmPopup()
    {
        ConfirmPopup.Instance.OpenYesNoPopup(@"\이 스테이지를 처음부터 새로 시작하겠습니까?".Localized(),
            ResetStage,
            ConfirmPopup.Instance.Close);
    }

    public void OnPaletteChange(int paletteButtonIndex)
    {
        if (Verbose)
        {
            ConDebug.Log($"Palette Change Notification {paletteButtonIndex}");
        }

        // 셰이더 상 팔레트 인덱스는 0번째가 외곽선 용이다. 하나 더해서 넘겨줘야한다.
        singlePaletteRenderer.SetPaletteIndex(paletteButtonIndex + 1);
    }
}