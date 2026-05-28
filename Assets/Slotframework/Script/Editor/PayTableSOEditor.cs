using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Slot.Common;

[CustomEditor(typeof(PayTableSO))]
public class PayTableSOEditor : Editor
{
    private string _symbolOddsInputData = "";
    private Vector2 _symbolOddsScrollPos;

    public override void OnInspectorGUI()
    {
        // 更新 SerializedObject
        serializedObject.Update();

        // 繪製 LineBet 和 PayLine
        DrawBasicSetting();

        GUILayout.Space(10);

        // 繪製現有的 SymbolOdds
        DrawExistingSymbolOdds();

        GUILayout.Space(20);

        // 繪製新增 SymbolOdds 的輸入區域
        DrawAddSymbolOddsArea();

        // 應用修改
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicSetting()
    {
        DrawSectionHeader("=== 基本設定 ===", new Color(0.2f, 0.6f, 0.86f, 1f));

        SerializedProperty lineBetProp = serializedObject.FindProperty("LineBet");
        SerializedProperty payLineProp = serializedObject.FindProperty("PayLine");
        SerializedProperty betRatiosProp = serializedObject.FindProperty("BetRatios");
        SerializedProperty defaultModeProp = serializedObject.FindProperty("DefaultDisplayMode");
        SerializedProperty defaultEntriesProp = serializedObject.FindProperty("DefaultDisplayEntries");

        EditorGUILayout.PropertyField(lineBetProp, new GUIContent("LineBet"));
        EditorGUILayout.PropertyField(payLineProp, new GUIContent("PayLine"));
        EditorGUILayout.PropertyField(betRatiosProp, new GUIContent("BetRatios"));

        GUILayout.Space(5);
        EditorGUILayout.PropertyField(defaultModeProp, new GUIContent("Default Display Mode"));
        EditorGUILayout.PropertyField(defaultEntriesProp, new GUIContent("Default Display Entries"), true);
    }

    private void DrawExistingSymbolOdds()
    {
        DrawSectionHeader("=== 現有的 SymbolOdds 資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));

        SerializedProperty symbolOddsProp = serializedObject.FindProperty("SymbolOdds");

        if (symbolOddsProp.arraySize > 0)
        {
            for (int i = 0; i < symbolOddsProp.arraySize; i++)
            {
                SerializedProperty symbolOddsElement = symbolOddsProp.GetArrayElementAtIndex(i);
                SerializedProperty oddsProp = symbolOddsElement.FindPropertyRelative("Odds");
                SerializedProperty displayEntriesProp = symbolOddsElement.FindPropertyRelative("DisplayEntries");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Symbol {i}", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(oddsProp, new GUIContent("Odds"), true);
                EditorGUILayout.PropertyField(displayEntriesProp, new GUIContent("Display Entries (Override)"), true);

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("尚無資料", MessageType.Info);
        }
    }

    private void DrawAddSymbolOddsArea()
    {
        DrawSectionHeader("=== 新增 SymbolOdds 資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));

        // 資料輸入區域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("輸入資料範例：", EditorStyles.label);
        EditorGUILayout.HelpBox("50\t100\t150\t200\n40\t80\t120\t160\n...", MessageType.Info);
        GUILayout.Space(5);
        _symbolOddsScrollPos = EditorGUILayout.BeginScrollView(_symbolOddsScrollPos, GUILayout.Height(150));
        _symbolOddsInputData = EditorGUILayout.TextArea(_symbolOddsInputData, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 按鈕區域
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增資料", GUILayout.Height(40), GUILayout.ExpandWidth(true)))
        {
            ParseAndAddSymbolOddsData((PayTableSO)target, _symbolOddsInputData);
            _symbolOddsInputData = ""; // 清空輸入框
        }

        GUILayout.Space(10);

        if (GUILayout.Button("清除資料", GUILayout.Height(40), GUILayout.Width(150)))
        {
            if (EditorUtility.DisplayDialog("確認清除", "確定要清除所有 SymbolOdds 資料嗎？", "是", "否"))
            {
                SerializedProperty symbolOddsProp = serializedObject.FindProperty("SymbolOdds");
                symbolOddsProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ParseAndAddSymbolOddsData(PayTableSO payTableSO, string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            EditorUtility.DisplayDialog("錯誤", "輸入資料為空。請輸入有效的資料。", "確定");
            return;
        }

        string[] lines = data.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        List<SymbolOdds> newSymbolOddsList = new List<SymbolOdds>();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            string[] tokens = trimmedLine.Split(new[] { '\t', ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);

            List<int> odds = new List<int>();

            foreach (string token in tokens)
            {
                if (int.TryParse(token, out int odd))
                {
                    odds.Add(odd);
                }
                else
                {
                    EditorUtility.DisplayDialog("錯誤", $"解析時出錯，無法將 '{token}' 轉換為整數。", "確定");
                    return;
                }
            }

            SymbolOdds symbolOdds = new SymbolOdds
            {
                Odds = odds
            };

            newSymbolOddsList.Add(symbolOdds);
        }

        // 記錄對象的更改以支持 Undo 功能
        Undo.RecordObject(payTableSO, "新增 SymbolOdds 資料");

        // 添加到現有的 SymbolOdds 列表中
        payTableSO.SymbolOdds.AddRange(newSymbolOddsList);

        // 標記對象已更改，確保修改被保存
        EditorUtility.SetDirty(payTableSO);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("成功", "SymbolOdds 資料已成功新增。", "確定");
    }

    /// <summary>
    /// 繪製帶有彩色背景的區域標題
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="color">背景顏色</param>
    private void DrawSectionHeader(string text, Color color)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            normal = { textColor = Color.white },
            padding = new RectOffset(10, 10, 5, 5)
        };

        // 獲取一個控制矩形
        Rect rect = EditorGUILayout.GetControlRect(false, 25);
        // 繪製背景
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), color);
        // 繪製文字
        EditorGUI.LabelField(rect, text, style);
    }
}
