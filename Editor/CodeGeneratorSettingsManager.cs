using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Sanat.CodeGenerator.Agents;
using Sanat.CodeGenerator.Editor.Bookmarks;
using Sanat.CodeGenerator.CodebaseRag;
using UnityEngine;
using UnityEditor;

namespace Sanat.CodeGenerator.Editor
{
    public class CodeGeneratorSettingsManager
    {
        public void SaveSettings(CodeGenerator codeGenerator)
        {
            EditorPrefs.SetString("SelectedClassNames", string.Join(",", codeGenerator.selectedClassNames));
            EditorPrefs.SetString("GeminiApiKey", codeGenerator.geminiApiKey);
            EditorPrefs.SetString("OpenaiApiKey", codeGenerator.openaiApiKey);
            EditorPrefs.SetString("GroqApiKey", codeGenerator.groqApiKey);
            EditorPrefs.SetString("AntrophicApiKey", codeGenerator.antrophicApiKey);
            EditorPrefs.SetString("GeminiProjectName", codeGenerator.GeminiProjectName);
            EditorPrefs.SetString("SupabaseUrl", codeGenerator.supabaseUrl);
            EditorPrefs.SetString("SupabaseKey", codeGenerator.supabaseKey);
            EditorPrefs.SetString("TableName", codeGenerator.tableName);
            EditorPrefs.SetInt("IsRag", codeGenerator.isRag ? 1 : 0);
            EditorPrefs.SetString("IgnoredFolders", string.Join(",", codeGenerator._ignoredFolders));
            EditorPrefs.SetString(CodeGenerator.INCLUDED_FOLDERS_PREFS_KEY, JsonUtility.ToJson(codeGenerator.includedFolders));
            codeGenerator.bookmarkManager.OnBookmarkLoaded -= codeGenerator.LoadBookmarkData;
            SaveAgentModelSettings(codeGenerator);
        }
        
        public async UniTask LoadSettings(CodeGenerator codeGenerator)
        {
            codeGenerator.IsSettingsLoaded = false;
            codeGenerator.selectedClassNames = new List<string>(
                EditorPrefs.GetString("SelectedClassNames", "")
                    .Split(',')
                    .Where(s => !string.IsNullOrEmpty(s))
            );
            codeGenerator.geminiApiKey = EditorPrefs.GetString("GeminiApiKey", "");
            codeGenerator.openaiApiKey = EditorPrefs.GetString("OpenaiApiKey", "");
            codeGenerator.groqApiKey = EditorPrefs.GetString("GroqApiKey", "");
            codeGenerator.antrophicApiKey = EditorPrefs.GetString("AntrophicApiKey", "");
            codeGenerator.supabaseUrl = EditorPrefs.GetString("SupabaseUrl", "");
            codeGenerator.supabaseKey = EditorPrefs.GetString("SupabaseKey", "");
            codeGenerator.tableName = EditorPrefs.GetString("TableName", "");
            codeGenerator.isRag = EditorPrefs.GetInt("IsRag", 0) == 1;
            codeGenerator.GeminiProjectName = EditorPrefs.GetString(CodeGenerator.PREFS_GEMINI_PROJECT_NAME, "");
            codeGenerator._ignoredFolders = EditorPrefs.GetString("IgnoredFolders", "").Split(',').Where(s => !string.IsNullOrEmpty(s)).ToList();
            codeGenerator.includedFolders = JsonUtility.FromJson<List<CodeGenerator.IncludedFolder>>(EditorPrefs.GetString(CodeGenerator.INCLUDED_FOLDERS_PREFS_KEY, "[]"));
            codeGenerator.taskInput = EditorPrefs.GetString(CodeGenerator.PREFS_KEY_TASK, "");
            codeGenerator.isGeneratingCode = false;
            Debug.Log("Loaded EditorPrefs settings");
            // Rest of the initialization code remains the same
            CodeGenerator.IncludedFoldersManager = new IncludedFoldersManager();
            Debug.Log("IncludedFoldersManager created");
            codeGenerator.RefreshClassList();
            Debug.Log("Class list refreshed");
            codeGenerator.CheckAndHandleFirstLaunch();
            Debug.Log("First launch checked");
            codeGenerator.bookmarkManager = new CodeGeneratorBookmarks();
            codeGenerator.bookmarkManager.OnBookmarkLoaded += codeGenerator.LoadBookmarkData;
            codeGenerator.bookmarkManager.InitializeReorderableList();
            Debug.Log("Bookmarks loaded");
            var bookmarks = codeGenerator.bookmarkManager.GetBookmarks();
            codeGenerator.bookmarkManager.InitializeReorderableList();
            Debug.Log("Reorderable list initialized");
            codeGenerator.ragProcessor = new RagProcessor();
            InitializeAgentModelSettings(codeGenerator);
            LoadAgentModelSettings(codeGenerator);
            Debug.Log("Agent model settings initialized");
            codeGenerator.IsSettingsLoaded = true;
        }
        
        public void InitializeAgentModelSettings(CodeGenerator codeGenerator)
        {
            codeGenerator.agentModelSettings = new Dictionary<string, AgentModelSettings>
            {
                { "AgentCodeHighLevelArchitector", new AgentModelSettings
                {
                    AgentName = "High Level Architector",
                    ApiProvider = AbstractAgentHandler.ApiProviders.Anthropic,
                    ModelName = ApiAnthropic.Model.Claude37.Name,
                    SelectedMemoryFiles = new List<string>()
                }},
                { "AgentCodeArchitector", new AgentModelSettings
                {
                    AgentName = "Code Architector",
                    ApiProvider = AbstractAgentHandler.ApiProviders.Gemini,
                    ModelName = Sanat.ApiGemini.Model.Flash2.Name
                }},
                { "AgentCodeMerger", new AgentModelSettings
                {
                    AgentName = "Code Merger",
                    ApiProvider = AbstractAgentHandler.ApiProviders.Gemini,
                    ModelName = Sanat.ApiGemini.Model.Pro.Name
                }},
                { "AgentCodeValidator", new AgentModelSettings
                {
                    AgentName = "Code Validator",
                    ApiProvider = AbstractAgentHandler.ApiProviders.Anthropic,
                    ModelName = ApiAnthropic.Model.Haiku35Latest.Name
                }}
            };
        }

        public void LoadAgentModelSettings(CodeGenerator codeGenerator)
        {
            string settingsJson = EditorPrefs.GetString("AgentModelSettings", "");
            if (!string.IsNullOrEmpty(settingsJson))
            {
                try
                {
                    SerializableAgentModelSettingsList loadedSettings = JsonUtility.FromJson<SerializableAgentModelSettingsList>(settingsJson);
                    if (loadedSettings != null && loadedSettings.settings != null)
                    {
                        foreach (var setting in loadedSettings.settings)
                        {
                            string agentKey = GetAgentKey(setting.AgentName);
                            if (codeGenerator.agentModelSettings.ContainsKey(agentKey))
                            {
                                codeGenerator.agentModelSettings[agentKey] = setting;
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error loading agent model settings: {e.Message}");
                }
            }
        }
        
        public void SaveAgentModelSettings(CodeGenerator codeGenerator)
        {
            string settingsJson = JsonUtility.ToJson(new SerializableAgentModelSettingsList(codeGenerator.agentModelSettings));
            EditorPrefs.SetString("AgentModelSettings", settingsJson);
        }
        
        [System.Serializable]
        private class SerializableAgentModelSettingsList
        {
            public List<AgentModelSettings> settings;

            public SerializableAgentModelSettingsList(Dictionary<string, AgentModelSettings> dict)
            {
                settings = dict.Values.ToList();
            }

            public Dictionary<string, AgentModelSettings> ToDictionary()
            {
                return settings.ToDictionary(s => GetAgentKey(s.AgentName));
            }
        }

        private static string GetAgentKey(string agentName)
        {
            switch (agentName)
            {
                case "High Level Architector": return "AgentCodeHighLevelArchitector";
                case "Code Architector": return "AgentCodeArchitector";
                case "Code Merger": return "AgentCodeMerger";
                case "Code Validator": return "AgentCodeValidator";
                default: return agentName;
            }
        }
    }
}