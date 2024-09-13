using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;

public class IslandShader3DController : MonoBehaviour
{
    [SerializeField]
    bool fullRender;

    [SerializeField]
    TargetImageQuadCamera targetImageQuadCamera;

    [SerializeField]
    MeshRenderer targetMeshRenderer;
    
    [SerializeField]
    RawImage targetRawImage;

    StageData stageData;
    
    Material targetMaterial;
    //https://docs.unity.cn/cn/2019.4/ScriptReference/Shader.PropertyToID.html
    //着色器属性的每个名称（例如 _MainTex 或 _Color）均分配有唯一 整数，在整个游戏中，该整数均保持相同
    static readonly int A1Tex = Shader.PropertyToID("_A1Tex");
    static readonly int A2Tex = Shader.PropertyToID("_A2Tex");
    static readonly int Palette = Shader.PropertyToID("_Palette");
    static readonly int IslandIndex = Shader.PropertyToID("_IslandIndex");
    static readonly int FullRender = Shader.PropertyToID("_FullRender");
    static readonly int PaletteTex = Shader.PropertyToID("_PaletteTex");

    public void Initialize(StageMetadata stageMetadata)
    {
        // Material 在实例运行时创建克隆。

        if (targetMeshRenderer != null)
        {
            targetMeshRenderer.material = Instantiate(targetMeshRenderer.material);//"Single Island Material (Instance)(Clone) (Instance) (UnityEngine.Material)"
            targetMaterial = targetMeshRenderer.material;
        }
        
        if (targetRawImage != null)
        {
            targetRawImage.material = Instantiate(targetRawImage.material);
            targetMaterial = targetRawImage.material;
        }

        if (targetMaterial == null)
        {
            Debug.LogError("Target material null");
            return;
        }

        var a1Tex = stageMetadata.A1Tex;//"051-OTB-FSNB-DIT-A1 (UnityEngine.Texture2D)"
        var a2Tex = stageMetadata.A2Tex;//"051-OTB-FSNB-DIT-A2 (UnityEngine.Texture2D)"

        targetMaterial.SetTexture(A1Tex, a1Tex);
        targetMaterial.SetTexture(A2Tex, a2Tex);

        using var stream = new MemoryStream(stageMetadata.RawStageData.bytes);
        var formatter = new BinaryFormatter();
        stageData = (StageData) formatter.Deserialize(stream);
        stream.Close();
        //-colorUintArray  uint[5] uint[]
        //[0] 4278190080  uint
        //[1] 4283189238  uint
        //[2] 4287221774  uint
        //[3] 4290008205  uint
        //[4] 4294901246  uint

        var colorUintArray =
            new[] {BlackConvert.GetC(new Color32(0, 0, 0, 255)) } // 调色板中的第 0 个始终为黑色，仅用于轮廓。
                .Concat(stageData.CreateColorUintArray())
                .ToArray();

        if (colorUintArray.Length > 64)
        {
            Debug.LogError("Maximum palette size 64 exceeded.");
        }
        else
        {
//            -paletteArray    UnityEngine.Color[5]    UnityEngine.Color[]
//+[0] "RGBA(0.000, 0.000, 0.000, 1.000)"  UnityEngine.Color
//+ [1] "RGBA(0.965, 0.278, 0.298, 1.000)"  UnityEngine.Color
//+ [2] "RGBA(0.055, 0.816, 0.537, 1.000)"  UnityEngine.Color
//+ [3] "RGBA(0.553, 0.329, 0.706, 1.000)"  UnityEngine.Color
//+ [4] "RGBA(0.996, 0.992, 0.996, 1.000)"  UnityEngine.Color

            var paletteArray = colorUintArray.Select(BlackConvert.GetColor).ToArray();
            targetMaterial.SetColorArray(Palette, paletteArray);

            var paletteTex = new Texture2D(64, 1, TextureFormat.RGBA32, false);
            Color[] paddedPaletteArray;
            if (paletteArray.Length < 64)
            {
                paddedPaletteArray = paletteArray.Concat(Enumerable.Repeat(Color.black, 64 - paletteArray.Length))
                    .ToArray();
            }
            else
            {
                paddedPaletteArray = paletteArray;
            }

            paletteTex.SetPixels(paddedPaletteArray);
            paletteTex.filterMode = FilterMode.Point;
            paletteTex.wrapMode = TextureWrapMode.Clamp;
            paletteTex.Apply();

            targetMaterial.SetTexture(PaletteTex, paletteTex);//!!!!!!!!!!!!
        }

        ClearAndEnqueueIslandIndex(0);

        targetMaterial.SetFloat(FullRender, fullRender ? 1 : 0);

        if (targetImageQuadCamera != null)
        {
            targetImageQuadCamera.ClearCameraOnce();
        }
    }

    public void SetIslandIndex(int islandIndex)
    {
        targetMaterial.SetInt(IslandIndex, islandIndex);//"Single Island Material (Instance)(Clone) (Instance) (UnityEngine.Material)"
        if (targetImageQuadCamera)
        {
            targetImageQuadCamera.RenderOneFrame();
        }
    }

    void ClearAndEnqueueIslandIndex(int islandIndex)
    {
        if (targetImageQuadCamera != null)
        {
            targetImageQuadCamera.ClearQueue();
            targetImageQuadCamera.EnqueueIslandIndex(islandIndex);
        }
    }

    public void EnqueueIslandIndex(int islandIndex)
    {
        if (targetImageQuadCamera != null)
        {
            targetImageQuadCamera.EnqueueIslandIndex(islandIndex);
        }
    }
}