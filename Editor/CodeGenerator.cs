// Copyright (c) Sanat. All rights reserved.
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Sanat.ApiGemini;
using Sanat.ApiOpenAI;
using Sanat.CodeGenerator.Agents;
using Sanat.CodeGenerator.Editor.Bookmarks;
using Sanat.CodeGenerator.CodebaseRag;
using Sanat.CodeGenerator.Editor;
using Sanat.CodeGenerator.Extensions;

namespace Sanat.CodeGenerator
{
    public class CodeGenerator : EditorWindow
    {
        #region Fields
        private CodeGeneratorSettingsManager _settingsManager;
        private CodeGeneratorUIRenderer _uiRenderer;
        private CodeGeneratorPreparationHelper _preparationHelper;
        private RagProcessor _ragProcessor;
        public MemoryFileSelectionUI memoryFileSelectionUI;
        public bool IsFirstOnEnableCall => _isFirstOnEnableCall;
        private static bool _isFirstOnEnableCall = true;
        
        // UI State
        public Vector2 scrollPosition;
        public Vector2 taskScrollPosition;
        public string classNameInput = "";
        public string taskInput = "";
        public bool isSettingsVisible;
        public bool isAgentSettingsVisible;
        public Dictionary<string, AgentModelSettings> agentModelSettings = new();
        public bool IsSettingsLoaded;

        // API Keys/Settings  
        public string geminiApiKey, openaiApiKey, antrophicApiKey, groqApiKey;
        public string supabaseUrl, supabaseKey;
        public string tableName;
        public bool isRag;
        public string GeminiProjectName;

        // Generation State
        public string generatedPrompt = "";
        public bool isGeneratingCode;
        public float generationProgress;
        private float lastProgressUpdateTime;
        private float targetProgress;
        private float progressSpeed = .000001f;
        private const int MAX_ALLOWED_VALIDITY_CHECKINGS = 3;
        private int _currentRun;

        // Button Animation
        private float buttonAnimationTime = 1f;
        private float buttonAnimationProgress;
        public bool isButtonAnimating;
        public Color buttonColor = Color.green;

        // Project Data
        public Dictionary<string, string> projectCode;
        public List<string> selectedClassNames = new();
        public Dictionary<string, string> classToPath = new();
        public List<string> _ignoredFolders = new();
        public List<IncludedFolder> includedFolders = new();
        public static IncludedFoldersManager IncludedFoldersManager;

        // Constants
        private const string PLUGIN_NAME = "Code Generator ";
        public const string PREFS_GEMINI_PROJECT_NAME = "GeminiProjectName";
        public const string PREFS_KEY_TASK = "Task";
        private const string PREFS_KEY_LAST_UPDATE_TIME = "LastUpdateTime";
        public const string INCLUDED_FOLDERS_PREFS_KEY = "IncludedFolders";

        // Managers
        public CodeGeneratorBookmarks bookmarkManager;
        #endregion

        public RagProcessor ragProcessor;

        [System.Serializable]
        public class IncludedFolder
        {
            public string path;
            public bool isEnabled;
        }

        [MenuItem("Tools/Sanat/CodeGenerator/CodeGenerator window")]
        public static void ShowWindow() => GetWindow<CodeGenerator>("Code Generator");

        private async void OnEnable()
        {
            InitializeManagers();
            
            await LoadSettingsDelayedAsync();
            // if (_isFirstOnEnableCall)
            // { <== causes editor crash
            //     await LoadSettingsDelayedAsync();
            //     _isFirstOnEnableCall = false;
            // }
            // else
            // {
            //     await _settingsManager.LoadSettings(this);
            // }
        }

        private async UniTask LoadSettingsDelayedAsync()
        {
            //if last update time greater than 2 hours ago then set delay time to 10, else to 0
            await UniTask.Delay(System.TimeSpan.FromSeconds(10), DelayType.DeltaTime, PlayerLoopTiming.Update);
            await _settingsManager.LoadSettings(this);
        }
        
        private void OnGUI() => _uiRenderer.RenderMainUI(this);

        private void OnDisable() => _settingsManager.SaveSettings(this);
        
        private void InitializeManagers()
        {
            _settingsManager = new CodeGeneratorSettingsManager();
            _uiRenderer = new CodeGeneratorUIRenderer();
            _preparationHelper = new CodeGeneratorPreparationHelper();
            _ragProcessor = new RagProcessor();
        }
        
        private async void ProcessAllFilesForRag()
        {
            string projectPath = Application.dataPath;
            EditorUtility.DisplayProgressBar("Processing Files", "Starting...", 0f);
            try
            {
                await ragProcessor.ProcessAllFiles(projectPath);
                EditorUtility.DisplayDialog("RAG Processing Complete", "All files have been processed for RAG.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing files for RAG: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred while processing files for RAG: {e.Message}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region Code Generation
        public void ExecGeneratePrompt()
        {
            projectCode = _preparationHelper.PrepareProjectCode(selectedClassNames, classToPath, _ignoredFolders);
            SaveTaskToPrefs();
            var prompt = GeneratePrompt(3);
            AbstractAgentHandler.SavePromptToFile(prompt, "AgentCodeHighLevelArchitector");
            CopyPromptToClipboard();
            StartButtonAnimation();
        }

        private void SaveTaskToPrefs() => EditorPrefs.SetString(PREFS_KEY_TASK, taskInput);
        
        public void ExecGenerateCode()
        {
            projectCode = _preparationHelper.PrepareProjectCode(selectedClassNames, classToPath, _ignoredFolders);
            InitiateGeneration();
        }

        private void InitiateGeneration()
        {
            isGeneratingCode = true;
            _currentRun = 0;
            generationProgress = 0f;
            targetProgress = .33f;
            lastProgressUpdateTime = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdateProgressBar;
            SaveTaskToPrefs();
            string[] projectCodePathes = projectCode.Keys.ToArray();
            var prompt = GeneratePrompt();
            AbstractAgentHandler.SavePromptToFile(prompt, "AgentCodeHighLevelArchitector");
            CopyPromptToClipboard();
            StartButtonAnimation();
            var includedCode = _preparationHelper.GenerateIncludedCode(projectCode, "");
            InitiateCodeGeneration(projectCodePathes, includedCode);
        }

        private void InitiateCodeGeneration(string[] projectCodePaths, string includedCode)
        {
            var apiKeys = new AbstractAgentHandler.ApiKeys(openaiApiKey, antrophicApiKey, groqApiKey, geminiApiKey);
    
            List<AbstractAgentHandler.FileContent> projectCodeScope = new ();
            foreach(var path in projectCodePaths)
            {
                if(File.Exists(path))
                {
                    AbstractAgentHandler.FileContent fileContent = new ();
                    fileContent.FilePath = path;
                    fileContent.Content = File.ReadAllText(path);
                    projectCodeScope.Add(fileContent);
                }
            }
            List<string> selectedMemoryFiles = null;
            if (agentModelSettings.TryGetValue("AgentCodeHighLevelArchitector", out var highLevelArchSettings))
            {
                selectedMemoryFiles = highLevelArchSettings.SelectedMemoryFiles;
            }
            var agentCodeHighLevelArchitector = new AgentCodeHighLevelArchitector(apiKeys, taskInput, includedCode, selectedMemoryFiles);
            var agentCodeArchitector = new AgentCodeArchitector(apiKeys, taskInput, projectCodeScope);
            var architectorSettings = agentModelSettings["AgentCodeArchitector"];
            agentCodeArchitector.ChangeLLM(architectorSettings.ApiProvider, architectorSettings.ModelName);
    
    
            var agentCodeMerger = new AgentCodeMerger(apiKeys, projectCodeScope, projectCodePaths); // Using the correct constructor
            // Set up callbacks and chain
            SetupArchitectorCallbacks(agentCodeArchitector, agentCodeMerger, apiKeys, agentCodeHighLevelArchitector, includedCode);
    
            // Start the generation process with initial prompt
            agentCodeHighLevelArchitector.SetNext(null);
            agentCodeHighLevelArchitector.Handle(generatedPrompt);
        }
        #endregion

        private void SetupArchitectorCallbacks(
            AgentCodeArchitector agentCodeArchitector,
            AgentCodeMerger agentCodeMerger, 
            AbstractAgentHandler.ApiKeys apiKeys,
            AgentCodeHighLevelArchitector agentCodeHighLevelArchitector,
            string includedCode)
        {
            UpdateProgress(0.1f);
            RemoveExistingEventHandlers(agentCodeArchitector, agentCodeMerger, agentCodeHighLevelArchitector);

            agentCodeHighLevelArchitector.OnTextAnswerProvided += (string techSpec) =>
            {
                if (!isGeneratingCode) return;
                UpdateProgress(0.3f);
                agentCodeArchitector.ChangeTask(techSpec);
                agentCodeArchitector.OnFileContentProvided = null;
                agentCodeArchitector.OnFileContentProvided += (List<AbstractAgentHandler.FileContent> fileContents) =>
                {
                    if (!isGeneratingCode) return;
                    AgentCodeValidator agentValidator = new AgentCodeValidator(apiKeys, taskInput, classToPath, includedCode, "", agentCodeMerger as AgentCodeMerger);
                    
                    UpdateProgress(0.8f);
                    agentValidator.ValidateSolution(fileContents, (string comment) =>
                    {
                        string architectorSolution = JsonConvert.SerializeObject(fileContents);
                        NextStepsAfterArchitector(comment, agentCodeMerger, apiKeys, architectorSolution, 
                            agentCodeHighLevelArchitector, agentCodeArchitector, includedCode);
                        UpdateProgress(1f);
                    });
                };
                agentCodeArchitector.SetNext(null);
                agentCodeArchitector.Handle(techSpec);
            };
            
            // Configure architect's completion handling
            agentCodeArchitector.OnComplete += (string result) =>
            {
                // Create validator to check architect's output
                AgentCodeValidator agentValidator = new AgentCodeValidator(apiKeys, taskInput, classToPath, includedCode, result, agentCodeMerger as AgentCodeMerger);
                
                agentValidator.OnComplete += (string validationResult) =>
                {
                    NextStepsAfterArchitector(validationResult, agentCodeMerger, apiKeys, result, 
                        agentCodeHighLevelArchitector, agentCodeArchitector, includedCode);
                };
                // Start validation
                agentValidator.Handle(result);
            };
            // Configure merger completion to finish the process
            agentCodeMerger.OnComplete += (string mergedCode) => 
            { 
                Debug.Log($"Code generation completed. Result: {mergedCode}");
                FinishGenerationActions();
            };
            // Handle potential failures 
            agentCodeArchitector.OnUnsuccessfull += () =>
            {
                Debug.LogError("Code architect failed to generate solution");
                FinishGenerationActions();
            };
            
            agentCodeMerger.OnUnsuccessfull += () => 
            {
                Debug.LogError("Code merger failed to process solution");
                FinishGenerationActions();
             };
        }

        private static void RemoveExistingEventHandlers(AgentCodeArchitector agentCodeArchitector,
            AgentCodeMerger agentCodeMerger, AgentCodeHighLevelArchitector agentCodeHighLevelArchitector)
        {
            agentCodeHighLevelArchitector.OnTextAnswerProvided = null;
            agentCodeArchitector.OnFileContentProvided = null;
            agentCodeArchitector.OnComplete = null;
            agentCodeMerger.OnComplete = null;
            agentCodeArchitector.OnUnsuccessfull = null;
            agentCodeMerger.OnUnsuccessfull = null;
        }

        private AbstractAgentHandler NextStepsAfterArchitector(string validationResult,
            AbstractAgentHandler agentCodeMerger, 
            AbstractAgentHandler.ApiKeys apiKeys, 
            string architectorSolution, 
            AbstractAgentHandler agentCodeHighLevelArchitector,
            AgentCodeArchitector agentCodeArchitector,
            string includedCode) 
        {
            _currentRun++;
            if (_currentRun > MAX_ALLOWED_VALIDITY_CHECKINGS || !isGeneratingCode)
            {
                Debug.Log($"Max allowed validity checkings reached. validationResult: {validationResult}");
                FinishGenerationActions();
                return agentCodeMerger;
            }
            else
            {
                Debug.Log($"<color=red>Validation failed.</color> Returning to Architector. validationResult: {validationResult}");
                if (_currentRun == 2)
                {
                    UpdateProgress(0.4f);
                    //Debug.Log($"Current run: {_currentRun}; model: {Sanat.ApiAnthropic.Model.Claude35Latest}");
                    Debug.Log($"Current run: {_currentRun}; model: {ApiGemini.Model.Pro.Name}");
                    //agentCodeArchitector.ChangeLLM(AbstractAgentHandler.ApiProviders.Gemini, ApiGeminiModels.Pro);
                    //Debug.Log($"Current run: {_currentRun}; model: {Model.GPT4o_16K.Name}");
                    //agentCodeArchitector.ChangeLLM(AbstractAgentHandler.ApiProviders.OpenAI, Model.GPT4o_16K.Name);
                }else if (_currentRun == 3)
                {
                    UpdateProgress(0.6f);
                     //Debug.Log($"Current run: {_currentRun}; model: {Sanat.ApiAnthropic.Model.Claude35Latest}");
                    Debug.Log($"Current run: {_currentRun}; model: {ApiOpenAI.Model.GPT4o1mini.Name}");
                    //agentCodeArchitector.ChangeLLM(AbstractAgentHandler.ApiProviders.OpenAI, Model.GPT4o1mini.Name);
                }else if (_currentRun == 4)
                {
                    UpdateProgress(0.8f);
                     Debug.Log($"Current run: {_currentRun}; model: {ApiOpenAI.Model.GPT4o1mini.Name}");
                    //agentCodeArchitector.ChangeLLM(AbstractAgentHandler.ApiProviders.OpenAI, Model.GPT4o1mini.Name);
                }
                
                agentCodeArchitector.WorkWithFeedback(validationResult, architectorSolution, (string feedbackResult) =>
                {
                    Debug.Log($"Feedback result: {feedbackResult}");
                    AgentCodeValidator agentValidator = new AgentCodeValidator(apiKeys, taskInput, classToPath, includedCode, feedbackResult, agentCodeMerger as AgentCodeMerger);
                    agentValidator.OnComplete += (string validationResult) =>
                    {
                        Debug.Log($"{agentValidator.Name} OnComplete: {validationResult}");
                        NextStepsAfterArchitector(validationResult, agentCodeMerger, 
                            apiKeys, feedbackResult, agentCodeHighLevelArchitector, 
                            agentCodeArchitector, includedCode);
                    };
                    agentValidator.Handle(feedbackResult);
                });
            }
            
            return agentCodeMerger;
        }

        private void FinishGenerationActions()
        {
            UpdateProgress(1f);
            isGeneratingCode = false;
            _currentRun = 0;
            Repaint();
        }

        private string GeneratePrompt(int rowsToRemove = 0)
        {
            string includedCode = "";
            string includedCodeRaw = "";
            includedCodeRaw = GenerateIncludedCode(includedCodeRaw, ref includedCode);
            includedCode = Regex.Replace(includedCode, @"\s+", " ").Trim();
            string clearedCodeScriptForLLM = includedCodeRaw;
            string promptLocation = Application.dataPath + $"{AbstractAgentHandler.PROMPTS_FOLDER_PATH}AgentCodeHighLevelArchitector.md";
            string loadedPrompt = AbstractAgentHandler.LoadPrompt(promptLocation);
            string newPrompt = CodeGeneratorExtensions.RemoveLastNRows(loadedPrompt, rowsToRemove);
            string prompt = newPrompt + $"\n# CODE: {clearedCodeScriptForLLM}\n\n# TASK: {taskInput}";
            generatedPrompt = prompt;
            return clearedCodeScriptForLLM;
        }

        private string GenerateIncludedCode(string includedCodeRaw, ref string includedCode)
        {
            foreach (var kvPathToCode in projectCode)
            {
                foreach (string className in selectedClassNames)
                {
                    if (kvPathToCode.Key.Contains(className))
                    {
                        includedCodeRaw += $"// {kvPathToCode.Key}:\n {kvPathToCode.Value} \n\n";
                        includedCode += File.ReadAllText(kvPathToCode.Key).Replace("\n", "").Replace("\r", "") + "\n\n";
                    }
                }
            }
            
            var includedFolders = IncludedFoldersManager.GetEnabledFolders();
            foreach (var folder in includedFolders)
            {
                string[] files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    includedCodeRaw += $"// {file}:\n {File.ReadAllText(file)} \n\n";
                    includedCode += File.ReadAllText(file).Replace("\n", "").Replace("\r", "") + "\n\n";
                }
            }
            
            return includedCodeRaw;
        }

        #region Progress Bar
        private void UpdateProgress(float progress)
        {
            targetProgress = progress;
            if (progress >= 1f)
            {
                isGeneratingCode = false;
                EditorApplication.update -= UpdateProgressBar;
                Repaint();
            }
        }

        public void UpdateProgressBar()
        {
            float currentTime = (float)EditorApplication.timeSinceStartup;
            if (currentTime - lastProgressUpdateTime >= 0.1f)
            {
                generationProgress = Mathf.Max(generationProgress + progressSpeed, targetProgress);
                lastProgressUpdateTime = currentTime;
                Repaint();
            }
        }
        #endregion

        #region Settings
        public void DrawSettingsFields()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Gemini API Key", "GeminiApiKey", "https://aistudio.google.com/app/apikey", ref geminiApiKey);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("OpenAI API Key", "OpenaiApiKey", "https://platform.openai.com/api-keys", ref openaiApiKey);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Groq API Key", "GroqApiKey", "https://console.groq.com/keys", ref groqApiKey);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Antrophic API Key", "AntrophicApiKey", "https://console.anthropic.com/settings/keys", ref antrophicApiKey);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Supabase URL", "SupabaseUrl", "https://supabase.com/dashboard/projects/", ref supabaseUrl);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Supabase Key", "SupabaseKey", "https://supabase.com/dashboard/projects/", ref supabaseKey);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            //is rag field
            EditorGUILayout.LabelField("Is RAG", GUILayout.Width(150));
            isRag = EditorGUILayout.Toggle(isRag);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            InsertApiKeyRow("Table Name", "TableName", "https://supabase.com/dashboard/projects/", ref tableName);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Process All Files for RAG"))
            {
                ProcessAllFilesForRag();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ignored Folders:", EditorStyles.boldLabel);
            if (GUILayout.Button("Add Ignored Folder"))
            {
                _ignoredFolders.Add("");
            }
            for (int i = 0; i < _ignoredFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _ignoredFolders[i] = EditorGUILayout.TextField("Folder Path (Ex.: Assets\\Test)", _ignoredFolders[i], GUILayout.ExpandWidth(true));
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    _ignoredFolders.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gemini Project Name", GUILayout.Width(150));
            GeminiProjectName = EditorGUILayout.TextField(GeminiProjectName);
            EditorPrefs.SetString(Gemini.PREFS_GEMINI_PROJECT_NAME, GeminiProjectName);
            EditorGUILayout.EndHorizontal();
            
            
            EditorGUILayout.Space();
            IncludedFoldersManager.DrawIncludedFoldersUI();
            EditorGUILayout.EndVertical();
        }
        #endregion

        private void InsertApiKeyRow(string fieldName, string playerPrefsKey, string url, ref string apiKeyVariable)
        {
            EditorGUILayout.LabelField(fieldName, GUILayout.Width(150));
            apiKeyVariable = EditorGUILayout.PasswordField(apiKeyVariable);
            EditorPrefs.SetString(playerPrefsKey, apiKeyVariable);
            if (GUILayout.Button("Get Key", GUILayout.Width(70)))
            {
                Application.OpenURL(url);
            }
        }

        public void RefreshClassList()
        {
            Debug.Log($"{PLUGIN_NAME}Refreshing class list...");
            classToPath.Clear();
            Debug.Log($"{PLUGIN_NAME}Current ignored folders count: {_ignoredFolders.Count}");
            foreach (var ignoredFolder in _ignoredFolders)
            {
                Debug.Log($"{PLUGIN_NAME}Ignored folder path: {ignoredFolder}");
            }
            string[] csFiles = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Packages", "*.cs", SearchOption.AllDirectories))
                .Where(filePath => !_ignoredFolders.Any(ignoredFolder => filePath.Contains(ignoredFolder)))
                .ToArray();
            Debug.Log($"{PLUGIN_NAME}Found {csFiles.Length} .cs files.");
            foreach (string filePath in csFiles)
            {
                string className = Path.GetFileNameWithoutExtension(filePath);
                classToPath[className] = filePath;
            }
            Debug.Log($"{PLUGIN_NAME}Class to path dictionary populated with {classToPath.Count} entries.");
        }

        private void CopyPromptToClipboard()
        {
            TextEditor te = new TextEditor();
            te.text = generatedPrompt;
            te.SelectAll();
            te.Copy();
        }

        private void StartButtonAnimation()
        {
            isButtonAnimating = true;
            buttonAnimationProgress = 0f;
            buttonColor = Color.yellow;
            EditorApplication.update += UpdateButtonAnimation;
        }

        private void UpdateButtonAnimation()
        {
            buttonAnimationProgress += Time.deltaTime / buttonAnimationTime;
            buttonColor = Color.Lerp(Color.red, Color.green, Mathf.PingPong(buttonAnimationProgress * 2, 1));
            if (buttonAnimationProgress >= 1f)
            {
                isButtonAnimating = false;
                EditorApplication.update -= UpdateButtonAnimation;
            }
        }
        
        public void LoadBookmarkData(BookmarkData bookmark)
        {
            if (bookmark != null)
            {
                selectedClassNames = new List<string>(bookmark.selectedClassNames);
                taskInput = bookmark.task;
            }
        }

        public void CheckAndHandleFirstLaunch()
        {
            if (!IsFirstLaunch())
            {
                return;
            }
            Debug.Log($"{PLUGIN_NAME}First launch detected. Adding prompt save folder to .gitignore and ignore.conf files.");
            string assetsParentPath = Directory.GetParent(Application.dataPath).FullName;
            string gitignorePath = Path.Combine(assetsParentPath, ".gitignore");
            string ignoreConfPath = Path.Combine(assetsParentPath, "ignore.conf");
            string folderPathToAdd = Environment.NewLine + "/Assets/" + AbstractAgentHandler.PROMPTS_SAVE_FOLDER + Environment.NewLine;
            string resultsPathToAdd = Environment.NewLine + "/Assets/" + AbstractAgentHandler.RESULTS_SAVE_FOLDER + Environment.NewLine;
            string backupsPathToAdd = Environment.NewLine + "/" + BackupManager.BACKUP_ROOT + "/" + BackupManager.BACKUP_FOLDER_NAME + Environment.NewLine;
            if (File.Exists(gitignorePath))
            {
                if (RequiredExclusionsExist(gitignorePath, folderPathToAdd, resultsPathToAdd, backupsPathToAdd))
                {
                    return;
                }
                File.AppendAllText(gitignorePath, folderPathToAdd);
                File.AppendAllText(gitignorePath, resultsPathToAdd);
            }
            if (File.Exists(ignoreConfPath))
            {
                if (RequiredExclusionsExist(ignoreConfPath, folderPathToAdd, resultsPathToAdd, backupsPathToAdd))
                {
                    return;
                }
                File.AppendAllText(ignoreConfPath, folderPathToAdd);
                File.AppendAllText(ignoreConfPath, resultsPathToAdd);
            }
        }

        private bool RequiredExclusionsExist(string gitignorePath, string folderPathToAdd, string resultsPathToAdd, string backupsPathToAdd)
        {
            string gitignoreContents = File.ReadAllText(gitignorePath);
            if (gitignoreContents.Contains(folderPathToAdd) && gitignoreContents.Contains(resultsPathToAdd) && gitignoreContents.Contains(backupsPathToAdd))
            {
                return true;
            }
            return false;
        }

        private bool IsFirstLaunch()
        {
            const string key = "CODE_GENERATOR_FIRST_LAUNCH";
            bool isFirstLaunch = EditorPrefs.GetInt(key, 1) == 1;
            if (isFirstLaunch)
            {
                Debug.Log($"{PLUGIN_NAME} Detected First launch");
                EditorPrefs.SetInt(key, 0);
            }
            return isFirstLaunch;
        }
    }
}