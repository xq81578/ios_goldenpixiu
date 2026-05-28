using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public class FontCharacterAnalyzer : EditorWindow
{
    private string fontCodesText = ""; // 已包含的编码输入
    private string inputText = ""; // 需要显示的字符输入
    private string resultText = "";
    private Vector2 scrollPosition;
    private Vector2 inputScrollPosition;
    private Vector2 fontCodesScrollPosition;
    private string  missingCodesText = "";

    [MenuItem("Tools/字体字符编码分析器")]
    public static void ShowWindow()
    {
        GetWindow<FontCharacterAnalyzer>("字体字符编码分析器");
    }

    private void OnGUI()
    {
        GUILayout.Label("字体字符编码分析工具", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 已包含的编码输入区域
        GUILayout.Label("已包含的编码（十六进制，用逗号分隔）:");
        fontCodesScrollPosition = EditorGUILayout.BeginScrollView(fontCodesScrollPosition, GUILayout.Height(80));
        fontCodesText = EditorGUILayout.TextArea(fontCodesText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        
        GUILayout.Label($"编码数量: {GetActualCodeCount(fontCodesText)}", EditorStyles.miniLabel);
        
        GUILayout.Space(10);
        
        // 需要显示的字符输入区域
        GUILayout.Label("需要显示的字符:");
        inputScrollPosition = EditorGUILayout.BeginScrollView(inputScrollPosition, GUILayout.Height(100));
        inputText = EditorGUILayout.TextArea(inputText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        
        GUILayout.Label($"字符数量: {GetCharacterCount(inputText)}", EditorStyles.miniLabel);

        GUILayout.Space(10);

        if (GUILayout.Button("分析缺少的字符编码"))
        {
            if (string.IsNullOrEmpty(fontCodesText))
            {
                EditorUtility.DisplayDialog("错误", "请输入已包含的编码", "确定");
                return;
            }
            
            if (string.IsNullOrEmpty(inputText))
            {
                EditorUtility.DisplayDialog("错误", "请输入需要显示的字符", "确定");
                return;
            }

            AnalyzeMissingCharacters();
        }

        GUILayout.Space(10);

        // 结果显示
        if (!string.IsNullOrEmpty(resultText))
        {
            GUILayout.Label("分析结果:");
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            EditorGUILayout.TextArea(resultText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("复制结果到剪贴板"))
            {
                GUIUtility.systemCopyBuffer = missingCodesText;
                EditorUtility.DisplayDialog("成功", "结果已复制到剪贴板", "确定");
            }
        }
    }

    private int GetCharacterCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Where(c => !char.IsWhiteSpace(c) && c != '\n' && c != '\r' && c != '\t').Count();
    }

    private int GetActualCodeCount(string codesText)
    {
        if (string.IsNullOrEmpty(codesText)) return 0;
        
        HashSet<int> codes = ParseInputCodes(codesText);
        return codes.Count;
    }

    private void AnalyzeMissingCharacters()
    {
        try
        {
            // 从输入文本提取已包含的编码
            HashSet<int> existingCodes = ParseInputCodes(fontCodesText);
            
            // 从输入文本提取需要显示的字符编码
            HashSet<int> requiredCodes = ExtractRequiredCharacterCodes(inputText);
            
            // 找出缺少的编码
            HashSet<int> missingCodes = new HashSet<int>(requiredCodes.Except(existingCodes));
            
            // 生成结果
            StringBuilder result = new StringBuilder();
            result.AppendLine($"已包含的编码数量: {existingCodes.Count}");
            result.AppendLine($"缺少的字符编码数量: {missingCodes.Count}");
            result.AppendLine();
            
            // 显示缺少的编码（逗号分隔）
            if (missingCodes.Count > 0)
            {
                result.AppendLine("=== 缺少的字符编码 ===");
                var sortedMissingCodes = missingCodes.OrderBy(code => code).ToList();
                missingCodesText = string.Join(",", sortedMissingCodes.Select(code => $"{code:X4}"));
                result.AppendLine(missingCodesText);
                result.AppendLine();
                result.AppendLine("=== 缺少的字符示例 ===");
                
                // 显示缺少的字符示例
                foreach (var code in sortedMissingCodes)
                {
                    char character = (char)code;
                    
                    result.AppendLine($"U+{code:X4}: {character} ");
                }
            }
            else
            {
                result.AppendLine("✅ 所有字符编码都已包含！");
            }
            
            resultText = result.ToString();
        }
        catch (System.Exception e)
        {
            resultText = $"分析过程中出现错误: {e.Message}\n{e.StackTrace}";
        }
    }

    private HashSet<int> ParseInputCodes(string codesText)
    {
        HashSet<int> codes = new HashSet<int>();
        
        if (string.IsNullOrEmpty(codesText)) return codes;
        
        // 分割逗号分隔的编码
        string[] codeStrings = codesText.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string codeStr in codeStrings)
        {
            string trimmedCode = codeStr.Trim();
            if (string.IsNullOrEmpty(trimmedCode)) continue;
            
            if (trimmedCode.Contains("-"))
            {
                // 处理范围格式 "XXXX-XXXX"
                string[] parts = trimmedCode.Split('-');
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out int start) &&
                        int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out int end))
                    {
                        for (int code = start; code <= end; code++)
                        {
                            codes.Add(code);
                        }
                    }
                }
            }
            else
            {
                // 处理单个编码格式 "XXXX"
                if (int.TryParse(trimmedCode, System.Globalization.NumberStyles.HexNumber, null, out int code))
                {
                    codes.Add(code);
                }
            }
        }
        
        return codes;
    }
    
    private HashSet<int> ExtractRequiredCharacterCodes(string text)
    {
        HashSet<int> codes = new HashSet<int>();
        
        if (string.IsNullOrEmpty(text)) return codes;
        
        // 提取所有字符的编码
        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c) && c != '\n' && c != '\r' && c != '\t')
            {
                int code = (int)c;
                codes.Add(code);
            }
        }
        
        return codes;
    }

}