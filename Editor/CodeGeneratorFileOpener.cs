using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Sanat.CodeGenerator.Editor
{
    public static class CodeGeneratorFileOpener
    {
        public static void OpenScript(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("File path is null or empty.");
                return;
            }

            string relativePath = filePath.Replace(Application.dataPath, "Assets");
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);

            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                AssetDatabase.OpenAsset(asset);
            }
            else
            {
                Debug.Log($"Could not find script at path: {relativePath}. Opening folder in Explorer.");
                OpenFolderInExplorer(filePath);
            }
        }

        private static void OpenFolderInExplorer(string filePath)
        {
            string folderPath = Path.GetDirectoryName(filePath);
            
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"Folder does not exist: {folderPath}");
                return;
            }

            try
            {
                #if UNITY_EDITOR_WIN
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                #elif UNITY_EDITOR_OSX
                    Process.Start("open", folderPath);
                #elif UNITY_EDITOR_LINUX
                    Process.Start("xdg-open", folderPath);
                #endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to open folder: {e.Message}");
            }
        }
    }
}