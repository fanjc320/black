using System.IO;
using UnityEditor;
using UnityEngine;

public static class KoreanUtil
{
    [MenuItem("Assets/Black/修复字素分离问题")]
    static void FixFileKoreanFileNamesMultipleSelection()
    {
        foreach (var o in Selection.objects)
        {
            NormalizeFileName(o);
            EditorUtility.SetDirty(o);
        }

        AssetDatabase.Refresh();
    }

    static void NormalizeFileName(UnityEngine.Object obj)
    {
        var assetPath = AssetDatabase.GetAssetPath(obj);
        var assetPathNormalized = assetPath.Normalize();

        if (string.CompareOrdinal(assetPath, assetPathNormalized) != 0)
        {
            Debug.Log($"Normalizing graph file '{assetPath}' to '{assetPathNormalized}'...");

            // AssetDatabase.RenameAsset()它不是通过以下方式解决的   
            File.Move(assetPath, assetPathNormalized);
        }
    }
}
