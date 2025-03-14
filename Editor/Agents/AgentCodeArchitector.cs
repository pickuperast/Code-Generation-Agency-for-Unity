// Copyright (c) Sanat. All rights reserved.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sanat.ApiAnthropic;
using Sanat.ApiGemini;
using Sanat.ApiOpenAI;
using UnityEngine;
using Antrophic = Sanat.ApiAnthropic.ApiAntrophicData;
using GeminiChatRequest = Sanat.ApiGemini.ChatRequest;
using Model = Sanat.ApiGemini.Model;

namespace Sanat.CodeGenerator.Agents
{
    public class AgentCodeArchitector : AbstractAgentHandler
    {
        private string _prompt;
        private string _promptLocation;
        private string _task;
        public Action<List<FileTasks>> OnFileTasksProvided;
        public Action<List<FileContent>> OnFileContentProvided;
        public Action<string> OnJobFailed;
        private readonly string _systemInstructions;
        private AgentFunctionDefinitions _functionDefinitions;
        
        protected override string PromptFilename() => "PromptAgentArchitectorTool.md";
        protected string _systemInstructionSplitTaskToSingleFiles = "PromptAgentArchitector_SplitTasks.md";
        protected override Sanat.ApiOpenAI.Model GetModel()
        {
            if (_isModelChanged && _isChangedModelOpenai)
            {
                return _newOpenaiModel;
            }
            return Sanat.ApiOpenAI.Model.GPT4o1mini;
        }

        protected override string GetGeminiModel()
        {
            if (_isModelChanged && _isChangedModelGemini)
            {
                return _newGeminiModel;
            }
            return ApiGemini.Model.Pro.Name;
        }

        public AgentCodeArchitector(ApiKeys apiKeys, string task, List<FileContent> includedCodeFiles)
        {
            Name = "Agent Unity3D Architect";
            DebugName = $"<color=green>{Name}</color>";
            Description = "Writes code for agents";
            Temperature = .5f;
            _projectCode = includedCodeFiles;
            StoreKeys(apiKeys);
            _promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PromptFilename()}";
            _task = $"# TASK: {task}. # CODE: {JsonConvert.SerializeObject(includedCodeFiles)}";
            PromptFromMdFile = LoadPrompt(_promptLocation);
            _prompt = $"{PromptFromMdFile} # TASK: {task}. # CODE: {JsonConvert.SerializeObject(includedCodeFiles)}";
            _systemInstructions = $"{PromptFromMdFile} # PROJECT CODE: {JsonConvert.SerializeObject(includedCodeFiles)}";
            _functionDefinitions = new AgentFunctionDefinitions();
            SelectedApiProvider = ApiProviders.Gemini;
            SelectedApiProvider = ApiProviders.Anthropic;
            _modelName = ApiAnthropic.Model.Claude37.Name;
        }

        public void ChangeTask(string task) => _task = task;
        
        public async Task SplitTaskToSingleFiles()
        {
            await Task.Delay(100);
            SelectedApiProvider = ApiProviders.Anthropic;
            _modelName = ApiAnthropic.Model.Claude35.Name;
            Debug.Log($"{DebugName} asking [{SelectedApiProvider}][{_modelName}]: {_task}");
            var fileTasks = new List<FileTasks>();
            BotParameters botParameters = new BotParameters(_task, SelectedApiProvider, Temperature, null, _modelName, true);
            string systemPrompt = PrepareSystemPrompt(_systemInstructionSplitTaskToSingleFiles);
            SavePromptToFile(systemPrompt, "AgentCodeArchitector_SplitTaskToSingleFiles");
            switch (botParameters.apiProvider)
            {
                case ApiProviders.OpenAI:
                    ToolSplitTaskToSingleFilesOpenAI(botParameters, systemPrompt);
                    break;
                case ApiProviders.Gemini:
                    ToolSplitTaskToSingleFilesGemini(botParameters, systemPrompt);
                    break;
                case ApiProviders.Anthropic:
                    ToolSplitTaskToSingleFilesAntrophic(botParameters, systemPrompt);
                    break;
            }
            AskBot(botParameters);
        }

        private void ToolSplitTaskToSingleFilesOpenAI(BotParameters botParameters, string systemPrompt)
        {
            var openaiTools = new ApiOpenAI.Tool[] { new("function", _functionDefinitions.GetFunctionData_OpenaiSplitTaskToSingleFiles()) }; // Use the new class
            botParameters.isToolUse = true;
            botParameters.openaiTools = openaiTools;
            botParameters.systemMessage = systemPrompt;
            botParameters.onOpenaiChatResponseComplete += (response) =>
            {
                Debug.Log($"{DebugName} {AgentFunctionDefinitions.TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES} Result: {response}");
                if (response.choices[0].finish_reason == "tool_calls")
                {
                    ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                    int filesAmount = toolCalls.Length;
                    List<FileTasks> fileTasks = new List<FileTasks>();
                    string logFileNames = $"<color=green>{Name}</color>: ";

                    for (int i = 0; i < filesAmount; i++)
                    {
                        SaveResultToFile(toolCalls[i].function.arguments);
                        fileTasks.Add(JsonConvert.DeserializeObject<FileTasks>(toolCalls[i].function.arguments));
                        logFileNames += $"{fileTasks[i].FilePath}, ";
                        if (i == filesAmount - 1)
                        {
                            logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        }
                    }
                    Debug.Log(logFileNames);
                    ReportFunctionResult_SplitTaskToSingleFiles(fileTasks);
                }
            };
        }

        private void ToolSplitTaskToSingleFilesGemini(BotParameters botParameters, string systemPrompt)
        {
            var geminiTools = new List<ApiGemini.Tool> { new() { function_declarations = new List<ApiGemini.FunctionDeclaration>() { _functionDefinitions.GetFunctionData_GeminiSplitTaskToSingleFiles() } } };
            botParameters.geminiRequest = new GeminiChatRequest(_task, Temperature)
            {
                tools = geminiTools,
                tool_config = new ToolConfig { function_calling_config = new FunctionCallingConfig { mode = "any", AllowedFunctionNames = new List<string> { AgentFunctionDefinitions.TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES } } }
            };
            botParameters.geminiRequest.system_instruction = new Content { parts = new List<Part> { new Part { text = systemPrompt } } };
            botParameters.onComplete += (result) =>
            {
                if (string.IsNullOrEmpty(result)) return;
                var responseData = JsonConvert.DeserializeObject<ApiGemini.GenerateContentResponse>(result);
                var fileTasks = new List<FileTasks>();
                if (responseData.candidates != null && responseData.candidates.Count > 0)
                {
                    var candidate = responseData.candidates[0];
                    foreach (var part in candidate.content.parts)
                    {
                        if (part.functionCall != null)
                        {
                            SaveResultToFile(JsonConvert.SerializeObject(part.functionCall.args));
                            var fileContent = JsonConvert.DeserializeObject<FileTasks>(JsonConvert.SerializeObject(part.functionCall.args));
                            fileTasks.Add(fileContent);
                        }
                    }
                    if (fileTasks.Count > 0) ReportFunctionResult_SplitTaskToSingleFiles(fileTasks);
                }
            };
        }
        
        private void ToolSplitTaskToSingleFilesAntrophic(BotParameters botParameters, string systemPrompt)
        {
            var antrophicTools = new[] { _functionDefinitions.GetFunctionData_AntrophicSplitTaskToSingleFiles() };
            Sanat.ApiAnthropic.Model model = ApiAnthropic.Model.GetModelByName(_modelName);
            
            List<Antrophic.ChatMessage> messages = new List<Antrophic.ChatMessage>
            {
                new("assistant", systemPrompt),
                new("user", _task)
            };
            
            botParameters.antrophicRequest = new Antrophic.ChatRequest(_modelName, .2f, messages, antrophicTools, model.MaxOutputTokens);
            botParameters.antrophicRequest.tool_choice = new Antrophic.ToolChoice { type = "any" };
            
            botParameters.onAntrophicChatResponseComplete += (response) =>
            {
                Debug.Log($"{DebugName} Working on SplitTaskToSingleFiles Result...");
                if (response.type == "error")
                {
                    Debug.LogError($"{DebugName} error[{response.error.type}]; message: {response.error.message}");
                    OnJobFailed?.Invoke(response.error.message);
                    return;
                }
                
                if (response.stop_reason == "tool_use")
                {
                    List<FileTasks> fileTasks = new List<FileTasks>();
                    string logFileNames = $"{Name}: ";
                    
                    foreach (var responseContent in response.content)
                    {
                        if (responseContent.type == "tool_use" && 
                            responseContent.name == AgentFunctionDefinitions.TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES)
                        {
                            FileTasks fileTask = new FileTasks();
                            foreach (var keyValuePair in responseContent.input)
                            {
                                switch (keyValuePair.Key)
                                {
                                    case "FilePath":
                                        fileTask.FilePath = keyValuePair.Value;
                                        break;
                                    case "TaskId":
                                        if (int.TryParse(keyValuePair.Value, out int taskId))
                                        {
                                            fileTask.TaskId = taskId;
                                        }
                                        break;
                                }
                            }
                            
                            SaveResultToFile($"{fileTask.FilePath}:{fileTask.TaskId}");
                            Debug.Log($"{DebugName} tool_use: {responseContent.name}({fileTask.FilePath}, {fileTask.TaskId})");
                            fileTasks.Add(fileTask);
                            logFileNames += $"{fileTask.FilePath}, ";
                        }
                    }
                    
                    if (fileTasks.Count > 0)
                    {
                        logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        Debug.Log(logFileNames);
                        ReportFunctionResult_SplitTaskToSingleFiles(fileTasks);
                    }
                    else
                    {
                        Debug.LogError($"{DebugName} No file tasks received");
                        OnJobFailed?.Invoke("No file tasks received");
                    }
                }
                else
                {
                    Debug.LogError($"{DebugName} Unexpected response stop reason: {response.stop_reason}");
                    OnJobFailed?.Invoke($"Unexpected response stop reason: {response.stop_reason}");
                }
            };
        }

        private async Task ReportFunctionResult_SplitTaskToSingleFiles(List<FileTasks> fileTasks)
        {
            if (fileTasks.Count > 0)
            {
                Debug.Log($"{DebugName} fileTasks: {fileTasks.Count}");
                string techSpec = _task;
                OnFileTasksProvided?.Invoke(fileTasks);
                // SelectedApiProvider = ApiProviders.OpenAI;
                // _modelName = ApiOpenAI.Model.GPT4omini.Name;
                SelectedApiProvider = ApiProviders.Gemini;
                _modelName = Model.ProExp.Name;
                for (int i = 0; i < fileTasks.Count; i++)
                {
                    var fileTask = fileTasks[i];
                    int taskId = fileTask.TaskId;
                    string task = String.Empty;
                    string currentCode = String.Empty;
                    switch (taskId)
                    {
                        case 0:
                            task = $"Modify file located at: {fileTask.FilePath}.\n# TECHNICAL SPECIFICATION: {techSpec}";
                            currentCode = $"# PROJECT CODE: {JsonConvert.SerializeObject(_projectCode)}";
                            break;
                        case 1:
                            task = $"Create new file at: {fileTask.FilePath}.\n# TECHNICAL SPECIFICATION: {techSpec}";
                            break;
                    }
                    Debug.Log($"{DebugName} Task #{i}: {task}");
                    ChangeTask(task);
                    string systemPrompt = $"{PromptFromMdFile} {currentCode}";
                    WriteCode(null, i, systemPrompt, task);
                    await Task.Delay(100);
                }
            }
            else
            {
                Debug.LogError($"{DebugName} No file content received");
                OnJobFailed?.Invoke("No file content received");
            }
        }

        public override void Handle(string input)
        {
            SplitTaskToSingleFiles();
        }
        
        private async Task WriteCode(List<Antrophic.ChatMessage> additionalMessages, int taskId, string systemPrompt = "", string task = "")
        {
            await Task.Delay(100);
            if (task == "") task = _task;
            if (systemPrompt == "") systemPrompt = PrepareSystemPrompt();
            Debug.Log($"{DebugName} Task #{taskId} [WriteCode] asking [{SelectedApiProvider}][{_modelName}]: {task}");
            SaveResultToFile($"{systemPrompt} \n# TASK: {task}");
            List<FileContent> fileContents = new List<FileContent>();
            BotParameters botParameters = new BotParameters(task, SelectedApiProvider, Temperature, null, _modelName, true);
            switch (botParameters.apiProvider)
            {
                case ApiProviders.OpenAI:
                    ToolHandlingOpenAI(botParameters, systemPrompt, DebugName, taskId);
                    break;
                case ApiProviders.Gemini:
                    ToolHandlingGemini(botParameters, systemPrompt, DebugName, taskId);
                    break;
                case ApiProviders.Anthropic:
                    ToolHandlingAntrophic(additionalMessages, systemPrompt, DebugName, fileContents, taskId);
                    break;
            }

            AskBot(botParameters);
        }
        #region Tools OpenAI
        private void ToolHandlingOpenAI(BotParameters botParameters, string systemPrompt, string agentName, int taskId)
        {
            var openaiTools = new ApiOpenAI.Tool[] { new("function", _functionDefinitions.GetFunctionData_OpenaiSReplaceScriptFile()) }; // Use the new class
            botParameters.isToolUse = true;
            botParameters.openaiTools = openaiTools;
            botParameters.systemMessage = systemPrompt;
            botParameters.onOpenaiChatResponseComplete += (response) =>
            {
                Debug.Log($"{agentName} {AgentFunctionDefinitions.TOOL_NAME_REPLACE_SCRIPT_FILE} Result: {response}");
                if (response.choices[0].finish_reason == "tool_calls")
                {
                    ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                    int filesAmount = toolCalls.Length;
                    List<FileContent> fileContents = new List<FileContent>();
                    string logFileNames = $"<color=green>{Name}</color>: ";

                    for (int i = 0; i < filesAmount; i++)
                    {
                        SaveResultToFile(toolCalls[i].function.arguments);
                        fileContents.Add(JsonConvert.DeserializeObject<FileContent>(toolCalls[i].function.arguments));
                        logFileNames += $"{fileContents[i].FilePath}, ";
                        if (i == filesAmount - 1)
                        {
                            logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        }
                    }
                    Debug.Log(logFileNames);
                    ReportFunctionResult_ReplaceScriptFiles(fileContents, agentName, taskId);
                }
            };
        }
        #endregion

        #region Tools Handling Antrophic
        private BotParameters ToolHandlingAntrophic(List<Antrophic.ChatMessage> additionalMessages, string systemPrompt, string agentName,
            List<FileContent> fileContents, int taskId)
        {
            BotParameters botParameters;
            Antrophic.ToolFunction[] tools = new[] { _functionDefinitions.GetFunctionData_AntrophicReplaceScriptFile() }; // Use the new class
            Sanat.ApiAnthropic.Model model = ApiAnthropic.Model.GetModelByName(_modelName);
            List<Antrophic.ChatMessage> messages = new List<Antrophic.ChatMessage>
                    {
                        new ("assistant", systemPrompt),
                        new ("user", _task)
                    };

            if (additionalMessages != null) messages.AddRange(additionalMessages);

            botParameters = new BotParameters(_prompt, SelectedApiProvider, Temperature, null);
            botParameters.antrophicRequest = new Antrophic.ChatRequest(_modelName, .5f, messages, tools, model.MaxOutputTokens);
            botParameters.antrophicRequest.tool_choice = new Antrophic.ToolChoice { type = "any" };
            botParameters.onAntrophicChatResponseComplete += (response) =>
            {
                Debug.Log($"{agentName} Working on ToolHandle Result...");
                if (response.type == "error")
                {
                    Debug.LogError($"{agentName} error[{response.error.type}]; message: {response.error.message}");
                }
                else if (response.stop_reason == "tool_use")
                {
                    foreach (var responseContent in response.content)
                    {
                        if (responseContent.type == "tool_use")
                        {
                            if (responseContent.name == "ReplaceScriptFile")
                            {
                                FileContent fileContent = new FileContent();
                                foreach (var keyValuePair in responseContent.input)
                                {
                                    switch (keyValuePair.Key)
                                    {
                                        case "FilePath":
                                            fileContent.FilePath = keyValuePair.Value;
                                            break;
                                        case "Content":
                                            fileContent.Content = keyValuePair.Value;
                                            break;
                                    }
                                }
                                SaveResultToFile($"//{fileContent.FilePath}{fileContent.Content}");
                                Debug.Log($"{agentName} tool_use: {responseContent.name}({fileContent.FilePath}, {fileContent.Content})");
                                fileContents.Add(fileContent);
                            }
                        }
                    }
                    ReportFunctionResult_ReplaceScriptFiles(fileContents, agentName, taskId);
                }
            };
            return botParameters;
        }
        #endregion
        #region Tools Handling Gemini
        private void ToolHandlingGemini(BotParameters botParameters, string systemPrompt, string agentName, int taskId)
        {
            _modelName = ApiGemini.Model.ProExp.Name;
            var geminiTools = new List<ApiGemini.Tool> { new() { function_declarations = new List<ApiGemini.FunctionDeclaration>() { _functionDefinitions.GetFunctionData_GeminiReplaceScriptFile() } } };
            botParameters.geminiRequest = new GeminiChatRequest(_task, Temperature)
            {
                tools = geminiTools,
                tool_config = new ToolConfig { function_calling_config = new FunctionCallingConfig { mode = "any", AllowedFunctionNames = new List<string> { AgentFunctionDefinitions.TOOL_NAME_REPLACE_SCRIPT_FILE } } }
            };
            botParameters.geminiRequest.system_instruction = new Content { parts = new List<Part> { new Part { text = systemPrompt } } };
            
            botParameters.onComplete += (result) =>
            {
                if (string.IsNullOrEmpty(result)) return;
                var responseData = JsonConvert.DeserializeObject<ApiGemini.GenerateContentResponse>(result);
                List<FileContent> files = new List<FileContent>();
                if (responseData.candidates != null && responseData.candidates.Count > 0)
                {
                    var candidate = responseData.candidates[0];
                    foreach (var part in candidate.content.parts)
                    {
                        if (part.functionCall != null)
                        {
                            SaveResultToFile(JsonConvert.SerializeObject(part.functionCall.args));
                            var fileContent = JsonConvert.DeserializeObject<FileContent>(JsonConvert.SerializeObject(part.functionCall.args));
                            files.Add(fileContent);
                        }
                    }
                    if (files.Count > 0) ReportFunctionResult_ReplaceScriptFiles(files, agentName, taskId);
                }
            };
        }
        #endregion
        private void ReportFunctionResult_ReplaceScriptFiles(List<FileContent> fileContents, string agentName, int taskId)
        {
            if (fileContents.Count > 0)
            {
                Debug.Log($"{agentName} Task #{taskId} path: {fileContents[0].FilePath}\n fileContents: {fileContents[0].Content}");
                OnFileContentProvided?.Invoke(fileContents);
            }
            else
            {
                Debug.LogError($"{agentName} No file content received");
                OnJobFailed?.Invoke("No file content received");
            }
        }
        private string PrepareSystemPrompt(string fileName = "")
        {
            string rawPrompt = LoadPrompt(_promptLocation);
            if (fileName != "")
            {
                rawPrompt = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{fileName}";
            }
            rawPrompt = rawPrompt.Replace("\r", "");
            rawPrompt = rawPrompt.Replace("\n", "");
            return rawPrompt;
        }
        public void WorkWithFeedback(string invalidationComment, string possibleSolution, Action<string> callback)
        {
            string newPrompt = $"{_prompt} # SOLUTION: {possibleSolution} # COMMENT: {invalidationComment}";
            SaveResultToFile(newPrompt);
            Debug.Log($"<color=green>{Name}</color> asking: {newPrompt}");
            var additionalMessages = new List<Antrophic.ChatMessage>
            {
                new ("assistant", possibleSolution),
                new ("user", invalidationComment)
            };
            WriteCode(additionalMessages, 0);
        }
    }
}