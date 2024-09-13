using System.Collections.Generic;
using System.Linq;
using ConditionalDebug;
using UnityEngine;

public class IslandLabelSpawner : MonoBehaviour
{
    static bool Verbose => false;
    
    readonly Dictionary<uint, IslandLabel> labelByMinPoint = new Dictionary<uint, IslandLabel>();

    [SerializeField]
    GridWorld gridWorld;

    [SerializeField]
    Transform islandLabelNumberGroup;

    [SerializeField]
    GameObject islandLabelNumberPrefab;

    [SerializeField]
    PaletteButtonGroup paletteButtonGroup;

    [SerializeField]
    RectTransform rt;

    public bool IsLabelByMinPointEmpty => labelByMinPoint.Count == 0;

#if UNITY_EDITOR
    void OnValidate()
    {
        rt = GetComponent<RectTransform>();
    }
#endif

    RectInt GetRectRange(ulong maxRectUlong)
    {
        var xMin = (int) (maxRectUlong & 0xffff);
        var yMax = gridWorld.TexSize - (int) ((maxRectUlong >> 16) & 0xffff);
        var xMax = (int) ((maxRectUlong >> 32) & 0xffff);
        var yMin = gridWorld.TexSize - (int) ((maxRectUlong >> 48) & 0xffff);
        var r = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        if (Verbose)
        {
            ConDebug.Log($"{r.xMin},{r.yMin} -- {r.xMax},{r.yMax} (area={r.size.x * r.size.y})");
        }
        return r;
    }

    public void CreateAllLabels(StageData stageData)
    {//maxRectDict[50]  "[9764864, (x:0, y:1031, width:904, height:320)]" - ...
        var maxRectDict = stageData.islandDataByMinPoint.ToDictionary(e => e.Key, e => GetRectRange(e.Value.maxRect));

        var rectIndex = 0;
        var subgroupCapacity = 50;
        GameObject islandLabelNumberSubgroup = null;
//        -maxRectDict Count = 6   System.Collections.Generic.Dictionary<uint, UnityEngine.RectInt>
//+ [0] "[0, (x:0, y:125, width:150, height:25)]"   System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>
//+ [1] "[2752530, (x:20, y:98, width:50, height:15)]"  System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>
//+ [2] "[3866689, (x:66, y:71, width:12, height:19)]"  System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>
//+ [3] "[4784241, (x:114, y:69, width:16, height:9)]"  System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>
//+ [4] "[5898252, (x:20, y:37, width:33, height:55)]"  System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>
//+ [5] "[7077943, (x:57, y:37, width:34, height:19)]"  System.Collections.Generic.KeyValuePair<uint, UnityEngine.RectInt>

        foreach (var kv in maxRectDict)
        {
            if (Verbose)
                ConDebug.Log(
                    $"Big sub rect island: ({kv.Value.xMin},{kv.Value.yMin})-({kv.Value.xMax},{kv.Value.yMax}) area={kv.Value.size.x * kv.Value.size.y}");

            if (rectIndex % subgroupCapacity == 0)
            {
                islandLabelNumberSubgroup =
                    new GameObject($"Island Label Subgroup ({rectIndex:d4}-{rectIndex + subgroupCapacity - 1:d4})");//"Island Label Subgroup (0000-0049) (UnityEngine.GameObject)"
                islandLabelNumberSubgroup.transform.parent = islandLabelNumberGroup;
                var subGroupRt = islandLabelNumberSubgroup.AddComponent<RectTransform>();//"Island Label Subgroup (0000-0049) (UnityEngine.RectTransform)"
                subGroupRt.anchoredPosition3D = Vector3.zero;
                subGroupRt.localScale = Vector3.one;
            }

            if (islandLabelNumberSubgroup == null)
            {
                Debug.LogError($"Logic error. {nameof(islandLabelNumberSubgroup)} should not be null at this point.");
                continue;
            }
            //"Island Label(Clone) (IslandLabel)"
            var label = Instantiate(islandLabelNumberPrefab, islandLabelNumberSubgroup.transform)
                .GetComponent<IslandLabel>();
            var labelRt = label.Rt;//"Island Label(Clone) (UnityEngine.RectTransform)"
            var texSizeFloat = (float) gridWorld.TexSize;//1500
            var delta = rt.sizeDelta;
            var anchoredPosition = kv.Value.center / texSizeFloat * delta - delta / 2;//!!!!!!!!!!!
            labelRt.anchoredPosition = anchoredPosition;//////
            var sizeDelta = (Vector2) kv.Value.size / texSizeFloat * delta;
            labelRt.sizeDelta = sizeDelta;
            var paletteIndex = paletteButtonGroup.GetPaletteIndexByColor(stageData.islandDataByMinPoint[kv.Key].rgba);
            label.Text = (paletteIndex + 1).ToString();
            labelByMinPoint[kv.Key] = label;//"Island Label 0000 #15 (IslandLabel)"
            label.name = $"Island Label {rectIndex:d4} #{paletteIndex + 1:d2}";//"Island Label 0000 #15"
            rectIndex++;
        }
    }

    public bool ContainsMinPoint(uint minPointUint)
    {
        return labelByMinPoint.ContainsKey(minPointUint);
    }

    public void DestroyLabelByMinPoint(uint minPointUint)
    {
        if (labelByMinPoint.TryGetValue(minPointUint, out var label))
        {
            Destroy(label.gameObject);
            labelByMinPoint.Remove(minPointUint);
        }
        else
        {
            Debug.LogError($"DestroyLabelByMinPoint: could not find minPointUint {minPointUint}!");
        }
    }

    public void SetLabelBackgroundImageActive(bool b)
    {
        foreach (var kv in labelByMinPoint) kv.Value.BackgroundImageActive = b;
    }
}