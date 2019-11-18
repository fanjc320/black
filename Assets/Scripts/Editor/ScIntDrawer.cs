﻿using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ScInt))]
public class ScIntDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);

        // Draw label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Don't make child fields be indented
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Calculate rects
        var valueRect = new Rect(position.x, position.y, position.width, position.height);
        property.FindPropertyRelative("value").intValue = EditorGUI.IntField(valueRect, property.FindPropertyRelative("value").intValue ^ ScInt.k) ^ ScInt.k;
        
        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }
}