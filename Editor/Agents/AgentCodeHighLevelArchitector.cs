// Copyright (c) Sanat. All rights reserved.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sanat.ApiGemini;
using Sanat.ApiOpenAI;
using UnityEngine;
using Antrophic = Sanat.ApiAnthropic.ApiAntrophicData;
using GeminiChatRequest = Sanat.ApiGemini.ChatRequest;

namespace Sanat.CodeGenerator.Agents
{
    public class AgentCodeHighLevelArchitector : AbstractAgentHandler
    {
        private string _systemInstructions;
        private string _promptLocation;
        
        #region ReplaceScriptFile
        private const string TOOL_NAME_REPLACE_SCRIPT_FILE = "ProvideHighLevelSolution";
        private const string PROPERTY_ProvideHighLevelSolution_FILEPATH = "FilePathes";
        private const string PROPERTY_ProvideHighLevelSolutione_SOLUTION = "Solution";
        private const string FUNCTION_ProvideHighLevelSolution_DESCRIPTION = "Provides answer for high level solution";
        private const string FUNCTION_PROPERTY_ProvideHighLevelSolution_FILEPATHES_DESCRIPTION = "Filepathes of the code scripts that require changes separated by ';', e.g. Assets\\Path\\To\\File.cs;Assets\\Path\\To\\File2.cs;Assets\\Path\\To\\File3.cs";
        private const string FUNCTION_PROPERTY_ProvideHighLevelSolution_SOLUTION_DESCRIPTION = "Technical specification for a programmer to follow";
        #endregion
        
        private string _task;
        public Action<List<TechnicalSpecification>> OnTechnicalSpecificationProvided;
        public Action<string> OnTextAnswerProvided;
        public Action<string> OnJobFailed;

        protected override string PromptFilename() => "AgentCodeHighLevelArchitector.md";

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
        
        public AgentCodeHighLevelArchitector(ApiKeys apiKeys, string task, string includedCode, List<string> selectedMemoryFiles)
        {
            Name = "AgentCodeHighLevelArchitector";
            DebugName = $"<color=#FFC0CB>Agent Unity3D High Level Architect</color>";
            Description = "Writes high level solution for code writer";
            Temperature = 1f;
            StoreKeys(apiKeys);
            _promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PromptFilename()}";
            PromptFromMdFile = LoadPrompt(_promptLocation);
            _selectedMemoryFiles = selectedMemoryFiles ?? new List<string>();
            _task = $"{task}";
            _systemInstructions = $"{PromptFromMdFile} # PROJECT CODE: " + includedCode;
            var selectedMemory = LoadSelectedMemoryIfRequired();
            _systemInstructions += selectedMemory;
            SelectedApiProvider = ApiProviders.Gemini;
            _modelName = ApiGemini.Model.Flash2.Name;
            SelectedApiProvider = ApiProviders.Anthropic;
            _modelName = ApiAnthropic.Model.Claude37.Name;
        }

        private string LoadSelectedMemoryIfRequired()
        {
            string result = "";
            if (_selectedMemoryFiles.Count > 0)
            {
                result = "\n# MEMORY: ";
                string promptLocation = "";
                string prompt = "";
                foreach (var fileName in _selectedMemoryFiles)
                {
                    promptLocation = Application.dataPath + $"{MEMORY_FOLDER_PATH}{fileName}.md";
                    prompt = LoadPrompt(promptLocation);
                    result += prompt + "; ";
                }
            }

            return result;
        }

        public override void Handle(string input)
        {
            ToolHandle();
        }

        private async Task ToolHandle(List<Antrophic.ChatMessage> additionalMessages = null, string task = "")
        {
            await Task.Delay(100);
            Debug.Log($"{DebugName} asking [{SelectedApiProvider}][{_modelName}]: {_task}");
            BotParameters botParameters = new BotParameters(_task, SelectedApiProvider, Temperature, null, _modelName, true);
            string systemPrompt = PrepareSystemPrompt();
            switch (botParameters.apiProvider)
            {
                case ApiProviders.OpenAI:
                    
                    break;

                case ApiProviders.Gemini:
                    botParameters.geminiRequest = new GeminiChatRequest(_task, Temperature);
                    botParameters.geminiRequest.system_instruction = new Content { parts = new List<Part> { new Part { text = _systemInstructions } } };
                    botParameters.onComplete += (result) =>
                    {
                        if (string.IsNullOrEmpty(result)) return;
                        SaveResultToFile(JsonConvert.SerializeObject(result));
                        Debug.Log($"{DebugName}: {result}");
                        OnTextAnswerProvided?.Invoke(result);
                    };
                    break;

                case ApiProviders.Anthropic:
                    botParameters.antrophicRequest = new Antrophic.ChatRequest(_modelName, .5f, new List<Antrophic.ChatMessage>
                    {
                        new ("assistant", _systemInstructions),
                        new ("user", _task)
                    }, null, ApiAnthropic.Model.GetModelByName(_modelName).MaxOutputTokens);
                    botParameters.onAntrophicChatResponseComplete += (response) =>
                    {
                        if (response.type == "error")
                        {
                            Debug.LogError($"{DebugName} error[{response.error.type}]; message: {response.error.message}");
                        }
                        else
                        {
                            foreach (var responseContent in response.content)
                            {
                                SaveResultToFile(JsonConvert.SerializeObject(responseContent.text));
                                Debug.Log($"{DebugName}: {responseContent.text}");
                                OnTextAnswerProvided?.Invoke(responseContent.text);
                            }
                        }
                        
                    };
                    break;
            }
            
            AskBot(botParameters);
        }

        #region Tools OpenAI

        private void ToolHandlingOpenAI(BotParameters botParameters, string systemPrompt)
        {
            _modelName = ApiOpenAI.Model.GPT4omini.Name;
            var openaiTools = new ApiOpenAI.Tool[] { new ("function", GetFunctionData_OpenaiProvideHighLevelSolution()) };
            botParameters.isToolUse = true;
            botParameters.openaiTools = openaiTools;
            botParameters.systemMessage = systemPrompt;
            botParameters.onOpenaiChatResponseComplete += (response) =>
            {
                Debug.Log($"{DebugName} ToolHandle Result: {response}");
                if (response.choices[0].finish_reason == "tool_calls")
                {
                    ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                    int filesAmount = toolCalls.Length;
                    List<TechnicalSpecification> fileContents = new List<TechnicalSpecification>();
                    string logFileNames = $"<color=green>{DebugName}</color>: ";
                            
                    for (int i = 0; i < filesAmount; i++)
                    {
                        SaveResultToFile(toolCalls[i].function.arguments);
                        fileContents.Add(JsonConvert.DeserializeObject<TechnicalSpecification>(toolCalls[i].function.arguments));
                        logFileNames += $"{fileContents[i].FilePathes}, ";
                        if (i == filesAmount - 1)
                        {
                            logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        }
                    }
                    Debug.Log(logFileNames);
                    ReportFunctionResult_ReplaceScriptFiles(fileContents);
                }
            };
        }

        #endregion
        
        #region Tools Handling Antrophic
        
        private BotParameters ToolHandlingAntrophic(List<Antrophic.ChatMessage> additionalMessages, string systemPrompt)
        {
            BotParameters botParameters;
            Antrophic.ToolFunction[] tools = new [] {GetFunctionData_AntrophicProvideHighLevelSolution()};
            Sanat.ApiAnthropic.Model model = ApiAnthropic.Model.GetModelByName(_modelName);
            List<TechnicalSpecification> techSpecs = new List<TechnicalSpecification>();
            List<Antrophic.ChatMessage> messages = new List<Antrophic.ChatMessage>
            {
                new ("assistant", systemPrompt),
                new ("user", _task)
            };
                    
            if (additionalMessages != null) messages.AddRange(additionalMessages);
                    
            botParameters = new BotParameters(_systemInstructions, SelectedApiProvider, Temperature, null);
            botParameters.antrophicRequest = new Antrophic.ChatRequest(_modelName, .5f, messages, tools, model.MaxOutputTokens);
            botParameters.antrophicRequest.tool_choice = new Antrophic.ToolChoice {type = "any"};
            botParameters.onAntrophicChatResponseComplete += (response) =>
            {
                Debug.Log($"{DebugName} Working on ToolHandle Result...");
                if (response.type == "error")
                {
                    Debug.LogError($"{DebugName} error[{response.error.type}]; message: {response.error.message}");
                }
                else if (response.stop_reason == "tool_use")
                {
                    foreach (var responseContent in response.content)
                    {
                        if (responseContent.type == "tool_use")
                        {
                            if (responseContent.name == TOOL_NAME_REPLACE_SCRIPT_FILE)
                            {
                                TechnicalSpecification fileContent = new TechnicalSpecification();
                                foreach (var keyValuePair in responseContent.input)
                                {
                                    switch (keyValuePair.Key)
                                    {
                                        case PROPERTY_ProvideHighLevelSolution_FILEPATH:
                                            fileContent.FilePathes = keyValuePair.Value;
                                            break;
                                        case PROPERTY_ProvideHighLevelSolutione_SOLUTION:
                                            fileContent.Solution = keyValuePair.Value;
                                            break;
                                    }
                                }
                                SaveResultToFile($"//{fileContent.FilePathes}\n {fileContent.Solution}");
                                Debug.Log($"{DebugName} tool_use: {responseContent.name}({fileContent.FilePathes}, {fileContent.Solution})");
                                techSpecs.Add(fileContent);
                            }
                        }
                    }
                    ReportFunctionResult_ReplaceScriptFiles(techSpecs);
                }
            };
            return botParameters;
        }
        
        #endregion

        #region Tools Handling Gemini

        private void ToolHandlingGemini(BotParameters botParameters, string systemPrompt)
        {
            _modelName = ApiGemini.Model.Flash2.Name;
            var geminiTools = new List<ApiGemini.Tool> { new() { function_declarations = new List<ApiGemini.FunctionDeclaration>() { GetFunctionData_GeminiProvideHighLevelSolution() } } };
            botParameters.geminiRequest = new GeminiChatRequest(_task, Temperature)
            {
                tools = geminiTools,
                tool_config = new ToolConfig { function_calling_config = new FunctionCallingConfig { mode = "auto" } }
            };
            botParameters.geminiRequest.system_instruction = new Content { parts = new List<Part> { new Part { text = systemPrompt } } };
            botParameters.onComplete += (result) =>
            {
                if(string.IsNullOrEmpty(result)) return;
                var responseData = JsonUtility.FromJson<ApiGemini.GenerateContentResponse>(result);
                if (responseData.candidates != null && responseData.candidates.Count > 0)
                {
                    var candidate = responseData.candidates[0];
                    var part = candidate.content.parts[0];
                    if (part.functionCall != null)
                    {
                        SaveResultToFile(JsonConvert.SerializeObject(part.functionCall.args));
                        var fileContent = JsonConvert.DeserializeObject<TechnicalSpecification>(JsonConvert.SerializeObject(part.functionCall.args));
                        ReportFunctionResult_ReplaceScriptFiles(new List<TechnicalSpecification>{fileContent});
                    }
                }
            };
        }

        #endregion

        private void ReportFunctionResult_ReplaceScriptFiles(List<TechnicalSpecification> technicalSpecs)
        {
            if (technicalSpecs.Count > 0)
            {
                Debug.Log($"{DebugName} technicalSpecs: {technicalSpecs.Count}");
                OnTechnicalSpecificationProvided?.Invoke(technicalSpecs);
            }
            else
            {
                Debug.LogError($"{DebugName} No file content received");
                OnJobFailed?.Invoke("No file content received");
            }
        }
        
        #region Tool_ProvideHighLevelSolution
        
        protected ToolFunction GetFunctionData_OpenaiProvideHighLevelSolution()
        {
            Parameter parameters = new ();
            parameters.AddProperty(PROPERTY_ProvideHighLevelSolution_FILEPATH, DataTypes.STRING, FUNCTION_PROPERTY_ProvideHighLevelSolution_FILEPATHES_DESCRIPTION);
            parameters.AddProperty(PROPERTY_ProvideHighLevelSolutione_SOLUTION, DataTypes.STRING, FUNCTION_PROPERTY_ProvideHighLevelSolution_SOLUTION_DESCRIPTION);
            parameters.Required.Add(PROPERTY_ProvideHighLevelSolution_FILEPATH);
            parameters.Required.Add(PROPERTY_ProvideHighLevelSolutione_SOLUTION);
            ToolFunction function = new ToolFunction(TOOL_NAME_REPLACE_SCRIPT_FILE, FUNCTION_ProvideHighLevelSolution_DESCRIPTION, parameters);
            return function;
        }
        
        protected ApiGemini.FunctionDeclaration GetFunctionData_GeminiProvideHighLevelSolution()
        {
            ApiGemini.FunctionDeclarationSchema parameters = new ApiGemini.FunctionDeclarationSchema 
            {
                type = ApiGemini.FunctionDeclarationSchemaType.OBJECT,
                properties = new Dictionary<string, ApiGemini.FunctionDeclarationSchemaProperty> 
                {
                    { PROPERTY_ProvideHighLevelSolution_FILEPATH, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.STRING, description = FUNCTION_PROPERTY_ProvideHighLevelSolution_FILEPATHES_DESCRIPTION} },
                    { PROPERTY_ProvideHighLevelSolutione_SOLUTION, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.STRING, description = FUNCTION_PROPERTY_ProvideHighLevelSolution_SOLUTION_DESCRIPTION} },
                },
                required = new List<string> { PROPERTY_ProvideHighLevelSolution_FILEPATH, PROPERTY_ProvideHighLevelSolutione_SOLUTION }
            };
            
            return new ApiGemini.FunctionDeclaration 
            {
                name = TOOL_NAME_REPLACE_SCRIPT_FILE,
                description = FUNCTION_ProvideHighLevelSolution_DESCRIPTION,
                parameters = parameters
            };
        }
        
        protected Antrophic.ToolFunction GetFunctionData_AntrophicProvideHighLevelSolution()
        {
            Antrophic.InputSchema parameters = new ();
            parameters.AddProperty(PROPERTY_ProvideHighLevelSolution_FILEPATH, Antrophic.DataTypes.STRING, FUNCTION_PROPERTY_ProvideHighLevelSolution_FILEPATHES_DESCRIPTION);
            parameters.AddProperty(PROPERTY_ProvideHighLevelSolutione_SOLUTION, Antrophic.DataTypes.STRING, FUNCTION_PROPERTY_ProvideHighLevelSolution_SOLUTION_DESCRIPTION);
            parameters.Required.Add(PROPERTY_ProvideHighLevelSolution_FILEPATH);
            parameters.Required.Add(PROPERTY_ProvideHighLevelSolutione_SOLUTION);
            Antrophic.ToolFunction function = new Antrophic.ToolFunction(TOOL_NAME_REPLACE_SCRIPT_FILE, PROPERTY_ProvideHighLevelSolutione_SOLUTION, parameters);
            return function;
        }
        #endregion
        private string PrepareSystemPrompt()
        {
            string rawPrompt = LoadPrompt(_promptLocation);
            rawPrompt = rawPrompt.Replace("\r", "");
            rawPrompt = rawPrompt.Replace("\n", "");
            return rawPrompt;
        }
    }
}