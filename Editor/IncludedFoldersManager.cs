// Assets\Sanat\CodeGenerator\Editor\IncludedFoldersManager.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Sanat.CodeGenerator
{
    public class IncludedFoldersManager
    {
        private const string INCLUDED_FOLDERS_PREFS_KEY = "CodeGenerator_IncludedFolders";
        private List<IncludedFolder> includedFolders = new();

        [System.Serializable]
        private class IncludedFolder
        {
            public string path;
            public bool isEnabled;
        }

        [System.Serializable]
        private class SerializedIncludedFolders
        {
            public List<IncludedFolder> folders = new();
        }

        public IncludedFoldersManager()
        {
            LoadIncludedFolders();
        }

        public void DrawIncludedFoldersUI()
        {
            EditorGUILayout.LabelField("Included Folders:", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add Included Folder"))
            {
                includedFolders.Add(new IncludedFolder { path = "", isEnabled = true });
                SaveIncludedFolders();
            }

            for (int i = 0; i < includedFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Store old values
                string oldPath = includedFolders[i].path;
                bool oldEnabled = includedFolders[i].isEnabled;

                // Draw fields
                includedFolders[i].path = EditorGUILayout.TextField(
                    "Folder Path (Ex.: Assets\\Test)", 
                    includedFolders[i].path, 
                    GUILayout.ExpandWidth(true)
                );
                
                includedFolders[i].isEnabled = EditorGUILayout.Toggle(
                    includedFolders[i].isEnabled, 
                    GUILayout.Width(20)
                );

                // Check if values changed
                if (oldPath != includedFolders[i].path || oldEnabled != includedFolders[i].isEnabled)
                {
                    SaveIncludedFolders();
                }

                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    includedFolders.RemoveAt(i);
                    SaveIncludedFolders();
                    i--; // Adjust index after removal
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        public List<string> GetEnabledFolders()
        {
            return includedFolders
                .Where(folder => folder.isEnabled)
                .Select(folder => folder.path)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();
        }

        private void LoadIncludedFolders()
        {
            string json = EditorPrefs.GetString(INCLUDED_FOLDERS_PREFS_KEY, "");
            
            if (string.IsNullOrEmpty(json))
            {
                includedFolders = new List<IncludedFolder>();
                return;
            }

            try
            {
                SerializedIncludedFolders serialized = JsonUtility.FromJson<SerializedIncludedFolders>(json);
                includedFolders = serialized.folders ?? new List<IncludedFolder>();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading included folders: {e.Message}");
                includedFolders = new List<IncludedFolder>();
            }
        }

        private void SaveIncludedFolders()
        {
            try
            {
                SerializedIncludedFolders serialized = new SerializedIncludedFolders
                {
                    folders = includedFolders
                };
                
                string json = JsonUtility.ToJson(serialized);
                EditorPrefs.SetString(INCLUDED_FOLDERS_PREFS_KEY, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error saving included folders: {e.Message}");
            }
        }
    }
}