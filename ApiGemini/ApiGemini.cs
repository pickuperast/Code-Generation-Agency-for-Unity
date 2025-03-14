// Assets\Sanat\CodeGenerator\ApiGemini\ApiGemini.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Sanat.CodeGenerator.Common;

namespace Sanat.ApiGemini
{
    public static class Gemini
    {
        public static string BaseURL2 { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/";
        public const string PREFS_GEMINI_PROJECT_NAME = "GeminiProjectName";
        public static string TextToSpeechURL = "https://texttospeech.googleapis.com/v1/text:synthesize";

        
        public static UnityWebRequestAsyncOperation SubmitChatAsync(
            string apiKey,
            string model,
            ChatRequest chatRequest,
            Action<string> callback)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            };

            string jsonData = JsonConvert.SerializeObject(chatRequest, settings);
            return SendRequestRecursively(apiKey, model, jsonData, chatRequest, callback);
        }

        private static UnityWebRequestAsyncOperation SendRequestRecursively(
            string apiKey,
            string model,
            string jsonData,
            ChatRequest chatRequest,
            Action<string> callback,
            int retryCount = 0,
            int maxRetries = 3)
        {
            string generatedUrl = $"{BaseURL2}{model}:generateContent?key={apiKey}";
            UnityWebRequest webRequest = CreateWebRequest(apiKey, generatedUrl, jsonData);
            UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();
            bool isToolCalling = chatRequest.tools != null && chatRequest.tools.Count > 0;
            var startTime = DateTime.Now;

            asyncOp.completed += (op) =>
            {
                var elapsedTime = (DateTime.Now - startTime).TotalSeconds;
                var success = webRequest.result == UnityWebRequest.Result.Success;
                var text = success ? webRequest.downloadHandler.text : string.Empty;

                if (!success)
                {
                    Debug.Log($"webRequest.error: {webRequest.error} \nwebRequest.downloadHandler.text: {webRequest.downloadHandler.text}");
                    Debug.Log($"{model} Retry #{retryCount + 1} due to: {(text.Contains("MAX_TOKENS") ? "MAX_TOKENS" : "print(default_api")}");
                    
                    if (retryCount > 10)
                    {
                        Debug.Log($"{model} Retry #{retryCount + 1} due to: retryCount > 10");
                        return;
                    }else if (webRequest.downloadHandler.text.Contains("RESOURCE_EXHAUSTED") || retryCount > 1)
                    {
                        model = ApiGemini.Model.FlashExp.Name;
                    }else if (retryCount > 2)
                    {
                        model = ApiGemini.Model.Flash.Name;
                    }
                    
                    webRequest.Dispose();
                    SendRequestRecursively(apiKey, model, jsonData, chatRequest, callback, retryCount + 1, maxRetries);
                    return;
                }

                if (isToolCalling && (text.Contains("MAX_TOKENS") || text.Contains("print(default_api.")) && retryCount < maxRetries)
                {
                    Debug.Log($"{model} Retry #{retryCount + 1} due to: {(text.Contains("MAX_TOKENS") ? "MAX_TOKENS" : "print(default_api")}");
                    webRequest.Dispose();
                    if (retryCount > 1)
                    {
                        Debug.Log($"Retry #{retryCount + 1} due to: retryCount > 1");
                        model = ApiGemini.Model.FlashExp.Name;
                    }
                    SendRequestRecursively(apiKey, model, jsonData, chatRequest, callback, retryCount++, maxRetries);
                    return;
                }

                // Process successful response
                string answer = ProcessResponse(text, model, elapsedTime);
                webRequest.Dispose();
                
                if (!text.Contains("MAX_TOKENS")) // Only invoke callback if not hitting token limit
                {
                    callback?.Invoke(answer);
                }
            };

            return asyncOp;
        }

        private static string ProcessResponse(string text, string model, double elapsedTime)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var m_Model = Model.GetModelByName(model);
            var responseData = JsonConvert.DeserializeObject<ChatResponse>(text);
            if (responseData?.candidates == null || responseData.candidates.Count == 0)
                return "No candidates found in response.";

            var candidate = responseData.candidates[0];
            var part = candidate.content.parts[0];
            string answer;

            if (part.functionCall != null && part.functionCall.name != null)
            {
                foreach (var pt in candidate.content.parts)
                {
                    Debug.Log($"{model} Function Call: {pt.functionCall.name}, Args: {JsonConvert.SerializeObject(pt.functionCall.args)}");
                }
                answer = text;
            }
            else if (part.text != null)
            {
                answer = part.text.Trim();
            }
            else
            {
                answer = "No candidates found in response.";
            }

            // Log usage metrics
            float tokensPrompt = responseData.usageMetadata.promptTokenCount / 1000f;
            var tokensCompletion = responseData.usageMetadata.candidatesTokenCount / 1000f;
            var tokensTotal = responseData.usageMetadata.totalTokenCount / 1000f;
            if (tokensPrompt > 8f)
            {
                Debug.Log($"{model}: {text}");
            }
            
            float inputCost = (tokensPrompt / 1000000f) * m_Model.InputPricePerMil;
            float outputCost = (tokensCompletion / 1000000f) * m_Model.OutputPricePerMil;
            float totalCost = inputCost + outputCost;
            
            Debug.Log($"{model} [<color=orange>{elapsedTime:F0}</color> sec] Usage(<color=green>{totalCost:F3}</color>$): {CommonForAnyApi.OUTPUT_TOKENS_SYMBOL} {tokensPrompt:F1}K; {CommonForAnyApi.INPUT_TOKENS_SYMBOL} {tokensCompletion:F1}K; total_tokens: {tokensTotal:F1}K");
            return answer;
        }

        private static UnityWebRequest CreateWebRequest(string apiKey, string url, string jsonData)
        {
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);
            UnityWebRequest webRequest = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(postData),
                downloadHandler = new DownloadHandlerBuffer(),
                disposeUploadHandlerOnDispose = true,
                disposeDownloadHandlerOnDispose = true
            };
            webRequest.SetRequestHeader("Content-Type", "application/json");
            return webRequest;
        }

        public static void GenerateTextToSpeech(string text, string projectName, string languageCode, string name, string ssmlGender, Action<AudioClip> callback)
        {
            var request = new TextToSpeechRequest(text, languageCode, name, ssmlGender);
            string jsonData = JsonUtility.ToJson(request);
            UnityWebRequest webRequest = CreateWebRequest(projectName, TextToSpeechURL, jsonData);
            UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();

            asyncOp.completed += (op) =>
            {
                var success = webRequest.result == UnityWebRequest.Result.Success;
                if (!success)
                {
                    Debug.Log($"{webRequest.error} projectName: {projectName}\n{webRequest.downloadHandler.text}");
                    callback?.Invoke(null);
                    return;
                }

                var audioData = webRequest.downloadHandler.data;
                float[] floatData = ConvertByteToFloatArray(audioData);
                AudioClip audioClip = AudioClip.Create("TextToSpeech", floatData.Length, 1, 44100, false);
                audioClip.SetData(floatData, 0);
                callback?.Invoke(audioClip);
            };
        }
        
        public static float[] ConvertByteToFloatArray(byte[] byteArray)
        {
            int floatArrayLength = byteArray.Length / 4;
            float[] floatArray = new float[floatArrayLength];
            for (int i = 0; i < floatArrayLength; i++)
            {
                floatArray[i] = BitConverter.ToSingle(byteArray, i * 4);
            }
            return floatArray;
        }
    }
}