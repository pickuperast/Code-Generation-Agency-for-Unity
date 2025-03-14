// Assets\Sanat\CodeGenerator\Editor\Agents\AgentFunctionDefinitions.cs
using System.Collections.Generic;
using Sanat.ApiAnthropic;
using Sanat.ApiGemini;
using Sanat.ApiOpenAI;

namespace Sanat.CodeGenerator.Agents
{
    public class AgentFunctionDefinitions
    {
        #region SplitTaskToSingleFiles
        public const string TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES = "SplitTaskToSingleFiles";
        private const string PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH = "FilePath";
        private const string PROPERTY_SplitTaskToSingleFiles_TASK_ID = "TaskId";
        private const string FUNCTION_SPLIT_TASK_TO_SINGLE_FILES_DESCRIPTION = "Splits the task into multiple files. It should be used when you want to split the task into multiple files.";
        private const string PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH_DESCRIPTION = "Filepath of the task file, e.g. Assets\\Path\\To\\TaskFile.cs";
        private const string PROPERTY_SplitTaskToSingleFiles_TASK_DEFINITION_DESCRIPTION = "Should be integer number 0 for Modify or integer number 1 for Create.";

        public ApiGemini.FunctionDeclaration GetFunctionData_GeminiSplitTaskToSingleFiles()
        {
            ApiGemini.FunctionDeclarationSchema parameters = new ApiGemini.FunctionDeclarationSchema
            {
                type = ApiGemini.FunctionDeclarationSchemaType.OBJECT,
                properties = new Dictionary<string, ApiGemini.FunctionDeclarationSchemaProperty>
                {
                    { PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.STRING, description = PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH_DESCRIPTION} },
                    { PROPERTY_SplitTaskToSingleFiles_TASK_ID, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.INTEGER, description = PROPERTY_SplitTaskToSingleFiles_TASK_DEFINITION_DESCRIPTION} }
                },
                required = new List<string> { PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH, PROPERTY_SplitTaskToSingleFiles_TASK_ID }
                
            };
            return new ApiGemini.FunctionDeclaration
            {
                name = TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES,
                description = FUNCTION_SPLIT_TASK_TO_SINGLE_FILES_DESCRIPTION,
                parameters = parameters
            };
        }
        
        public ApiOpenAI.ToolFunction GetFunctionData_OpenaiSplitTaskToSingleFiles()
        {
            ApiOpenAI.Parameter parameters = new ApiOpenAI.Parameter();
            parameters.AddProperty(PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH, ApiOpenAI.DataTypes.STRING, PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_SplitTaskToSingleFiles_TASK_ID, ApiOpenAI.DataTypes.NUMBER, PROPERTY_SplitTaskToSingleFiles_TASK_DEFINITION_DESCRIPTION);
            parameters.Required.Add(PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH);
            parameters.Required.Add(PROPERTY_SplitTaskToSingleFiles_TASK_ID);

            ApiOpenAI.ToolFunction function = new ApiOpenAI.ToolFunction(TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES, FUNCTION_SPLIT_TASK_TO_SINGLE_FILES_DESCRIPTION, parameters);
            return function;
        }

        public ApiAnthropic.ApiAntrophicData.ToolFunction GetFunctionData_AntrophicSplitTaskToSingleFiles()
        {
            ApiAnthropic.ApiAntrophicData.InputSchema parameters = new ApiAnthropic.ApiAntrophicData.InputSchema();
            parameters.AddProperty(PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH, ApiAnthropic.ApiAntrophicData.DataTypes.STRING, PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_SplitTaskToSingleFiles_TASK_ID, ApiAnthropic.ApiAntrophicData.DataTypes.NUMBER, PROPERTY_SplitTaskToSingleFiles_TASK_DEFINITION_DESCRIPTION);
            parameters.Required.Add(PROPERTY_SplitTaskToSingleFiles_TASK_FILEPATH);
            parameters.Required.Add(PROPERTY_SplitTaskToSingleFiles_TASK_ID);

            ApiAnthropic.ApiAntrophicData.ToolFunction function = new ApiAnthropic.ApiAntrophicData.ToolFunction(TOOL_NAME_SPLIT_TASK_TO_SINGLE_FILES, FUNCTION_SPLIT_TASK_TO_SINGLE_FILES_DESCRIPTION, parameters);
            return function;
        }
        
        #endregion

        #region Tool_ReplaceScriptFile
        public const string TOOL_NAME_REPLACE_SCRIPT_FILE = "ReplaceScriptFile";
        private const string PROPERTY_ReplaceScriptFile_FILEPATH = "FilePath";
        private const string PROPERTY_ReplaceScriptFile_CONTENT = "Content";
        private const string FUNCTION_REPLACE_SCRIPT_FILE_DESCRIPTION = "Fully replaces script file code content with new code content. It should be used when you want to replace the content of a script file with new content.";
        private const string FUNCTION_PROPERTY_ReplaceScriptFile_FILEPATH_DESCRIPTION = "Filepath of the code snippet, e.g. Assets\\Path\\To\\File.cs";
        private const string FUNCTION_PROPERTY_ReplaceScriptFile_CONTENT_DESCRIPTION = "FULL code for selected filepath, partial code snippets are NOT ALLOWED.";
        
        public ApiOpenAI.ToolFunction GetFunctionData_OpenaiSReplaceScriptFile()
        {
            ApiOpenAI.Parameter parameters = new ApiOpenAI.Parameter();
            parameters.AddProperty(PROPERTY_ReplaceScriptFile_FILEPATH, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_ReplaceScriptFile_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_ReplaceScriptFile_CONTENT, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_ReplaceScriptFile_CONTENT_DESCRIPTION);
            parameters.Required.Add(PROPERTY_ReplaceScriptFile_FILEPATH);
            parameters.Required.Add(PROPERTY_ReplaceScriptFile_CONTENT);
            ApiOpenAI.ToolFunction function = new ApiOpenAI.ToolFunction(TOOL_NAME_REPLACE_SCRIPT_FILE, FUNCTION_REPLACE_SCRIPT_FILE_DESCRIPTION, parameters);
            return function;
        }

        public ApiGemini.FunctionDeclaration GetFunctionData_GeminiReplaceScriptFile()
        {
            ApiGemini.FunctionDeclarationSchema parameters = new ApiGemini.FunctionDeclarationSchema
            {
                type = ApiGemini.FunctionDeclarationSchemaType.OBJECT,
                properties = new Dictionary<string, ApiGemini.FunctionDeclarationSchemaProperty>
                 {
                    { PROPERTY_ReplaceScriptFile_FILEPATH, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.STRING, description = FUNCTION_PROPERTY_ReplaceScriptFile_FILEPATH_DESCRIPTION} },
                    { PROPERTY_ReplaceScriptFile_CONTENT, new ApiGemini.FunctionDeclarationSchemaProperty{ type = ApiGemini.FunctionDeclarationSchemaType.STRING, description = FUNCTION_PROPERTY_ReplaceScriptFile_CONTENT_DESCRIPTION} },
                },
                required = new List<string> { PROPERTY_ReplaceScriptFile_FILEPATH, PROPERTY_ReplaceScriptFile_CONTENT }
            };

            return new ApiGemini.FunctionDeclaration
            {
                name = TOOL_NAME_REPLACE_SCRIPT_FILE,
                description = FUNCTION_REPLACE_SCRIPT_FILE_DESCRIPTION,
                parameters = parameters
            };
        }

        public ApiAnthropic.ApiAntrophicData.ToolFunction GetFunctionData_AntrophicReplaceScriptFile()
        {
            ApiAnthropic.ApiAntrophicData.InputSchema parameters = new ApiAnthropic.ApiAntrophicData.InputSchema();
            parameters.AddProperty(PROPERTY_ReplaceScriptFile_FILEPATH, ApiAnthropic.ApiAntrophicData.DataTypes.STRING, FUNCTION_PROPERTY_ReplaceScriptFile_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_ReplaceScriptFile_CONTENT, ApiAnthropic.ApiAntrophicData.DataTypes.STRING, FUNCTION_PROPERTY_ReplaceScriptFile_CONTENT_DESCRIPTION);
            parameters.Required.Add(PROPERTY_ReplaceScriptFile_FILEPATH);
            parameters.Required.Add(PROPERTY_ReplaceScriptFile_CONTENT);
            ApiAnthropic.ApiAntrophicData.ToolFunction function = new ApiAnthropic.ApiAntrophicData.ToolFunction(TOOL_NAME_REPLACE_SCRIPT_FILE, FUNCTION_REPLACE_SCRIPT_FILE_DESCRIPTION, parameters);
            return function;
        }
        #endregion

        #region Tool_MergeCode
        public const string TOOL_NAME_MERGE_CODE = "MergeCode";
        private const string PROPERTY_MergeCode_FILEPATH = "FilePath";
        private const string PROPERTY_MergeCode_CONTENT = "Content";
        private const string FUNCTION_MERGE_CODE_DESCRIPTION = "Merges old and new code into a single file.";
        private const string FUNCTION_PROPERTY_MergeCode_FILEPATH_DESCRIPTION = "File path of the merged code";
        private const string FUNCTION_PROPERTY_MergeCode_CONTENT_DESCRIPTION = "Full merged code content";

        public ApiOpenAI.ToolFunction GetFunctionData_OpenaiMergeCode() {
            ApiOpenAI.Parameter parameters = new ApiOpenAI.Parameter();
            parameters.AddProperty(PROPERTY_MergeCode_FILEPATH, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_MergeCode_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_MergeCode_CONTENT, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_MergeCode_CONTENT_DESCRIPTION);
            parameters.Required.Add(PROPERTY_MergeCode_FILEPATH);
            parameters.Required.Add(PROPERTY_MergeCode_CONTENT);
            ApiOpenAI.ToolFunction function = new ApiOpenAI.ToolFunction(TOOL_NAME_MERGE_CODE, FUNCTION_MERGE_CODE_DESCRIPTION, parameters);
            return function;
        }

        public ApiGemini.FunctionDeclaration GetFunctionData_GeminiMergeCode() {
            ApiGemini.FunctionDeclarationSchema parameters = new ApiGemini.FunctionDeclarationSchema {
                type = ApiGemini.FunctionDeclarationSchemaType.OBJECT,
                properties = new Dictionary<string, ApiGemini.FunctionDeclarationSchemaProperty> {
                    { PROPERTY_MergeCode_FILEPATH, new ApiGemini.FunctionDeclarationSchemaProperty{
                        type = ApiGemini.FunctionDeclarationSchemaType.STRING,
                        description = FUNCTION_PROPERTY_MergeCode_FILEPATH_DESCRIPTION
                    }},
                    { PROPERTY_MergeCode_CONTENT, new ApiGemini.FunctionDeclarationSchemaProperty{
                        type = ApiGemini.FunctionDeclarationSchemaType.STRING,
                        description = FUNCTION_PROPERTY_MergeCode_CONTENT_DESCRIPTION
                    }},
                },
                required = new List<string> { PROPERTY_MergeCode_FILEPATH, PROPERTY_MergeCode_CONTENT }
            };
            
            return new ApiGemini.FunctionDeclaration {
                name = TOOL_NAME_MERGE_CODE,
                description = FUNCTION_MERGE_CODE_DESCRIPTION,
                parameters = parameters
            };
        }

        public ApiAnthropic.ApiAntrophicData.ToolFunction GetFunctionData_AntrophicMergeCode() {
            ApiAnthropic.ApiAntrophicData.InputSchema parameters = new ApiAnthropic.ApiAntrophicData.InputSchema();
            parameters.AddProperty(PROPERTY_MergeCode_FILEPATH, ApiAnthropic.ApiAntrophicData.DataTypes.STRING, FUNCTION_PROPERTY_MergeCode_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_MergeCode_CONTENT, ApiAnthropic.ApiAntrophicData.DataTypes.STRING, FUNCTION_PROPERTY_MergeCode_CONTENT_DESCRIPTION);
            parameters.Required.Add(PROPERTY_MergeCode_FILEPATH);
            parameters.Required.Add(PROPERTY_MergeCode_CONTENT);
            ApiAnthropic.ApiAntrophicData.ToolFunction function = new ApiAnthropic.ApiAntrophicData.ToolFunction(TOOL_NAME_MERGE_CODE, FUNCTION_MERGE_CODE_DESCRIPTION, parameters);
            return function;
        }
        #endregion

        // Add SplitCodeToFilePathes tool functions
        #region Tool_SplitCodeToFilePathes
        public const string TOOL_NAME_SPLIT_CODE_TO_FILE_PATHES = "InsertCodeToPath";
        private const string PROPERTY_SplitCodeToFilePathes_FILEPATH = "FilePath";
        private const string PROPERTY_SplitCodeToFilePathes_CONTENT = "Content";
        private const string FUNCTION_SPLIT_CODE_TO_FILE_PATHES_DESCRIPTION = "Inserts code into the file.";
        private const string FUNCTION_PROPERTY_SplitCodeToFilePathes_FILEPATH_DESCRIPTION = "AI must tell filepath of the code snippet";
        private const string FUNCTION_PROPERTY_SplitCodeToFilePathes_CONTENT_DESCRIPTION = "AI must provide FULL code snippet for selected filepath";

        public ApiOpenAI.ToolFunction GetFunctionData_OpenaiSplitCodeToFilePathes() {
            ApiOpenAI.Parameter parameters = new ApiOpenAI.Parameter();
            parameters.AddProperty(PROPERTY_SplitCodeToFilePathes_FILEPATH, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_SplitCodeToFilePathes_FILEPATH_DESCRIPTION);
            parameters.AddProperty(PROPERTY_SplitCodeToFilePathes_CONTENT, ApiOpenAI.DataTypes.STRING, FUNCTION_PROPERTY_SplitCodeToFilePathes_CONTENT_DESCRIPTION);
            parameters.Required.Add(PROPERTY_SplitCodeToFilePathes_FILEPATH);
            parameters.Required.Add(PROPERTY_SplitCodeToFilePathes_CONTENT);
            ApiOpenAI.ToolFunction function = new ApiOpenAI.ToolFunction(TOOL_NAME_SPLIT_CODE_TO_FILE_PATHES, FUNCTION_SPLIT_CODE_TO_FILE_PATHES_DESCRIPTION, parameters);
            return function;
        }
        #endregion
    }
}
