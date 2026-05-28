using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class TexturePackerTool : EditorWindow
{
    [MenuItem("Tools/Texture Packer Tool", false, 1)]
    public static void ShowWindow()
    {
        TexturePackerTool window = (TexturePackerTool)EditorWindow.GetWindow(typeof(TexturePackerTool), false, "Texture Packer Tool");
        window.minSize = new Vector2(400, 500);
        window.Show();
    }

    private List<Texture2D> _textures = new List<Texture2D>();
    private string _outputName = "atlas";
    private string _outputPath = "Assets/";
    private int _padding = 2;
    private Vector2 _scrollPos;

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        GUILayout.Label("Texture Packer Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 选择纹理
        GUILayout.Label("Selected Textures:", EditorStyles.label);
        GUILayout.Space(5);

        for (int i = 0; i < _textures.Count; i++)
        {
            GUILayout.BeginHorizontal();
            _textures[i] = (Texture2D)EditorGUILayout.ObjectField(_textures[i], typeof(Texture2D), false, GUILayout.Width(300));
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                _textures.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Add Texture", GUILayout.Width(150)))
        {
            _textures.Add(null);
        }

        GUILayout.Space(20);

        // 输出设置
        GUILayout.Label("Output Settings:", EditorStyles.boldLabel);
        GUILayout.Space(5);

        _outputName = EditorGUILayout.TextField("Output Name:", _outputName);
        _outputPath = EditorGUILayout.TextField("Output Path:", _outputPath);
        _padding = EditorGUILayout.IntField("Padding:", _padding);

        if (GUILayout.Button("Select Output Folder"))
        {
            string path = EditorUtility.OpenFolderPanel("Select Output Folder", _outputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                // 转换为相对路径
                if (path.StartsWith(Application.dataPath))
                {
                    _outputPath = "Assets" + path.Substring(Application.dataPath.Length) + "/";
                }
                else
                {
                    _outputPath = path + "/";
                }
            }
        }

        GUILayout.Space(20);

        // 生成按钮
        if (GUILayout.Button("Generate Atlas", GUILayout.Height(40)))
        {
            GenerateAtlas();
        }

        EditorGUILayout.EndScrollView();
    }

    private void GenerateAtlas()
    {
        // 验证输入
        if (_textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Please select at least one texture.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_outputName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter an output name.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_outputPath))
        {
            EditorUtility.DisplayDialog("Error", "Please enter an output path.", "OK");
            return;
        }

        // 移除空纹理
        _textures.RemoveAll(t => t == null);

        // 检查并启用纹理的Read/Write属性
        List<bool> originalReadWriteStates = new List<bool>();
        List<Texture2D> texturesToProcess = new List<Texture2D>();
        
        for (int i = 0; i < _textures.Count; i++)
        {
            Texture2D tex = _textures[i];
            string assetPath = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                originalReadWriteStates.Add(importer.isReadable);
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                
                // 重新获取纹理引用
                Texture2D reloadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (reloadedTex != null)
                {
                    texturesToProcess.Add(reloadedTex);
                }
            }
            else
            {
                originalReadWriteStates.Add(false);
                texturesToProcess.Add(tex);
            }
        }
        
        // 使用重新加载的纹理进行后续处理
        _textures = texturesToProcess;

        // 计算图集大小
        int maxWidth = 1024;
        int maxHeight = 1024;
        int currentX = 0;
        int currentY = 0;
        int rowHeight = 0;

        // 计算总大小
        int totalWidth = 0;
        int totalHeight = 0;

        for (int i = 0; i < _textures.Count; i++)
        {
            Texture2D tex = _textures[i];
            if (currentX + tex.width + _padding > maxWidth)
            {
                // 换行
                totalWidth = Mathf.Max(totalWidth, currentX);
                totalHeight += rowHeight + _padding;
                currentX = 0;
                currentY = totalHeight;
                rowHeight = 0;
            }

            currentX += tex.width + _padding;
            rowHeight = Mathf.Max(rowHeight, tex.height);
        }

        // 最后一行
        totalWidth = Mathf.Max(totalWidth, currentX);
        totalHeight += rowHeight;

        // 确保宽度和高度是2的幂
        totalWidth = Mathf.NextPowerOfTwo(totalWidth);
        totalHeight = Mathf.NextPowerOfTwo(totalHeight);

        // 创建图集纹理
        Texture2D atlasTexture = new Texture2D(totalWidth, totalHeight, TextureFormat.RGBA32, false);
        atlasTexture.filterMode = FilterMode.Point;
        atlasTexture.wrapMode = TextureWrapMode.Clamp;

        // 填充透明
        Color[] transparentPixels = new Color[atlasTexture.width * atlasTexture.height];
        for (int i = 0; i < transparentPixels.Length; i++)
        {
            transparentPixels[i] = Color.clear;
        }
        atlasTexture.SetPixels(transparentPixels);

        // 生成JSON数据
        Dictionary<string, object> atlasData = new Dictionary<string, object>();
        List<Dictionary<string, object>> frames = new List<Dictionary<string, object>>();

        // 重置位置
        currentX = 0;
        currentY = 0;
        rowHeight = 0;

        for (int i = 0; i < _textures.Count; i++)
        {
            Texture2D tex = _textures[i];
            if (currentX + tex.width + _padding > atlasTexture.width)
            {
                // 换行
                currentY += rowHeight + _padding;
                currentX = 0;
                rowHeight = 0;
            }

            // 获取纹理像素
            Color[] pixels = tex.GetPixels();
            atlasTexture.SetPixels(currentX, currentY, tex.width, tex.height, pixels);

            // 创建帧数据
            Dictionary<string, object> frameData = new Dictionary<string, object>();
            frameData["filename"] = Path.GetFileName(AssetDatabase.GetAssetPath(tex));
            frameData["frame"] = new Dictionary<string, int>
            {
                {"x", currentX},
                {"y", currentY},
                {"w", tex.width},
                {"h", tex.height}
            };
            frameData["rotated"] = false;
            frameData["trimmed"] = false;
            frameData["spriteSourceSize"] = new Dictionary<string, int>
            {
                {"x", 0},
                {"y", 0},
                {"w", tex.width},
                {"h", tex.height}
            };
            frameData["sourceSize"] = new Dictionary<string, int>
            {
                {"w", tex.width},
                {"h", tex.height}
            };

            frames.Add(frameData);

            currentX += tex.width + _padding;
            rowHeight = Mathf.Max(rowHeight, tex.height);
        }

        // 应用像素更改
        atlasTexture.Apply();

        // 创建meta数据
        Dictionary<string, object> metaData = new Dictionary<string, object>();
        metaData["app"] = "https://www.codeandweb.com/texturepacker";
        metaData["version"] = "1.0";
        metaData["image"] = _outputName + ".png";
        metaData["format"] = "RGBA8888";
        metaData["size"] = new Dictionary<string, int>
        {
            {"w", atlasTexture.width},
            {"h", atlasTexture.height}
        };
        metaData["scale"] = "1";
        metaData["smartupdate"] = "$TexturePacker:SmartUpdate:00000000000000000000000000000000:00000000000000000000000000000000:00000000000000000000000000000000$";

        atlasData["frames"] = frames;
        atlasData["meta"] = metaData;

        // 保存图集
        string pngPath = Path.Combine(_outputPath, _outputName + ".png");
        byte[] pngData = atlasTexture.EncodeToPNG();
        File.WriteAllBytes(pngPath, pngData);

        // 保存JSON
        string jsonPath = Path.Combine(_outputPath, _outputName + ".json");
        string jsonContent = ConvertToJson(atlasData, true);
        File.WriteAllText(jsonPath, jsonContent);

        // 刷新AssetDatabase
        AssetDatabase.Refresh();

        // 显示成功信息
        EditorUtility.DisplayDialog("Success", "Atlas generated successfully!\n" + pngPath + "\n" + jsonPath, "OK");
    }

    private string ConvertToJson(Dictionary<string, object> data, bool prettyPrint)
    {
        StringBuilder sb = new StringBuilder();
        int indentLevel = 0;
        bool isFirst = true;

        sb.AppendLine("{");
        indentLevel++;

        // 处理frames
        sb.Append(GetIndent(indentLevel));
        sb.AppendLine("\"frames\": [");
        indentLevel++;

        List<Dictionary<string, object>> frames = (List<Dictionary<string, object>>)data["frames"];
        for (int i = 0; i < frames.Count; i++)
        {
            Dictionary<string, object> frame = frames[i];
            if (i > 0)
            {
                sb.AppendLine(",");
            }
            sb.Append(GetIndent(indentLevel));
            sb.AppendLine("{");
            indentLevel++;

            // filename
            sb.Append(GetIndent(indentLevel));
            sb.AppendFormat("\"filename\": \"{0}\",\n", frame["filename"]);

            // frame
            sb.Append(GetIndent(indentLevel));
            sb.Append("\"frame\": {");
            Dictionary<string, int> frameDict = (Dictionary<string, int>)frame["frame"];
            sb.AppendFormat("\"x\":{0},\"y\":{1},\"w\":{2},\"h\":{3}", frameDict["x"], frameDict["y"], frameDict["w"], frameDict["h"]);
            sb.AppendLine("},");

            // rotated
            sb.Append(GetIndent(indentLevel));
            sb.AppendFormat("\"rotated\": {0},\n", frame["rotated"]);

            // trimmed
            sb.Append(GetIndent(indentLevel));
            sb.AppendFormat("\"trimmed\": {0},\n", frame["trimmed"]);

            // spriteSourceSize
            sb.Append(GetIndent(indentLevel));
            sb.Append("\"spriteSourceSize\": {");
            Dictionary<string, int> spriteSourceSize = (Dictionary<string, int>)frame["spriteSourceSize"];
            sb.AppendFormat("\"x\":{0},\"y\":{1},\"w\":{2},\"h\":{3}", spriteSourceSize["x"], spriteSourceSize["y"], spriteSourceSize["w"], spriteSourceSize["h"]);
            sb.AppendLine("},");

            // sourceSize
            sb.Append(GetIndent(indentLevel));
            sb.Append("\"sourceSize\": {");
            Dictionary<string, int> sourceSize = (Dictionary<string, int>)frame["sourceSize"];
            sb.AppendFormat("\"w\":{0},\"h\":{1}", sourceSize["w"], sourceSize["h"]);
            sb.AppendLine("}");

            indentLevel--;
            sb.Append(GetIndent(indentLevel));
            sb.Append("}");
        }

        indentLevel--;
        sb.AppendLine();
        sb.Append(GetIndent(indentLevel));
        sb.AppendLine("],");

        // 处理meta
        sb.Append(GetIndent(indentLevel));
        sb.AppendLine("\"meta\": {");
        indentLevel++;

        Dictionary<string, object> meta = (Dictionary<string, object>)data["meta"];
        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"app\": \"{0}\",\n", meta["app"]);
        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"version\": \"{0}\",\n", meta["version"]);
        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"image\": \"{0}\",\n", meta["image"]);
        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"format\": \"{0}\",\n", meta["format"]);

        // size
        sb.Append(GetIndent(indentLevel));
        sb.Append("\"size\": {");
        Dictionary<string, int> size = (Dictionary<string, int>)meta["size"];
        sb.AppendFormat("\"w\":{0},\"h\":{1}", size["w"], size["h"]);
        sb.AppendLine("},");

        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"scale\": \"{0}\",\n", meta["scale"]);
        sb.Append(GetIndent(indentLevel));
        sb.AppendFormat("\"smartupdate\": \"{0}\"\n", meta["smartupdate"]);

        indentLevel--;
        sb.Append(GetIndent(indentLevel));
        sb.AppendLine("}");

        indentLevel--;
        sb.AppendLine("}");

        return sb.ToString();
    }

    private string GetIndent(int level)
    {
        return new string('\t', level);
    }
}