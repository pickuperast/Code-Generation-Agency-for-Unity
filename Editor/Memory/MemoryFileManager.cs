// Assets/Sanat/CodeGenerator/Editor/Memory/MemoryFileManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Sanat.CodeGenerator.Editor.Memory
{
    public class MemoryFileManager
    {
        private const string MEMORY_FOLDER_PATH = "/Sanat/CodeGenerator/Memory/";
        private List<MemoryFile> _availableMemoryFiles = new();
        
        [Serializable]
        public class MemoryFile
        {
            public string FileName;
            public string FilePath;
            public bool IsSelected;
            
            public MemoryFile(string fileName, string filePath, bool isSelected = false)
            {
                FileName = fileName;
                FilePath = filePath;
                IsSelected = isSelected;
            }
        }
        
        public MemoryFileManager()
        {
            LoadAvailableMemoryFiles();
        }
        
        public List<MemoryFile> GetAvailableMemoryFiles()
        {
            return _availableMemoryFiles;
        }
        
        public void LoadAvailableMemoryFiles()
        {
            _availableMemoryFiles.Clear();
            string memoryFolderPath = Path.Combine(Application.dataPath, MEMORY_FOLDER_PATH.TrimStart('/'));
            
            if (!Directory.Exists(memoryFolderPath))
            {
                Debug.LogWarning($"Memory folder not found at: {memoryFolderPath}");
                return;
            }
            
            string[] mdFiles = Directory.GetFiles(memoryFolderPath, "*.md");
            foreach (string filePath in mdFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                _availableMemoryFiles.Add(new MemoryFile(fileName, filePath));
            }
            
            Debug.Log($"Loaded {_availableMemoryFiles.Count} memory files from {memoryFolderPath}");
        }
        
        public void UpdateSelectionState(List<string> selectedFileNames)
        {
            foreach (var memoryFile in _availableMemoryFiles)
            {
                memoryFile.IsSelected = selectedFileNames.Contains(memoryFile.FileName);
            }
        }
        
        public List<string> GetSelectedFilePaths()
        {
            return _availableMemoryFiles
                .Where(file => file.IsSelected)
                .Select(file => file.FilePath)
                .ToList();
        }
        
        public List<string> GetSelectedFileNames()
        {
            return _availableMemoryFiles
                .Where(file => file.IsSelected)
                .Select(file => file.FileName)
                .ToList();
        }
        
        public string LoadMemoryContent(List<string> selectedFilePaths)
        {
            string combinedContent = "";
            
            foreach (string filePath in selectedFilePaths)
            {
                try
                {
                    string content = File.ReadAllText(filePath);
                    combinedContent += $"# MEMORY FROM {Path.GetFileNameWithoutExtension(filePath)}:{content}";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading memory file {filePath}: {ex.Message}");
                }
            }
            
            return combinedContent;
        }
    }
}
