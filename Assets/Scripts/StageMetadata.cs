using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class StageMetadata : ScriptableObject
{
    [SerializeField]
    TextAsset rawStageData;

    [SerializeField]
    Material sdfMaterial;

    [SerializeField]
    Texture2D a1Tex;

    [SerializeField]
    Texture2D a2Tex;

    // 轮廓填充的最大颜色（黑色）。随着数字的增加，即使是模糊的黑色也会被视为完全黑色。
    [SerializeField]
    int outlineThreshold = 30;

    public int StageIndex { get; set; }

    public Material SdfMaterial => sdfMaterial;
    public TextAsset RawStageData => rawStageData;
    public Texture2D A1Tex => a1Tex;
    public Texture2D A2Tex => a2Tex;
    public int OutlineThreshold => outlineThreshold;

    public StageSequenceData StageSequenceData => Data.dataSet.StageSequenceData[StageIndex];

    // [NonSerialized] 如果不附加该属性，cachedStageData 可能不为 null 并且内容可能为空...
    // 왜지...?
    [NonSerialized]
    StageData cachedStageData;

    public StageData StageData => cachedStageData ??= LoadStageData();

    StageData LoadStageData()
    {
        using var stream = new MemoryStream(RawStageData.bytes);
        var formatter = new BinaryFormatter();
        var stageData = (StageData) formatter.Deserialize(stream);
        stream.Close();
        return stageData;
    }

#if UNITY_EDITOR
    public static StageMetadata Create()
    {
        return CreateInstance<StageMetadata>();
    }

    public static void SetValues(StageMetadata asset, Material sdfMat, TextAsset stageData,
        Texture2D a1Tex, Texture2D a2Tex)
    {
        asset.sdfMaterial = sdfMat;
        asset.rawStageData = stageData;
        asset.a1Tex = a1Tex;
        asset.a2Tex = a2Tex;
    }
#endif
}