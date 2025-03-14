// Assets\Sanat\CodeGenerator\Editor\Memory\MemoryFileSelectionUI.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sanat.CodeGenerator.Agents;
using Sanat.CodeGenerator.Editor.Memory;

namespace Sanat.CodeGenerator.Editor
{
    public class MemoryFileSelectionUI
    {
        private MemoryFileManager _memoryFileManager;
        private Vector2 _scrollPosition;
        private bool _showMemoryFiles = false;
        private string _searchFilter = "";

        public MemoryFileSelectionUI()
        {
            _memoryFileManager = new MemoryFileManager();
        }

        public void DrawMemoryFileSelection(AgentModelSettings agentSettings)
        {
            _showMemoryFiles = EditorGUILayout.Foldout(_showMemoryFiles, "Memory Files", true);
            
            if (!_showMemoryFiles)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            if (GUILayout.Button("Refresh Memory Files"))
            {
                _memoryFileManager.LoadAvailableMemoryFiles();
            }

            // Add search field
            _searchFilter = EditorGUILayout.TextField("Search", _searchFilter);

            var memoryFiles = _memoryFileManager.GetAvailableMemoryFiles();
            _memoryFileManager.UpdateSelectionState(agentSettings.SelectedMemoryFiles);

            if (memoryFiles.Count == 0)
            {
                EditorGUILayout.HelpBox("No memory files found in the memory folder.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                {
                    SelectAllMemoryFiles(memoryFiles, agentSettings);
                }
                if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                {
                    DeselectAllMemoryFiles(memoryFiles, agentSettings);
                }
                EditorGUILayout.EndHorizontal();

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
                
                foreach (var memoryFile in memoryFiles)
                {
                    // Skip if doesn't match search filter
                    if (!string.IsNullOrEmpty(_searchFilter) && 
                        !memoryFile.FileName.ToLower().Contains(_searchFilter.ToLower()))
                        continue;

                    bool wasSelected = memoryFile.IsSelected;
                    memoryFile.IsSelected = EditorGUILayout.ToggleLeft(
                        memoryFile.FileName, 
                        memoryFile.IsSelected
                    );
                    
                    // If selection state changed, update the agent settings
                    if (wasSelected != memoryFile.IsSelected)
                    {
                        if (memoryFile.IsSelected)
                        {
                            if (!agentSettings.SelectedMemoryFiles.Contains(memoryFile.FileName))
                                agentSettings.SelectedMemoryFiles.Add(memoryFile.FileName);
                        }
                        else
                        {
                            agentSettings.SelectedMemoryFiles.Remove(memoryFile.FileName);
                        }
                    }
                }
                
                EditorGUILayout.EndScrollView();
            }

            // Show selected files count
            EditorGUILayout.LabelField($"Selected: {agentSettings.SelectedMemoryFiles.Count} files", EditorStyles.boldLabel);
            
            EditorGUILayout.EndVertical();
        }

        private void SelectAllMemoryFiles(List<MemoryFileManager.MemoryFile> memoryFiles, AgentModelSettings agentSettings)
        {
            agentSettings.SelectedMemoryFiles.Clear();
            foreach (var memoryFile in memoryFiles)
            {
                memoryFile.IsSelected = true;
                agentSettings.SelectedMemoryFiles.Add(memoryFile.FileName);
            }
        }

        private void DeselectAllMemoryFiles(List<MemoryFileManager.MemoryFile> memoryFiles, AgentModelSettings agentSettings)
        {
            agentSettings.SelectedMemoryFiles.Clear();
            foreach (var memoryFile in memoryFiles)
            {
                memoryFile.IsSelected = false;
            }
        }

        public string GetSelectedMemoryContent(List<string> selectedFileNames)
        {
            _memoryFileManager.UpdateSelectionState(selectedFileNames);
            var selectedFilePaths = _memoryFileManager.GetSelectedFilePaths();
            return _memoryFileManager.LoadMemoryContent(selectedFilePaths);
        }
    }
}