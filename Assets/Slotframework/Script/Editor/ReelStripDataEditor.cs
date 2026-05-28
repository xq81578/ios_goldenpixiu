using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Linq;

[CustomEditor(typeof(ReelStripGroupSO))]
public class ReelStripGroupSOEditor : Editor
{
    private int _selectedTab = 0;
    private string _reelStripInputData = "";
    private Vector2 _reelStripScrollPos;

    private string _combinationInputData = "";
    private Vector2 _combinationScrollPos;

    // 用於管理每個 Group 和 Combination 的摺疊狀態
    private Dictionary<int, bool> _groupFoldouts = new();
    private Dictionary<int, bool> _combinationFoldouts = new();

    private SerializedProperty reelStripGroupProp;
    private SerializedProperty reelStripCombinationProp;

    private void OnEnable()
    {
        // 獲取 ReelStrip 和 ReelStripCombinations 屬性
        reelStripGroupProp = serializedObject.FindProperty("ReelStripGroups");
        reelStripCombinationProp = serializedObject.FindProperty("ReelStripCombinations");

        // 初始化每個 Group 的摺疊狀態
        ReelStripGroupSO reelStripData = (ReelStripGroupSO)target;
        for (int i = 0; i < reelStripData.ReelStripGroups.Count; i++)
        {
            if (!_groupFoldouts.ContainsKey(i))
            {
                _groupFoldouts[i] = false;
            }
        }

        // 初始化每個 Combination 的摺疊狀態
        for (int i = 0; i < reelStripData.ReelStripCombinations.Count; i++)
        {
            if (!_combinationFoldouts.ContainsKey(i))
            {
                _combinationFoldouts[i] = false;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        SerializedProperty maxCombIndexProp = serializedObject.FindProperty("MaxCombIndex");
        EditorGUILayout.PropertyField(maxCombIndexProp, new GUIContent("MaxCombIndex"));
        // 添加選項卡
        _selectedTab = GUILayout.Toolbar(_selectedTab, new string[] { "轉輪帶", "轉輪帶組合" });

        // 更新 SerializedObject
        serializedObject.Update();

        if (_selectedTab == 0)
        {
            // 轉輪帶的介面
            DrawReelStripGUI();
        }
        else if (_selectedTab == 1)
        {
            // 轉輪帶組合的介面
            DrawReelStripCombinationGUI();
        }

        // 應用修改
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawReelStripGUI()
    {
        GUILayout.Space(10);
        DrawSectionHeader("=== 現有轉輪帶資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));
        GUILayout.Space(5);

        if (reelStripGroupProp.arraySize > 0)
        {
            for (int i = 0; i < reelStripGroupProp.arraySize; i++)
            {
                SerializedProperty groupProp = reelStripGroupProp.GetArrayElementAtIndex(i);
                SerializedProperty reelsProp = groupProp.FindPropertyRelative("ReelStrips");

                string groupLabel = $"Group {i}";

                // 初始化 Group 的摺疊狀態
                if (!_groupFoldouts.ContainsKey(i))
                {
                    _groupFoldouts[i] = false;
                }

                // 創建水平佈局，包含 Group 標籤和展開/摺疊按鈕
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(groupLabel, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                // 按鈕顯示為 "展開" 或 "摺疊" 根據當前狀態
                string buttonLabel = _groupFoldouts[i] ? "摺疊" : "展開";
                if (GUILayout.Button(buttonLabel, GUILayout.Width(60)))
                {
                    _groupFoldouts[i] = !_groupFoldouts[i];

                    // 當展開 Group 時，更新 reelsProp 的展開狀態
                    for (int j = 0; j < reelsProp.arraySize; j++)
                    {
                        SerializedProperty reelProp = reelsProp.GetArrayElementAtIndex(j);
                        reelProp.isExpanded = _groupFoldouts[i];
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 如果 Group 被展開，顯示其 Reels 及其 Elements
                if (_groupFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    if (reelsProp.arraySize > 0)
                    {
                        for (int j = 0; j < reelsProp.arraySize; j++)
                        {
                            SerializedProperty reelProp = reelsProp.GetArrayElementAtIndex(j);
                            SerializedProperty elementsProp = reelProp.FindPropertyRelative("Symbols");

                            // 使用 PropertyField 顯示 Reel，保持原始 Inspector 樣貌
                            EditorGUILayout.PropertyField(reelProp, new GUIContent($"ReelStrip {j}"), true);
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("該 Group 尚無 Reels", MessageType.Info);
                    }
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("尚無資料", MessageType.Info);
        }

        GUILayout.Space(20);
        DrawSectionHeader("=== 新增轉輪帶資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));
        GUILayout.Space(5);

        // 資料輸入區域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("輸入資料範例：", EditorStyles.label);
        EditorGUILayout.HelpBox("Group0\n{A, B, C}, {D, E, F}, {G, H, I},\n\nGroup1\n{J, K, L}, {M, N, O}, {P, Q, R},", MessageType.Info);
        GUILayout.Space(5);
        _reelStripScrollPos = EditorGUILayout.BeginScrollView(_reelStripScrollPos, GUILayout.Height(150));
        _reelStripInputData = EditorGUILayout.TextArea(_reelStripInputData, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 按鈕區域
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增資料", GUILayout.Height(40), GUILayout.ExpandWidth(true)))
        {
            ParseAndAddData((ReelStripGroupSO)target, _reelStripInputData);
            _reelStripInputData = ""; // 清空輸入框
        }

        GUILayout.Space(10);

        if (GUILayout.Button("清除資料", GUILayout.Height(40), GUILayout.Width(150)))
        {
            if (EditorUtility.DisplayDialog("確認清除", "確定要清除所有資料嗎？", "是", "否"))
            {
                reelStripGroupProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawReelStripCombinationGUI()
    {
        GUILayout.Space(10);
        DrawSectionHeader("=== 現有轉輪帶組合資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));
        GUILayout.Space(5);

        if (reelStripCombinationProp.arraySize > 0)
        {
            for (int i = 0; i < reelStripCombinationProp.arraySize; i++)
            {
                SerializedProperty combinationProp = reelStripCombinationProp.GetArrayElementAtIndex(i);
                SerializedProperty groupIndicesProp = combinationProp.FindPropertyRelative("ReelStripGroup");

                string combinationLabel = $"Combination {i}";

                // 初始化 Combination 的摺疊狀態
                if (!_combinationFoldouts.ContainsKey(i))
                {
                    _combinationFoldouts[i] = false;
                }

                // 創建水平佈局，包含 Combination 標籤和展開/摺疊按鈕
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(combinationLabel, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                // 按鈕顯示為 "展開" 或 "摺疊" 根據當前狀態
                string buttonLabel = _combinationFoldouts[i] ? "摺疊" : "展開";
                if (GUILayout.Button(buttonLabel, GUILayout.Width(60)))
                {
                    _combinationFoldouts[i] = !_combinationFoldouts[i];

                    // 當展開時，更新組合屬性的展開狀態
                    combinationProp.isExpanded = _combinationFoldouts[i];
                }
                EditorGUILayout.EndHorizontal();

                // 如果 Combination 被展開，顯示其 ReelStripGroup 列表
                if (_combinationFoldouts[i])
                {
                    EditorGUI.indentLevel++;
                    // 使用 PropertyField 顯示組合的詳細資訊
                    EditorGUILayout.PropertyField(groupIndicesProp, new GUIContent("Group 索引"), true);
                    EditorGUI.indentLevel--;
                }

                GUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("尚無資料", MessageType.Info);
        }

        GUILayout.Space(20);
        DrawSectionHeader("=== 新增轉輪帶組合資料 ===", new Color(0.2f, 0.6f, 0.86f, 1f));
        GUILayout.Space(5);

        // 資料輸入區域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("輸入資料範例：", EditorStyles.label);
        EditorGUILayout.HelpBox("0\t0\t0\t0\t0\t0\t0\t0\n1\t4\t1\t1\t1\t1\t1\t1\n...", MessageType.Info);
        GUILayout.Space(5);
        _combinationScrollPos = EditorGUILayout.BeginScrollView(_combinationScrollPos, GUILayout.Height(150));
        _combinationInputData = EditorGUILayout.TextArea(_combinationInputData, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 按鈕區域
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新增組合", GUILayout.Height(40), GUILayout.ExpandWidth(true)))
        {
            ParseAndAddCombinationData((ReelStripGroupSO)target, _combinationInputData);
            _combinationInputData = ""; // 清空輸入框
        }

        GUILayout.Space(10);

        if (GUILayout.Button("清除組合", GUILayout.Height(40), GUILayout.Width(150)))
        {
            if (EditorUtility.DisplayDialog("確認清除", "確定要清除所有組合資料嗎？", "是", "否"))
            {
                reelStripCombinationProp.ClearArray();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 繪製帶有彩色背景的區域標題
    /// </summary>
    /// <param name="text">標題文字</param>
    /// <param name="color">背景顏色</param>
    private void DrawSectionHeader(string text, Color color)
    {
        GUIStyle style = new(EditorStyles.boldLabel)
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

    /// <summary>
    /// 解析輸入資料並新增到 ReelStrip 中
    /// </summary>
    /// <param name="reelStripData">ReelStripGroupSO 對象</param>
    /// <param name="data">輸入的資料字串</param>
    private void ParseAndAddData(ReelStripGroupSO reelStripData, string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            EditorUtility.DisplayDialog("錯誤", "輸入資料為空。請輸入有效的資料。", "確定");
            return;
        }

        DateTime startTime = DateTime.Now;

        // 移除資料中的控制字元，避免影響解析
        data = data.Replace("\r", "");

        // 編譯正則表達式，提升匹配速度
        Regex groupPattern = new(@"Group\s*(\d+)\s*((?:\{[^}]+\}\s*,?\s*)+)", RegexOptions.Compiled);
        Regex arrayPattern = new(@"\{([^}]+)\}", RegexOptions.Compiled);

        MatchCollection matches = groupPattern.Matches(data);

        if (matches.Count == 0)
        {
            EditorUtility.DisplayDialog("錯誤", "未能解析輸入的資料。請確保格式正確。", "確定");
            return;
        }

        // 用於摘要的資料結構
        Dictionary<int, GroupSummary> addedGroups = new();
        // 臨時存儲解析後的資料
        Dictionary<int, List<ReelStrip>> parsedGroups = new();

        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out int groupIndex))
            {
                // 無法解析 group index，跳過
                continue;
            }

            string groupName = $"Group{groupIndex}";
            string groupContent = match.Groups[2].Value;

            // 使用正則表達式找出所有的 { ... }
            MatchCollection arrayMatches = arrayPattern.Matches(groupContent);

            List<ReelStrip> reelStripList = new();
            List<int> elementsPerReel = new();

            foreach (Match arrayMatch in arrayMatches)
            {
                string arrayContent = arrayMatch.Groups[1].Value;

                // 將元素分割並去除空白，同時排除空元素
                string[] elements = arrayContent.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(e => e.Trim())
                                                .Where(e => !string.IsNullOrEmpty(e))
                                                .ToArray();

                if (elements.Length > 0)
                {
                    ReelStrip newReel = new()
                    {
                        Symbols = new List<string>(elements)
                    };
                    reelStripList.Add(newReel);
                    elementsPerReel.Add(elements.Length);
                }
            }

            if (reelStripList.Count > 0)
            {
                parsedGroups[groupIndex] = reelStripList;

                // 添加到摘要
                if (!addedGroups.ContainsKey(groupIndex))
                {
                    addedGroups[groupIndex] = new GroupSummary(groupName);
                }

                GroupSummary summary = addedGroups[groupIndex];
                summary.NumberOfReels += reelStripList.Count;
                summary.ElementsPerReel.AddRange(elementsPerReel);
            }
        }

        // 記錄對象的更改以支持 Undo 功能
        Undo.RecordObject(reelStripData, "Parse and Add Data");

        // 更新或新增 Group
        foreach (var kvp in parsedGroups)
        {
            int groupIndex = kvp.Key;
            List<ReelStrip> reelStripList = kvp.Value;

            if (groupIndex < reelStripData.ReelStripGroups.Count)
            {
                // 更新現有的 Group
                reelStripData.ReelStripGroups[groupIndex].ReelStrips = reelStripList;
            }
            else
            {
                // 添加新的 Group，填充空白
                while (reelStripData.ReelStripGroups.Count < groupIndex)
                {
                    reelStripData.ReelStripGroups.Add(new ReelStripGroup());
                }
                reelStripData.ReelStripGroups.Add(new ReelStripGroup() { ReelStrips = reelStripList });

                // 初始化 Group 的摺疊狀態
                if (!_groupFoldouts.ContainsKey(groupIndex))
                {
                    _groupFoldouts[groupIndex] = false;
                }
            }
        }

        // 應用修改
        serializedObject.ApplyModifiedProperties();
        // 標記對象已更改，確保修改被保存
        EditorUtility.SetDirty(reelStripData);
        AssetDatabase.SaveAssets();

        // 打印摘要
        Debug.Log("=== 新增的群組摘要 ===");
        foreach (var kvp in addedGroups)
        {
            Debug.Log($"{kvp.Value.GroupName}:");
            Debug.Log($"  軸數: {kvp.Value.NumberOfReels}");
            for (int i = 0; i < kvp.Value.ElementsPerReel.Count; i++)
            {
                Debug.Log($"    Reel {i}: {kvp.Value.ElementsPerReel[i]} 個元素");
            }
            Debug.Log("===============");
        }

        TimeSpan span = DateTime.Now - startTime;
        // 顯示成功對話框
        EditorUtility.DisplayDialog("成功", $"資料已成功新增。耗時 {span.TotalSeconds:F2} 秒", "確定");
    }

    /// <summary>
    /// 解析輸入資料並新增到 ReelStripCombination 中
    /// </summary>
    /// <param name="reelStripData">ReelStripGroupSO 對象</param>
    /// <param name="data">輸入的資料字串</param>
    private void ParseAndAddCombinationData(ReelStripGroupSO reelStripData, string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            EditorUtility.DisplayDialog("錯誤", "輸入資料為空。請輸入有效的資料。", "確定");
            return;
        }

        DateTime startTime = DateTime.Now;

        string[] lines = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        Dictionary<int, ReelStripCombination> newCombinations = new();

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            string[] tokens = trimmedLine.Split(new[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 1)
            {
                // 至少需要一個 group index
                continue;
            }

            if (!int.TryParse(tokens[0], out int combIndex))
            {
                continue;
            }

            List<int> groupIndices = new();
            bool parseSuccess = true;
            for (int i = 1; i < tokens.Length; i++)
            {
                if (int.TryParse(tokens[i], out int groupIndex))
                {
                    groupIndices.Add(groupIndex);
                }
                else
                {
                    parseSuccess = false;
                    break;
                }
            }

            if (parseSuccess)
            {
                ReelStripCombination newCombination = new()
                {
                    ReelStripGroup = groupIndices
                };
                newCombinations[combIndex] = newCombination;

                // 初始化 Combination 的摺疊狀態
                if (!_combinationFoldouts.ContainsKey(combIndex))
                {
                    _combinationFoldouts[combIndex] = false;
                }
            }
        }

        if (newCombinations.Count == 0)
        {
            EditorUtility.DisplayDialog("錯誤", "輸入的資料中沒有找到有效的組合。", "確定");
            return;
        }

        // 記錄對象的更改以支持 Undo 功能
        Undo.RecordObject(reelStripData, "Add Reel Strip Combinations");

        // 更新或新增組合
        foreach (var kvp in newCombinations)
        {
            int combIndex = kvp.Key;
            ReelStripCombination combination = kvp.Value;

            if (combIndex < reelStripData.ReelStripCombinations.Count)
            {
                // 更新現有的組合
                reelStripData.ReelStripCombinations[combIndex].ReelStripGroup = combination.ReelStripGroup;
            }
            else
            {
                // 添加新的組合，填充空白
                while (reelStripData.ReelStripCombinations.Count < combIndex)
                {
                    reelStripData.ReelStripCombinations.Add(new ReelStripCombination());
                }
                reelStripData.ReelStripCombinations.Add(combination);
            }
        }

        // 應用修改
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(reelStripData);
        AssetDatabase.SaveAssets();

        TimeSpan span = DateTime.Now - startTime;

        // 顯示成功對話框
        EditorUtility.DisplayDialog("成功", $"組合資料已成功新增。耗時 {span.TotalSeconds:F2} 秒", "確定");
    }

    /// <summary>
    /// 用於摘要的資料結構
    /// </summary>
    private class GroupSummary
    {
        public string GroupName;
        public int NumberOfReels;
        public List<int> ElementsPerReel;

        public GroupSummary(string name)
        {
            GroupName = name;
            NumberOfReels = 0;
            ElementsPerReel = new List<int>();
        }
    }
}
