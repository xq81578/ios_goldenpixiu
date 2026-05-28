using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization; // 處理代理對的關鍵
using System;

namespace Core.Localization.Editor
{

    public class CharacterStripWindow : EditorWindow
    {
        private string inputFilePath = "";
        private string statusMessage = "";
        private MessageType messageType = MessageType.Info;

        // 1. 建立一個選單項目來打開這個視窗
        [MenuItem ("Tools/Character/Create Unique Strip File Window")]
        public static void ShowWindow ()
        {
            // 獲取現有開啟的視窗，或者如果它不存在，就建立一個新的
            GetWindow<CharacterStripWindow> ("Character Strip Utility");
        }

        // 2. 繪製視窗的 UI
        private void OnGUI ()
        {
            EditorGUILayout.LabelField ("Unique Character File Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox ("此工具會讀取一個 .txt 檔案，找出所有不重複的字符（包含 Emoji），並在相同資料夾下產生一個 _strip.txt 檔案。", MessageType.Info);

            EditorGUILayout.Space (10);

            // --- 檔案選擇區域 ---
            EditorGUILayout.BeginHorizontal ();
            // 顯示目前選擇的路徑 (設為 ReadOnly，只能透過按鈕更改)
            EditorGUILayout.TextField ("Source File Path", inputFilePath);

            if (GUILayout.Button ("Browse...", GUILayout.Width (80)))
            {
                // 打開檔案選擇對話框，限定為 txt
                string path = EditorUtility.OpenFilePanel ("Select Source TXT File", "", "txt");
                if (!string.IsNullOrEmpty (path))
                {
                    inputFilePath = path;
                    statusMessage = ""; // 清除舊的狀態訊息
                }
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space (10);

            // --- 執行按鈕 ---
            if (GUILayout.Button ("Generate Unique Character File"))
            {
                ProcessFile ();
            }

            EditorGUILayout.Space (10);

            // --- 狀態/結果回饋 ---
            if (!string.IsNullOrEmpty (statusMessage))
            {
                EditorGUILayout.HelpBox (statusMessage, messageType);
            }
        }

        /// <summary>
        /// 執行處理的主邏輯
        /// </summary>
        private void ProcessFile ()
        {
            if (string.IsNullOrEmpty (inputFilePath))
            {
                statusMessage = "Error: Please select a source .txt file first.";
                messageType = MessageType.Error;
                return;
            }

            try
            {
                // 呼叫我們之前寫的核心邏輯
                string outputFilePath = CreateUniqueCharacterStripFile (inputFilePath);

                // 成功
                statusMessage = $"Success! File generated at:\n{outputFilePath}";
                messageType = MessageType.Info;

                // (額外功能) 如果檔案在 Assets 目錄下，自動刷新並 ping 它
                if (outputFilePath.StartsWith (Application.dataPath))
                {
                    AssetDatabase.Refresh ();
                    string assetPath = "Assets" + outputFilePath.Substring (Application.dataPath.Length);
                    UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object> (assetPath);
                    if (obj != null)
                    {
                        EditorGUIUtility.PingObject (obj);
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                statusMessage = $"Error: File not found.\n{ex.Message}";
                messageType = MessageType.Error;
            }
            catch (IOException ex)
            {
                statusMessage = $"Error: Could not read/write file (is it open? Check permissions).\n{ex.Message}";
                messageType = MessageType.Error;
            }
            catch (Exception ex)
            {
                statusMessage = $"An unexpected error occurred:\n{ex.Message}";
                messageType = MessageType.Error;
            }
        }

        // 3. 這是從您上一個問題複製過來的核心邏輯
        //    (將它放在 EditorWindow 類別內部作為一個私有方法)
        //
        //    讀取一個 .txt 檔案，找出所有不重複的「邏輯字符」
        //    (能正確處理代理對，如 Emoji)，
        //    並在相同資料夾下產生一個新的 _strip.txt 檔案。
        private string CreateUniqueCharacterStripFile (string inputPath)
        {
            // 1. 驗證來源檔案 (在 ProcessFile 中已檢查，但為安全起見)
            if (!File.Exists (inputPath))
            {
                throw new FileNotFoundException ("Source file not found.", inputPath);
            }

            // 2. 決定輸出路徑
            string directory = Path.GetDirectoryName (inputPath);
            string originalFilename = Path.GetFileNameWithoutExtension (inputPath);
            string outputFilename = $"{originalFilename}_strip.txt";
            string outputFilePath = Path.Combine (directory, outputFilename);

            // 3. 核心邏輯：使用 HashSet<string> 來儲存邏輯字符
            var uniqueLogicalCharacters = new HashSet<string> ();

            // 4. 以 UTF-8 格式逐行讀取檔案
            foreach (string line in File.ReadLines (inputPath, Encoding.UTF8))
            {
                // 處理代理對的關鍵！
                TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator (line);
                while (enumerator.MoveNext ())
                {
                    uniqueLogicalCharacters.Add (enumerator.GetTextElement ());
                }
            }

            // 5. 準備輸出內容
            string outputContent = string.Concat (uniqueLogicalCharacters);

            // 6. 以 UTF-8 格式寫入新檔案
            File.WriteAllText (outputFilePath, outputContent, Encoding.UTF8);

            // 7. 返回新檔案的路徑
            return outputFilePath;
        }
    }
}