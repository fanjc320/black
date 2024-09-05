using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ToastMessage))]
public class ToastMessageTester : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Good"))
        {
            ToastMessage.Instance.PlayGoodAnim("成就：10组合");
        }
        else if (GUILayout.Button("Warning"))
        {
            ToastMessage.Instance.PlayWarnAnim("请重新确认颜色。");
        }
    }
}