// Copyright (c) Sanat. All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sanat.ApiOpenAI;
using Sanat.ApiAnthropic;
using Sanat.ApiGemini;
using Sanat.ApiGroq;
using UnityEngine;
using UnityEngine.Networking;
using ChatMessage = Sanat.ApiGemini.ChatMessage;
using ChatResponse = Sanat.ApiOpenAI.ChatResponse;
using Sanat.ApiAnthropic;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ChatRequest = Sanat.ApiGemini.ChatRequest;

namespace Sanat.CodeGenerator.Agents
{
    public abstract class AbstractAgentHandler
    {
        public string Name { get; set; }
        public string DebugName { get; set; }
        public string Description { get; set; }
        public string[] Tools { get; set; }
        public float Temperature { get; set; }
        public string PromptFromMdFile { get; set; }
        public Dictionary<string, string> ClassToPath { get; set; }
        public ApiProviders SelectedApiProvider = ApiProviders.Anthropic;
        public ApiKeys Apikeys;
        public Action<string> OnComplete;
        public Action OnUnsuccessfull;
        public const string PROMPTS_SAVE_FOLDER = "Sanat/CodeGenerator/Prompts";
        public const string RESULTS_SAVE_FOLDER = "Sanat/CodeGenerator/Results";
        public const string PROMPTS_FOLDER_PATH = "/Sanat/CodeGenerator/Editor/Agents/Prompts/";
        public const string MEMORY_FOLDER_PATH = "/Sanat/CodeGenerator/Editor/Agents/Prompts/Memory/";
        public const string KEY_FIGURE_OPEN = "[figureOpen]";
        public const string KEY_FIGURE_CLOSE = "[figureClose]";
        protected List<FileContent> _projectCode = new ();

        public enum ApiProviders { OpenAI, Anthropic, Groq, Gemini }
        public readonly string CSV_SEPARATOR = "[CSV_SEPARATOR]";
        public enum Brackets { round, square, curly, angle }

        protected bool _isModelChanged;
        protected bool _isChangedModelOpenai;
        protected Sanat.ApiOpenAI.Model _newOpenaiModel;
        protected bool _isChangedModelGemini;
        protected bool _isChangedModelAnthropic;
        protected string _newGeminiModel;
        protected string _modelName;
        public int ConversationHistoryMemory { get; set; } = 1;
        protected HttpClient httpClient;
        protected List<string> _selectedMemoryFiles = new List<string>();

        public void SaveResultToFile(string result)
        {
            string directoryPath = Path.Combine(Application.dataPath, RESULTS_SAVE_FOLDER);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            string fileName = $"result_{Name}_{DateTime.Now:yyyy-MM-ddTHH-mm-ss-fff}.txt";
            string filePath = Path.Combine(directoryPath, fileName);
            File.WriteAllText(filePath, result);
            Debug.Log($"Result saved to: {filePath}");
        }

        public static void SavePromptToFile(string promptBody, string agentName = "")
        {
            string directoryPath = Path.Combine(Application.dataPath, PROMPTS_SAVE_FOLDER);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            string fileName = $"prompt_{DateTime.Now:yyyy-MM-ddTHH-mm-ss}.txt";
            string filePath = Path.Combine(directoryPath, fileName);
            File.WriteAllText(filePath, promptBody);
            Debug.Log($"{agentName}: Prompt of Length = {promptBody.Length} chars   saved to {filePath}");
        }
        
        public AbstractAgentHandler SetNext(AbstractAgentHandler handler)
        {
            _nextHandler = handler;
            return handler;
        }

        protected AbstractAgentHandler _nextHandler;
        protected abstract string PromptFilename();

        protected void StoreOpenAIKey(string key)
        {
            Apikeys.openAI = key;
        }
        
        public void StoreKeys(ApiKeys keys) => Apikeys = keys;

        protected virtual ApiOpenAI.Model GetModel() => ApiOpenAI.Model.GPT4omini;
        
        protected virtual string GetGeminiModel() => ApiGemini.Model.Flash.Name;
        
        public static string LoadPrompt(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error reading .md file: {{e.Message}}");
                }
            }
            else
            {
                Debug.LogError($"The .md file does not exist in the specified path.[{{path}}]");
            }
            return String.Empty;
        }

        public virtual void Handle(string input)
        {
            if (input.Contains("HTTP/1.1 400 Bad Request"))
            {
                OnUnsuccessfull?.Invoke();
                return;
            }
            
            if (input.Contains("HTTP/1.1 401 Unauthorized"))
            {
                OnUnsuccessfull?.Invoke();
                return;
            }
            OnComplete?.Invoke(input);
        }

        public class BotParameters
        {
            public string systemMessage;
            public string prompt;
            public ApiProviders apiProvider;
            public float temp;
            public string modelName;
            public Action<string> onComplete;
            public Action<ToolCalls> onOpenaiToolComplete;
            public Action<CompletionResponse> onOpenaiChatResponseComplete;
            public ApiGemini.ChatRequest geminiRequest;
            public ApiOpenAI.Tool[] openaiTools;
            
            public ApiAntrophicData.ChatRequest antrophicRequest;
            public Action<ApiAntrophicData.ChatResponse> onAntrophicChatResponseComplete;
            public bool isToolUse = false;
            
            public BotParameters(string prompt, ApiProviders apiProvider, float temp, Action<string> onComplete, string modelName = null, bool isToolUse = false)
            {
                this.prompt = prompt;
                this.apiProvider = apiProvider;
                this.temp = temp;
                this.onComplete = onComplete;
                this.modelName = modelName;
                this.isToolUse = isToolUse;
            }
        }

        public void AskGroqTool(BotParameters botParameters)
        {
            List<ApiOpenAI.ChatMessage> messages = new List<ApiOpenAI.ChatMessage>();
            messages.Add(new ApiOpenAI.ChatMessage("user", botParameters.prompt));
            ApiOpenAI.Model model = ApiGroqModels.GetModelByName(_modelName);
            UnityWebRequestAsyncOperation request = Groq.SubmitToolChatAsync(
                Apikeys.groq,
                model,
                botParameters.temp,
                1,
                0,
                0,
                model.MaxOutputTokens,
                messages,
                botParameters.onOpenaiChatResponseComplete,
                botParameters.openaiTools
            );
        }

        public void AskChatGptTool(BotParameters botParameters)
        {
            List<ApiOpenAI.ChatMessage> messages = new List<ApiOpenAI.ChatMessage>();
            messages.Add(new ApiOpenAI.ChatMessage("user", botParameters.prompt));
            ApiOpenAI.Model model = ApiOpenAI.Model.GetModelByName(_modelName);
            UnityWebRequestAsyncOperation request = OpenAI.SubmitToolChatAsync(
                Apikeys.openAI,
                model,
                botParameters.temp,
                1,
                0,
                0,
                model.MaxOutputTokens,
                messages,
                botParameters.onOpenaiChatResponseComplete,
                botParameters.openaiTools
            );
        }
        
        public void AskBot(BotParameters botParameters) {
            switch (botParameters.apiProvider)
            {
                case ApiProviders.OpenAI:
                    if (botParameters.isToolUse)
                    {
                        AskChatGptTool(botParameters);
                    }
                    else
                    {
                        AskChatGpt(botParameters.prompt, botParameters.temp, botParameters.onComplete);
                    }
                    break;
                case ApiProviders.Anthropic:
                    AskAntrophic(botParameters.antrophicRequest, botParameters.onAntrophicChatResponseComplete);
                    break;
                case ApiProviders.Groq:
                    if (botParameters.isToolUse)
                    {
                        AskGroqTool(botParameters);
                    }
                    else
                    {
                        AskGroq(botParameters.prompt, botParameters.temp, botParameters.onComplete);
                    }
                    break;
                case ApiProviders.Gemini:
                    AskGemini(botParameters.geminiRequest, botParameters.onComplete);
                    break;
            }
        }

        public void AskChatGpt(string prompt, float temp, Action<string> onComplete, List<ApiOpenAI.Tool> tools = null) {
            List<ApiOpenAI.ChatMessage> messages = new List<ApiOpenAI.ChatMessage>();
            messages.Add(new ApiOpenAI.ChatMessage("user", prompt));
            ApiOpenAI.Model model = ApiOpenAI.Model.GetModelByName(_modelName);
            
            UnityWebRequestAsyncOperation request = OpenAI.SubmitChatAsync(
                Apikeys.openAI,
                model,
                temp,
                model.MaxOutputTokens,
                messages,
                onComplete
            );
        }
        
        public void AskAntrophic(ApiAntrophicData.ChatRequest chatRequest, Action<ApiAntrophicData.ChatResponse> onComplete) {
            UnityWebRequestAsyncOperation request = Anthropic.SubmitChatAsync(
                Apikeys.antrophic,
                chatRequest,
                onComplete
            );
        }
        
        public void AskGroq(string prompt, float temp, Action<string> onComplete) {
            List<ApiOpenAI.ChatMessage> messages = new List<ApiOpenAI.ChatMessage>();
            messages.Add(new ApiOpenAI.ChatMessage("user", prompt));
            UnityWebRequestAsyncOperation request = Groq.SubmitChatAsync(
                Apikeys.groq,
                ApiGroqModels.Llama3_70b_8192_tool,
                temp,
                8192,
                messages,
                onComplete
            );
        }
        
        public void AskGemini(ChatRequest chatRequest, Action<string> onComplete) {
            Gemini.SubmitChatAsync(Apikeys.gemini, _modelName, chatRequest, onComplete);
        }
        
        protected string LoadMemoryContent()
        {
            if (_selectedMemoryFiles == null || _selectedMemoryFiles.Count == 0)
                return string.Empty;
            
            string memoryContent = "\n\n# MEMORY FILES:\n";
            string memoryFolderPath = Application.dataPath + MEMORY_FOLDER_PATH;
        
            foreach (string fileName in _selectedMemoryFiles)
            {
                string filePath = Path.Combine(memoryFolderPath, fileName + ".md");
                if (File.Exists(filePath))
                {
                    try
                    {
                        string content = File.ReadAllText(filePath);
                        memoryContent += $"\n## {fileName}:\n{content}\n";
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error loading memory file {filePath}: {ex.Message}");
                    }
                }
            }
        
            return memoryContent;
        }
        
        public static string ClearResult(string input, Brackets bracket = Brackets.square)
        {
            string pattern = @"(\[.*\])";
            switch (bracket)
            {
                case Brackets.round:
                    pattern = @"(\(.*\))";
                    break;
                case Brackets.curly:
                    pattern = @"(\{.*\})";
                    break;
                case Brackets.angle:
                    pattern = @"(\<.*\>)";
                    break;
            }
            Match match = Regex.Match(input, pattern, RegexOptions.Singleline);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            Debug.LogError("No match found");
            return input;
        }

        public void ChangeLLM(ApiProviders provider, string modelName)
        {
            SelectedApiProvider = provider;
            _modelName = modelName;
            _isModelChanged = true;
            switch (provider)
            {
                case ApiProviders.OpenAI:
                    _isChangedModelAnthropic = false;
                    _isChangedModelOpenai = true;
                    _isChangedModelGemini = false;
                    break;
                case ApiProviders.Gemini:
                    _isChangedModelAnthropic = false;
                    _isChangedModelOpenai = false;
                    _isChangedModelGemini = true;
                    break;
                case ApiProviders.Anthropic:
                    _isChangedModelAnthropic = true;
                    _isChangedModelOpenai = false;
                    _isChangedModelGemini = false;
                    break;
            }
        }

        public struct ApiKeys
        {
            public string openAI;
            public string antrophic;
            public string groq;
            public string gemini;
            
            public ApiKeys(string openAI, string antrophic, string groq, string gemini)
            {
                this.openAI = openAI;
                this.antrophic = antrophic;
                this.groq = groq;
                this.gemini = gemini;
            }
        }
        
        [Serializable]
        public class FileContent
        {
            public string FilePath { get; set; }
            public string Content { get; set; }
        }
        
        [Serializable]
        public class FileTasks
        {
            public string FilePath { get; set; }
            public int TaskId { get; set; }
        }

        [Serializable]
        public class TechnicalSpecification
        {
            public string FilePathes;
            public string Solution;
        }
    }
}