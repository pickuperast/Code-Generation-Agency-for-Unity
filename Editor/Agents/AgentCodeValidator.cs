// Copyright (c) Sanat. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sanat.ApiGemini;
using UnityEngine;
using Sanat.ApiOpenAI;
using Newtonsoft.Json;

namespace Sanat.CodeGenerator.Agents
{
    public class AgentCodeValidator : AbstractAgentHandler
    {
        public string[] Tools { get; set; }
        private string _prompt;
        private const string PROMPT_VALIDATE_SOLUTION_USING_TOOL = "PromptAgentCodeValidatorValidateSolutionTool.md";
        private const string FUNCTION_VALIDATE_SOLUTION = "ValidateSolution";
        private AgentCodeMerger _agentCodeMerger;

        protected override string PromptFilename() => "UnityCodeValidatorInstructions.md";
        
        public AgentCodeValidator(ApiKeys apiKeys, string task, Dictionary<string, string> classToPath, string includedCode, string possibleAnswer, AgentCodeMerger codeMerger)
        {
            Name = "Agent Code Validator";
            Description = "Validates code for agents";
            Temperature = .0f;
            ClassToPath = classToPath;
            StoreKeys(apiKeys);
            _modelName = Sanat.ApiOpenAI.Model.GPT4omini.Name;
            string promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PromptFilename()}";
            PromptFromMdFile = LoadPrompt(promptLocation);
            _prompt = $"{PromptFromMdFile} " +
                      $"# TASK: {task}. " +
                      $"# CODE: {includedCode} " +
                      $"# POSSIBLE ANSWER: {possibleAnswer}";
            SelectedApiProvider = ApiProviders.OpenAI;
            _agentCodeMerger = codeMerger;
        }
        
        protected override string GetGeminiModel() => ApiGemini.Model.Pro.Name;

        public override void Handle(string input)
        {
            Debug.Log($"<color=yellow>{Name}</color> asking: {_prompt}");
            BotParameters botParameters = new BotParameters(_prompt, SelectedApiProvider, Temperature, delegate(string result)
            {
                //Debug.Log($"<color=purple>{Name}</color> result: {result}");
                OnComplete?.Invoke(result);
                SaveResultToFile(result);
            });
            AskBot(botParameters);
        }
        
        protected ToolFunction GetFunctionData_OpenaiValidateSolution()
        {
            string description = "Validate provided solution to fully accomplish task";
            string propertyIsValid = "IsValid";
            string propertyComment = "Comment";

            Parameter parameters = new ();
            parameters.AddProperty(propertyIsValid, DataTypes.NUMBER, $"AI must tell if provided solution is valid or not. 1 for valid, 0 for invalid");
            parameters.AddProperty(propertyComment, DataTypes.STRING, $"Fill this field only if solution is invalid. Describing what is wrong with solution.");
            parameters.Required.Add(propertyIsValid);
            parameters.Required.Add(propertyComment);
            ToolFunction function = new ToolFunction(FUNCTION_VALIDATE_SOLUTION, description, parameters);
            return function;
        }
        
        public struct ValidationData
        {
            public int IsValid;
            public string Comment;
        }
        
        protected async Task DoLLMValidation(List<FileContent> fileContents, Action<string> invalidComment)
        {
            string promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PROMPT_VALIDATE_SOLUTION_USING_TOOL}";
            string agentLogName = $"<color=yellow>{Name}</color>";
            bool isSkipValidation = true;
            foreach (var file in fileContents)
            {
                if (isSkipValidation)
                {
                    _agentCodeMerger.MergeFiles(new List<FileContent> { file });
                    continue;
                }
                
                string filePath = file.FilePath;
                string className = file.FilePath.Split("/").Last();
                bool fileExists = _agentCodeMerger.CheckIfFileExists(className);
                string oldCode = "";
                string question = $"{JsonConvert.SerializeObject(file)}";
                if (fileExists)
                {
                    oldCode = "# CURRENT CODE: " + _agentCodeMerger.GetCurrentCodeAtPath(filePath);
                    question = $"{oldCode} # NEW IMPLEMENTATION: {JsonConvert.SerializeObject(file)}";
                }
                _modelName = ApiOpenAI.Model.GPT4omini.Name;//ApiGroqModels.Llama3_70b_8192_tool.Name;
                BotParameters botParameters = new BotParameters(question, ApiProviders.OpenAI, .2f, null, _modelName, true);
                var openaiTools = new ApiOpenAI.Tool[] { new ("function", GetFunctionData_OpenaiValidateSolution()) };
                botParameters.isToolUse = true;
                botParameters.openaiTools = openaiTools;
                botParameters.systemMessage = LoadPrompt(promptLocation);
                botParameters.onOpenaiChatResponseComplete += (response) =>
                {
                    Debug.Log($"{agentLogName} [DoLLMValidation] GetFilePath Result: {response}");
                    if (response.choices[0].finish_reason == "tool_calls")
                    {
                        ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                        ValidationData validationData = JsonConvert.DeserializeObject<ValidationData>(toolCalls[0].function.arguments);
                        if (validationData.IsValid == 1)
                        {
                            Debug.Log($"{agentLogName} Solution is valid");
                            _agentCodeMerger.MergeFiles(new List<FileContent> { file });
                        }
                        else
                        {
                            Debug.LogError($"{agentLogName} Solution is invalid: {validationData.Comment}");
                            invalidComment?.Invoke(validationData.Comment);
                        }
                    }
                };
            
            
                AskBot(botParameters);
                await Task.Delay(100);
            }
        }
        
        public void ValidateSolution(List<FileContent> fileContents, Action<string> onInvalidCommentProvided)
        {
            string agentName = $"<color=yellow>{Name}</color>";
            Debug.Log($"{agentName} asking: {_prompt}");
            
            DoLLMValidation(fileContents, onInvalidCommentProvided);
            return;
            foreach(var fileContent in fileContents)
            {
                string className = fileContent.FilePath.Split("/").Last().Split(".").First();
                bool fileExists = ClassToPath.TryGetValue(className, out string filePath);
                if (fileExists)
                {
                    
                }
                else
                {
                    AgentCodeMerger agentCodeMerger = new AgentCodeMerger();
                    agentCodeMerger.DirectInsertion(fileContent.FilePath, fileContent.Content);
                }
            }
        }
    }
}