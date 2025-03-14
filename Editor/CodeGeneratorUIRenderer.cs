// Assets/Sanat/CodeGenerator/Editor/CodeGeneratorUIRenderer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sanat.ApiGemini;
using Sanat.CodeGenerator;
using Sanat.CodeGenerator.Agents;
using Sanat.CodeGenerator.Editor;
using UnityEditor;
using UnityEngine;

public class CodeGeneratorUIRenderer
{
    private bool isSelectedClassesVisible = true;
    private bool isGeneratedPromptVisible = true;
    private bool isGenerateCodeButtonDisabled = false;
    private double generateCodeButtonDisableTime = 0;

    public void RenderMainUI(CodeGenerator codeGenerator)
    {
        if (!codeGenerator.IsSettingsLoaded) return;
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Task Description", EditorStyles.boldLabel);
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
        textAreaStyle.wordWrap = true;
        float textAreaHeight = 3 * EditorGUIUtility.singleLineHeight;
        codeGenerator.taskScrollPosition =
            EditorGUILayout.BeginScrollView(codeGenerator.taskScrollPosition, GUILayout.Height(textAreaHeight));
        codeGenerator.taskInput =
            EditorGUILayout.TextArea(codeGenerator.taskInput, textAreaStyle, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        GUILayout.Space(20);
        if (codeGenerator.isSettingsVisible)
            codeGenerator.DrawSettingsFields();
        if (codeGenerator.isAgentSettingsVisible)
            DrawAgentSettingsUI(codeGenerator);

        EditorGUILayout.LabelField("Select Class Names:", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh Class List"))
        {
            codeGenerator.RefreshClassList();
        }

        EditorGUILayout.BeginHorizontal();
        string settingsButtonLabel = codeGenerator.isSettingsVisible ? "Close Settings" : "Settings";
        if (GUILayout.Button(settingsButtonLabel))
        {
            codeGenerator.isSettingsVisible = !codeGenerator.isSettingsVisible;
        }

        string agentSettingsButtonLabel =
            codeGenerator.isAgentSettingsVisible ? "Close Agent Settings" : "Agent Settings";
        if (GUILayout.Button(agentSettingsButtonLabel))
        {
            codeGenerator.isAgentSettingsVisible = !codeGenerator.isAgentSettingsVisible;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        codeGenerator.classNameInput = EditorGUILayout.TextField("Class Name", codeGenerator.classNameInput);
        RenderClassNameSuggestions(codeGenerator);

        EditorGUILayout.BeginHorizontal();
        string selectedClassesLabel = isSelectedClassesVisible ? "Hide Selected Classes" : "Show Selected Classes";
        if (GUILayout.Button(selectedClassesLabel))
        {
            isSelectedClassesVisible = !isSelectedClassesVisible;
        }

        if (GUILayout.Button("Clear Selected Classes"))
        {
            codeGenerator.selectedClassNames.Clear();
        }

        EditorGUILayout.EndHorizontal();

        if (isSelectedClassesVisible)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2.4f));
            RenderSelectedClassesColumn(codeGenerator, 0);
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width / 2.4f));
            RenderSelectedClassesColumn(codeGenerator, 1);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        RenderGenerationButtons(codeGenerator);

        if (codeGenerator != null && codeGenerator.IsSettingsLoaded && codeGenerator.bookmarkManager != null)
            codeGenerator.bookmarkManager.DrawBookmarksUI(codeGenerator);

        if (!codeGenerator.IsFirstOnEnableCall)
        {
            RenderGeneratedPrompt(codeGenerator);
        }

        EditorGUILayout.Space();

        if (codeGenerator.isButtonAnimating)
        {
            codeGenerator.Repaint();
        }

        if (isGenerateCodeButtonDisabled)
        {
            codeGenerator.Repaint();
        }

    }

    private void RenderSelectedClassesColumn(CodeGenerator codeGenerator, int column)
    {
        var selectedClasses = codeGenerator.selectedClassNames;
        int itemsPerColumn = (selectedClasses.Count + 1) / 2;
        int startIndex = column * itemsPerColumn;
        int endIndex = Math.Min(startIndex + itemsPerColumn, selectedClasses.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                selectedClasses.RemoveAt(i);
                return;
            }

            if (GUILayout.Button(selectedClasses[i], GUILayout.ExpandWidth(true)))
            {
                if (codeGenerator.classToPath.TryGetValue(selectedClasses[i], out string filePath))
                {
                    CodeGeneratorFileOpener.OpenScript(filePath);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void RenderClassNameSuggestions(CodeGenerator codeGenerator)
    {
        if (!string.IsNullOrEmpty(codeGenerator.classNameInput))
        {
            List<string> filteredSuggestions = FilterClassNameSuggestions(codeGenerator);
            string[] suggestions = filteredSuggestions.OrderBy(c => c).ToArray();
            if (suggestions.Length > 0)
            {
                codeGenerator.scrollPosition =
                    EditorGUILayout.BeginScrollView(codeGenerator.scrollPosition, GUILayout.Height(200));
                foreach (var suggestion in suggestions)
                {
                    if (GUILayout.Button(suggestion))
                    {
                        AddSuggestionToSelectedClasses(codeGenerator, suggestion);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }
    }

    private List<string> FilterClassNameSuggestions(CodeGenerator codeGenerator)
    {
        return codeGenerator.classToPath
            .Where(kv =>
                kv.Key.StartsWith(codeGenerator.classNameInput, StringComparison.CurrentCultureIgnoreCase) &&
                !codeGenerator._ignoredFolders.Any(ignoredFolder => kv.Value.Contains(ignoredFolder)))
            .Select(kv => kv.Key)
            .ToList();
    }

    private void AddSuggestionToSelectedClasses(CodeGenerator codeGenerator, string suggestion)
    {
        if (!codeGenerator.selectedClassNames.Contains(suggestion))
        {
            codeGenerator.selectedClassNames.Add(suggestion);
        }

        codeGenerator.classNameInput = "";
        GUI.FocusControl(null);
    }

    private void RenderGenerationButtons(CodeGenerator codeGenerator)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Prompt"))
        {
            codeGenerator.generatedPrompt = "";
        }

        GUI.backgroundColor = codeGenerator.buttonColor;
        if (GUILayout.Button("Generate Prompt"))
        {
            codeGenerator.ExecGeneratePrompt();
        }

        if (codeGenerator.isGeneratingCode)
        {
            Rect progressRect = GUILayoutUtility.GetRect(100, 20);
            EditorGUI.ProgressBar(progressRect, codeGenerator.generationProgress,
                $"Generating... {codeGenerator.generationProgress * 100:F0}%");
        }

        if (isGenerateCodeButtonDisabled && EditorApplication.timeSinceStartup >= generateCodeButtonDisableTime)
        {
            isGenerateCodeButtonDisabled = false;
        }

        // Handle the Generate Code button
        GUI.enabled = !isGenerateCodeButtonDisabled;
        if (GUILayout.Button("Generate Code"))
        {
            codeGenerator.ExecGenerateCode();
            isGenerateCodeButtonDisabled = true;
            generateCodeButtonDisableTime = EditorApplication.timeSinceStartup + 3.0;
        }

        GUI.enabled = true;

        if (GUILayout.Button("Stop"))
        {
            codeGenerator.isGeneratingCode = false;
            EditorApplication.update -= codeGenerator.UpdateProgressBar;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void RenderGeneratedPrompt(CodeGenerator codeGenerator)
    {
        if (!string.IsNullOrEmpty(codeGenerator.generatedPrompt))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Generated Prompt:", EditorStyles.boldLabel);
            string toggleLabel = isGeneratedPromptVisible ? "Hide" : "Show";
            if (GUILayout.Button(toggleLabel, GUILayout.Width(50)))
            {
                isGeneratedPromptVisible = !isGeneratedPromptVisible;
            }

            EditorGUILayout.EndHorizontal();

            if (isGeneratedPromptVisible)
            {
                codeGenerator.scrollPosition = GUILayout.BeginScrollView(codeGenerator.scrollPosition,
                    GUILayout.Height(Screen.height * 0.4f));
                codeGenerator.generatedPrompt = EditorGUILayout.TextArea(codeGenerator.generatedPrompt,
                    GUILayout.Height(20 * EditorGUIUtility.singleLineHeight));
                GUILayout.EndScrollView();
            }
        }
    }

    public void DrawAgentSettingsUI(CodeGenerator codeGenerator)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Agent Model Settings", EditorStyles.boldLabel);

        // Create memory file selection UI if it doesn't exist
        if (codeGenerator.memoryFileSelectionUI == null)
        {
            codeGenerator.memoryFileSelectionUI = new MemoryFileSelectionUI();
        }

        foreach (var agentEntry in codeGenerator.agentModelSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(agentEntry.Value.AgentName, EditorStyles.boldLabel);

            // API Provider Dropdown
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Provider", GUILayout.Width(100));
            agentEntry.Value.ApiProvider =
                (AbstractAgentHandler.ApiProviders)EditorGUILayout.EnumPopup(agentEntry.Value.ApiProvider);
            EditorGUILayout.EndHorizontal();

            // Model selection based on API Provider
            switch (agentEntry.Value.ApiProvider)
            {
                case AbstractAgentHandler.ApiProviders.OpenAI:
                    DrawModelSelection("OpenAI Model", ref agentEntry.Value.ModelName,
                        new string[]
                        {
                            Sanat.ApiOpenAI.Model.GPT4o1mini.Name,
                            Sanat.ApiOpenAI.Model.GPT4o.Name,
                            Sanat.ApiOpenAI.Model.GPT4_Turbo.Name
                        });
                    break;
                case AbstractAgentHandler.ApiProviders.Anthropic:
                    DrawModelSelection("Anthropic Model", ref agentEntry.Value.ModelName,
                        new string[]
                        {
                            Sanat.ApiAnthropic.Model.Claude35.Name,
                            Sanat.ApiAnthropic.Model.Haiku35Latest.Name,
                            Sanat.ApiAnthropic.Model.Claude37.Name
                        });
                    break;
                case AbstractAgentHandler.ApiProviders.Gemini:
                    DrawModelSelection("Gemini Model", ref agentEntry.Value.ModelName,
                        new string[]
                        {
                            Sanat.ApiGemini.Model.Pro.Name,
                            Sanat.ApiGemini.Model.Flash.Name,
                            Sanat.ApiGemini.Model.Flash2.Name
                        });
                    break;
            }

            // Add memory file selection for AgentCodeHighLevelArchitector
            if (agentEntry.Key == "AgentCodeHighLevelArchitector")
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Memory Files", EditorStyles.boldLabel);
                codeGenerator.memoryFileSelectionUI.DrawMemoryFileSelection(agentEntry.Value);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawModelSelection(string label, ref string selectedModel, string[] models)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(100));
        int selectedIndex = Array.IndexOf(models, selectedModel);
        selectedIndex = EditorGUILayout.Popup(selectedIndex, models);
        if (selectedIndex >= 0 && selectedIndex < models.Length)
        {
            selectedModel = models[selectedIndex];
        }

        EditorGUILayout.EndHorizontal();
    }
}
