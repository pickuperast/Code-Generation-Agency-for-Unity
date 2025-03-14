// Copyright (c) Sanat. All rights reserved.
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sanat.ApiGemini;
using Sanat.ApiGroq;
using Sanat.ApiOpenAI;
using Sanat.CodeGenerator;
using Sanat.CodeGenerator.Editor;
using Antrophic = Sanat.ApiAnthropic.ApiAntrophicData;
using GeminiChatRequest = Sanat.ApiGemini.ChatRequest;
using Model = Sanat.ApiGemini.Model;

namespace Sanat.CodeGenerator.Agents
{
    public class AgentCodeMerger : AbstractAgentHandler
    {
        protected const string PROMPT_FILE_PATH_EXTRACT = "PromptAgentCodeMergerFilePathExtract.md";
        protected const string PROMPT_MERGE_CODE = "PromptAgentCodeMergerMergeCode.md";
        string[] _projectCodePaths;
        
        protected override string PromptFilename() => "AgentCodeMerger.md";
        
        protected override ApiOpenAI.Model GetModel() => ApiOpenAI.Model.GPT4omini;
        
        protected override string GetGeminiModel() => ApiGemini.Model.FlashExp.Name;
        private AgentFunctionDefinitions _functionDefinitions;

        public AgentCodeMerger()
        {
            _functionDefinitions = new AgentFunctionDefinitions();
        }

        public AgentCodeMerger(ApiKeys apiKeys, List<FileContent> includedCodeFiles, string[] projectCodePaths)
        {
            Name = "Agent Code Merger";
            Description = "Merges code snippets";
            Temperature = .0f;
            string promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PromptFilename()}";
            PromptFromMdFile = LoadPrompt(promptLocation);
            StoreKeys(apiKeys);
            _projectCode = includedCodeFiles;
            _projectCodePaths = projectCodePaths;
            SelectedApiProvider = ApiProviders.Gemini;
            _functionDefinitions = new AgentFunctionDefinitions();
        }
        
        public void InsertCode(string solutionInput)
        {
            GetFilePath(solutionInput, (result) =>
            {
                FileContent newQuestData = JsonUtility.FromJson<FileContent>(result);
                string[] filePathes = newQuestData.FilePath.Split(CSV_SEPARATOR);
                string[] fileContents = newQuestData.Content.Split(CSV_SEPARATOR);
                for (int i = 0; i < filePathes.Length; i++)
                {
                    Debug.Log($"<color=cyan>{Name}</color> GetFilePath Result: {filePathes[i]}");
                }
                
                var splitted = result.Split(";");
                //var filePathesCleared = LeaveOnlyCsFiles(splitted);
                if (splitted.Length == 1)
                {
                    Debug.Log($"<color=cyan>{Name}</color> working with single path: {result}");
                    DirectInsertion(splitted[0], solutionInput);
                }else
                {
                    for (int i = 0; i < splitted.Length; i++)
                    {
                        ExtractCodeContents(solutionInput, splitted[i]);
                    }
                }
            });
        }

        protected void ExtractCodeContents(string solutionInput, string filePath)
        {
            Debug.Log($"<color=cyan>{Name}</color> ExtractCodeContents for path: {filePath}");
            AgentExtractCodeByFilepath agentExtractCodeByFilepath = new AgentExtractCodeByFilepath(Apikeys, filePath);
            agentExtractCodeByFilepath.SplitToFilePathContent(solutionInput, (result) =>
            {
                Debug.Log($"<color=cyan>{Name}</color> ExtractCodeContents Result: {result}");
                Debug.Log($"<color=cyan>{Name}</color> working with single path: {result}");
                DirectInsertion(filePath, solutionInput);
            });
        }

        protected void GetFilePath(string solutionInput, Action<string> callback)
        {
            string promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PROMPT_FILE_PATH_EXTRACT}";
            string agentLogName = $"<color=cyan>{Name}</color>";
            string question = solutionInput;
            _modelName = ApiOpenAI.Model.GPT4omini.Name;//ApiGroqModels.Llama3_70b_8192_tool.Name;
            BotParameters botParameters = new BotParameters(question, ApiProviders.OpenAI, .2f, callback, _modelName, true);
            var openaiTools = new ApiOpenAI.Tool[] { new ("function", _functionDefinitions.GetFunctionData_OpenaiSplitCodeToFilePathes()) };
            botParameters.isToolUse = true;
            botParameters.openaiTools = openaiTools;
            botParameters.systemMessage = LoadPrompt(promptLocation);
            botParameters.onOpenaiChatResponseComplete += (response) =>
            {
                Debug.Log($"{agentLogName} GetFilePath Result: {response}");
                if (response.choices[0].finish_reason == "tool_calls")
                {
                    ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                    int filesAmount = toolCalls.Length;
                    FileContent[] fileContents = new FileContent[filesAmount];
                    string logFileNames = $"<color=cyan>{Name}</color>: ";
                    for (int i = 0; i < filesAmount; i++)
                    {
                        fileContents[i] = JsonConvert.DeserializeObject<FileContent>(toolCalls[i].function.arguments);
                        logFileNames += $"{fileContents[i].FilePath}, ";
                        if (i == filesAmount - 1)
                        {
                            logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        }
                    }
                    
                    Debug.Log(logFileNames);
                    for (int i = 0; i < filesAmount; i++)
                    {
                        Debug.Log($"{agentLogName} Writing file: {fileContents[i].FilePath}");
                        DirectInsertion(fileContents[i].FilePath, fileContents[i].Content);
                    }
                }
            };
            AskBot(botParameters);
        }

        public override void Handle(string input)
        {
            string clearedJson = ClearResult(input);
            Debug.Log($"<color=cyan>{Name}</color> working with this solution({clearedJson.Length} chars): {clearedJson}");
            List<FileContent> codeSnippets;
            try
            {
                codeSnippets = ExtractFileContents(clearedJson);
                foreach (FileContent snippet in codeSnippets)
                {
                    string filePath = snippet.FilePath;
                    string code = snippet.Content;
                
                    Debug.Log("FilePath: " + filePath);
                    Debug.Log("Content: " + code);
                    if (code == String.Empty)
                        continue;
                
                    code = TranslateFromValidJson(code);
                    code = code.Replace("\\n", "\n").Replace("\\r\\n", "\r\n");
                    DirectInsertion(filePath, code);
                }
            }catch(Exception ex)
            {
                Debug.Log($"<color=cyan>{Name}</color> <color=red>ERROR!</color> Could not parse the solution. Trying different approach. Exception: {ex.Message}");
                codeSnippets = ParseJson(clearedJson);
                foreach (FileContent snippet in codeSnippets)
                {
                    string filePath = snippet.FilePath;
                    string code = snippet.Content;
                
                    Debug.Log("FilePath: " + filePath);
                    Debug.Log($"Content({code.Length} chars): " + code);
                    if (code == String.Empty)
                        continue;
                
                    string request = "Pretty print the code for direct insertion to file. Dont say anything, just provide the code. DONT SKIP ANY CODE OR KITTENS WILL DIE PLEASE PLEASE PLEASE. # CODE:";
                    string prompt = $"{request}{code}";
                    BotParameters botParameters = new BotParameters(prompt, SelectedApiProvider, Temperature, delegate(string result)
                    {
                        Debug.Log($"<color=purple>{Name}</color> result({result} chars): {result}");
                    
                        var match = Regex.Match(result, @"```\s*([\s\S]+)```");
                        string formattedCode = match.Groups[1].Value;
                        formattedCode = formattedCode.Replace("\\n", "\n").Replace("\\r\\n", "\r\n");
                        DirectInsertion(filePath, formattedCode);
                    });
                    AskBot(botParameters);
                }
            }

            if (_nextHandler != null)
            {
                _nextHandler.Handle(input);
            }
        }

        protected List<FileContent> ExtractFileContents(string clearedJson)
        {
            List<FileContent> codeSnippets;
            string convertedJson = TranslateToValidJson(clearedJson);
            var settings = new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml };
            codeSnippets = JsonConvert.DeserializeObject<List<FileContent>>(convertedJson, settings);
            return codeSnippets;
        }

        protected string TranslateToValidJson(string clearedJson)
        {
            string convertedJson = clearedJson.Replace("({", KEY_FIGURE_OPEN);
            convertedJson = convertedJson.Replace("(}", KEY_FIGURE_CLOSE);
            return convertedJson;
        }
        
        protected string TranslateFromValidJson(string clearedJson)
        {
            string convertedJson = clearedJson.Replace(KEY_FIGURE_OPEN, "({");
            convertedJson = convertedJson.Replace(KEY_FIGURE_CLOSE, "(}");
            return convertedJson;
        }

        public List<FileContent> ParseJson(string json)
        {
            string filePathPattern = @"""FilePath"":\s*""([^""]+)""";
            string contentPattern = @"""Content"":\s*""((?:[^""\\]|\\.)*)""";

            var filePathMatches = Regex.Matches(json, filePathPattern);
            var contentMatches = Regex.Matches(json, contentPattern);

            List<FileContent> filePathList = new List<FileContent>();
            
            for (int i = 0; i < filePathMatches.Count; i++)
            {
                string filePath = filePathMatches[i].Groups[1].Value;
                string content = contentMatches[i].Groups[1].Value;
                filePathList.Add(new FileContent { FilePath = filePath, Content = content });
            }

            return filePathList;
        }

        public bool CheckIfFileExists(string className)
        {
            for (int i = 0; i < _projectCodePaths.Length; i++)
            {
                if (_projectCodePaths[i].Contains(className))
                {
                    return true;
                }
            }
            return false;
        }

        public async void MergeFiles(List<FileContent> fileContents)
        {
            var instructions = LoadPrompt(Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PROMPT_MERGE_CODE}");
            await Task.Delay(100);
            foreach(var fileContent in fileContents)
            {
                string className = fileContent.FilePath.Split("/").Last();
                bool fileExists = CheckIfFileExists(className);
                if (fileExists)
                {
                    Debug.Log($"<color=cyan>{Name}</color> Merging code for: {fileContent.FilePath}");
                    MergeCodeWithLLM(fileContent);
                }
                else
                {
                    AgentCodeMerger agentCodeMerger = new AgentCodeMerger();
                    agentCodeMerger.DirectInsertion(fileContent.FilePath, fileContent.Content);
                }
            }
        }

        public async Task MergeCodeWithLLM(FileContent fileContentToMerge)
        {
            await Task.Delay(100);
            string oldCode = GetCurrentCodeAtPath(fileContentToMerge.FilePath);
            
            if (string.IsNullOrEmpty(oldCode))
            {
                Debug.LogError($"{Name} ERROR! Could not find the old code for: {fileContentToMerge.FilePath}");
                return;
            }
            
            string question = $"Path: {fileContentToMerge.FilePath}. Here is the Old Code:\n" + oldCode + "\n\nHere is the New Code:\n" + fileContentToMerge.Content;
            SelectedApiProvider = ApiProviders.Gemini;
            _modelName = Model.FlashExp.Name;
            Debug.Log($"{Name} asking [{SelectedApiProvider}][{_modelName}]: {question}");
            
            BotParameters botParameters = new BotParameters(question, SelectedApiProvider, Temperature, null, _modelName, true);
            
            string promptLocation = Application.dataPath + $"{PROMPTS_FOLDER_PATH}{PROMPT_MERGE_CODE}";
            string systemPrompt = LoadPrompt(promptLocation);
            
            switch (botParameters.apiProvider)
            {
                case ApiProviders.OpenAI:
                    ToolMergeCodeWithLLMOpenAI(botParameters, systemPrompt, fileContentToMerge);
                    break;
                case ApiProviders.Gemini:
                    ToolMergeCodeWithLLMGemini(botParameters, systemPrompt, fileContentToMerge);
                    break;
                case ApiProviders.Anthropic:
                    ToolMergeCodeWithLLMAnthropic(botParameters, systemPrompt, fileContentToMerge);
                    break;
            }
            
            AskBot(botParameters);
        }

        private void ToolMergeCodeWithLLMOpenAI(BotParameters botParameters, string systemPrompt, FileContent fileContentToMerge)
        {
            var openaiTools = new ApiOpenAI.Tool[] { new("function", _functionDefinitions.GetFunctionData_OpenaiMergeCode()) };
            botParameters.isToolUse = true;
            botParameters.openaiTools = openaiTools;
            botParameters.systemMessage = systemPrompt;
            botParameters.onOpenaiChatResponseComplete += (response) =>
            {
                string responseText = JsonConvert.SerializeObject(response);
                Debug.Log($"{Name} MergeCodeWithLLM Result: {responseText}");
                SaveResultToFile(responseText);
                
                if (response.choices[0].finish_reason == "tool_calls")
                {
                    ToolCalls[] toolCalls = response.choices[0].message.tool_calls;
                    int filesAmount = toolCalls.Length;
                    FileContent[] fileContents = new FileContent[filesAmount];
                    string logFileNames = $"{Name}: ";
                    
                    for (int i = 0; i < filesAmount; i++)
                    {
                        fileContents[i] = JsonConvert.DeserializeObject<FileContent>(toolCalls[i].function.arguments);
                        logFileNames += $"{fileContents[i].FilePath}, ";
                        if (i == filesAmount - 1)
                        {
                            logFileNames = logFileNames.Remove(logFileNames.Length - 2);
                        }
                    }
                    
                    Debug.Log(logFileNames);
                    for (int i = 0; i < filesAmount; i++)
                    {
                        Debug.Log($"{Name} Writing file: {fileContents[i].FilePath}");
                        DirectInsertion(fileContents[i].FilePath, fileContents[i].Content);
                    }
                }
            };
        }

        private void ToolMergeCodeWithLLMGemini(BotParameters botParameters, string systemPrompt, FileContent fileContentToMerge)
        {
            var geminiTools = new List<ApiGemini.Tool> {
                new() {
                    function_declarations = new List<ApiGemini.FunctionDeclaration>() {
                        _functionDefinitions.GetFunctionData_GeminiMergeCode()
                    }
                }
            };
            
            botParameters.geminiRequest = new GeminiChatRequest(botParameters.prompt, Temperature)
            {
                tools = geminiTools,
                tool_config = new ToolConfig { 
                    function_calling_config = new FunctionCallingConfig { 
                        mode = "any", 
                        AllowedFunctionNames = new List<string> { "MergeCode" } 
                    } 
                }
            };
            
            botParameters.geminiRequest.system_instruction = new Content { 
                parts = new List<Part> { new Part { text = systemPrompt } } 
            };
            
            botParameters.onComplete += (result) =>
            {
                if (string.IsNullOrEmpty(result)) return;
                
                var responseData = JsonConvert.DeserializeObject<ApiGemini.GenerateContentResponse>(result);
                SaveResultToFile(result);
                
                if (responseData.candidates != null && responseData.candidates.Count > 0)
                {
                    var candidate = responseData.candidates[0];
                    foreach (var part in candidate.content.parts)
                    {
                        if (part.functionCall != null)
                        {
                            SaveResultToFile(JsonConvert.SerializeObject(part.functionCall.args));
                            var fileContent = JsonConvert.DeserializeObject<FileContent>(JsonConvert.SerializeObject(part.functionCall.args));
                            Debug.Log($"{Name} Writing file: {fileContent.FilePath}");
                            DirectInsertion(fileContent.FilePath, fileContent.Content);
                        }
                    }
                }
            };
        }

        private void ToolMergeCodeWithLLMAnthropic(BotParameters botParameters, string systemPrompt, FileContent fileContentToMerge)
        {
            var antrophicTools = new[] { _functionDefinitions.GetFunctionData_AntrophicMergeCode() };
            Sanat.ApiAnthropic.Model model = ApiAnthropic.Model.GetModelByName(_modelName);
            
            List<Antrophic.ChatMessage> messages = new List<Antrophic.ChatMessage>
            {
                new("assistant", systemPrompt),
                new("user", botParameters.prompt)
            };
            
            botParameters.antrophicRequest = new Antrophic.ChatRequest(_modelName, Temperature, messages, antrophicTools, model.MaxOutputTokens);
            botParameters.antrophicRequest.tool_choice = new Antrophic.ToolChoice { type = "any" };
            
            botParameters.onAntrophicChatResponseComplete += (response) =>
            {
                Debug.Log($"{Name} Working on MergeCodeWithLLM Result...");
                SaveResultToFile(JsonConvert.SerializeObject(response));
                
                if (response.type == "error")
                {
                    Debug.LogError($"{Name} error[{response.error.type}]; message: {response.error.message}");
                    return;
                }
                
                if (response.stop_reason == "tool_use")
                {
                    foreach (var responseContent in response.content)
                    {
                        if (responseContent.type == "tool_use" && responseContent.name == "MergeCode")
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
                            
                            SaveResultToFile($"//{fileContent.FilePath}\n{fileContent.Content}");
                            Debug.Log($"{Name} tool_use: {responseContent.name}({fileContent.FilePath})");
                            DirectInsertion(fileContent.FilePath, fileContent.Content);
                        }
                    }
                }
            };
        }
        
        private string SafeUnescape(string code)
        {
            try
            {
                return Regex.Unescape(code);
            }
            catch (ArgumentException)
            {
                // If Regex.Unescape fails, use a custom unescaping approach
                return code.Replace("\\\\", "\\")  // Handle escaped backslashes
                    .Replace("\\'", "'")     // Handle escaped single quotes
                    .Replace("\\\"", "\"")   // Handle escaped double quotes
                    .Replace("\\n", "\n")    // Handle newlines
                    .Replace("\\r", "\r")    // Handle carriage returns
                    .Replace("\\t", "\t");   // Handle tabs
            }
        }
        
        public void DirectInsertion(string filePath, string code)
        {
            SaveResultToFile(code);
            filePath = filePath.Replace(":", string.Empty);
            CompareAndFixFilePath(ref filePath);
            code = SafeUnescape(code);
            code = code.Replace("\r\n", "\n")  // First normalize to \n
                .Replace("\r", "\n")      // Convert any remaining \r to \n
                .Replace("\n", Environment.NewLine); // Then convert to platform-specific line endings
            
            if (File.Exists(filePath))
            {
                string originalContent = File.ReadAllText(filePath);
                BackupManager.BackupScriptFile(filePath, originalContent);
            }

            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                }

                File.SetAttributes(filePath, FileAttributes.Normal);
                File.WriteAllText(filePath, code, new UTF8Encoding(false));
                OnComplete?.Invoke(code);
                CodeGeneratorFileOpener.OpenScript(filePath);
                Debug.Log($"<color=cyan>{Name}</color> <color=green>COMPLETED!</color> [{filePath}] Direct insertion:\n{code}");
            }catch(Exception ex)
            {
                Debug.LogError($"<color=cyan>{Name}</color> <color=red>ERROR!</color> Could not insert the code to: {filePath}. Exception: {ex.Message}");
            }
            
        }

        private void CompareAndFixFilePath(ref string filePath)
        {
            var filename = Regex.Match(filePath, @"([^\\\/]+\.cs)");
            // try to find that filename in the project code
            if (filename.Success)
            {
                string filenameToFind = filename.Groups[1].Value;
                foreach (var projectFilePath in _projectCode)
                {
                    if (projectFilePath.FilePath.Contains(filenameToFind))
                    {
                        filePath = projectFilePath.FilePath;
                        break;
                    }
                }
            }
        }

        public string GetCurrentCodeAtPath(string filePath)
        {
            string oldCode = String.Empty;
            string normalizedPathToFind = Path.GetFullPath(filePath).Replace("/", Path.DirectorySeparatorChar.ToString()).Replace("\\", Path.DirectorySeparatorChar.ToString());

            string checkedPath = "";
            
            if (filePath.Contains("/")) filePath.Replace("/", "\\");
            foreach (var file in _projectCode)
            {
                string normalizedProjectPath = Path.GetFullPath(file.FilePath).Replace("/", Path.DirectorySeparatorChar.ToString()).Replace("\\", Path.DirectorySeparatorChar.ToString());

                checkedPath += file.FilePath + "\n";
                if (normalizedProjectPath.Equals(normalizedPathToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return file.Content;
                    break;
                }
            }
            Debug.LogError($"<color=cyan>{Name}</color> <color=red>ERROR!</color> Could not find the code for: {filePath}. Checked Pathes: {checkedPath}");
            return String.Empty;
        }
    }
}