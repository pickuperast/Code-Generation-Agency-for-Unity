using System;
using System.Collections.Generic;
using Sanat.ApiOpenAI;
using UnityEngine;
using UnityEngine.Networking;
using Sanat.ApiOpenAI;

namespace Sanat.ApiGroq
{
    public static class Groq
    {
        public static string BaseURL { get; set; } = "https://api.groq.com/openai/v1/";
        public static string ChatURL => $"{BaseURL}chat/completions";

        public static bool IsApiKeyValid(string apiKey)
        {
            return !string.IsNullOrEmpty(apiKey);
        }

        public static UnityWebRequestAsyncOperation SubmitChatAsync(string apiKey, ApiOpenAI.Model model, float temp, int maxTokens,
            List<ChatMessage> messages, Action<string> callback)
        {
            var chatRequest = new ChatRequest(model.Name, messages,temp, maxTokens);
            string jsonData = JsonUtility.ToJson(chatRequest);

            UnityWebRequest webRequest = CreateWebRequest(apiKey, ChatURL, jsonData);

            UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();

            asyncOp.completed += (op) =>
            {
                var success = webRequest.result == UnityWebRequest.Result.Success;
                var text = success ? webRequest.downloadHandler.text : string.Empty;
                if (!success) Debug.Log($"{webRequest.error}\n{webRequest.downloadHandler.text}");
                webRequest.Dispose();
                webRequest = null;

                if (!string.IsNullOrEmpty(text))
                {
                    var responseData = JsonUtility.FromJson<ChatResponse>(text);
                    text = responseData.choices[0].message.content.Trim();
                    var tokensPrompt = responseData.usage.prompt_tokens;
                    var tokensCompletion = responseData.usage.completion_tokens;
                    var tokensTotal = responseData.usage.total_tokens;
                    
                    Debug.Log($"{model} Usage: prompt_tokens: {tokensPrompt}; " +
                              $"completion_tokens: {tokensCompletion}; " +
                              $"total_tokens: {tokensTotal}");
                }
                callback?.Invoke(text);
            };

            return asyncOp;
        }

        public static UnityWebRequest CreateWebRequest(string apiKey, string url, string jsonData)
        {
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(jsonData);

            UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(postData);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.disposeUploadHandlerOnDispose = true;
            webRequest.disposeDownloadHandlerOnDispose = true;
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            return webRequest;
        }

        public static UnityWebRequestAsyncOperation SubmitToolChatAsync(
            string apiKey, Model model, 
            float temperature, float top_p,
            float frequency_penalty, float presence_penalty, 
            int maxTokens,
            List<ChatMessage> messages, Action<CompletionResponse> callback, Tool[] tools, string tool_choice = "required"
            ){
            var chatRequest = new ChatRequest(model.Name, messages, 
                temperature, top_p, frequency_penalty, presence_penalty,
                maxTokens);
            string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(chatRequest);
            if (tools != null)
            {
                jsonData = jsonData.Insert(jsonData.Length - 1, $",\"tool_choice\": \"required\"");
                // Append  tools to the jsonData
                var toolsJson = Newtonsoft.Json.JsonConvert.SerializeObject(tools);
                jsonData = jsonData.Insert(jsonData.Length - 1, $",\"tools\": {toolsJson}");
            }

            UnityWebRequest webRequest = CreateWebRequest(apiKey, ChatURL, jsonData);

            UnityWebRequestAsyncOperation asyncOp = webRequest.SendWebRequest();

            asyncOp.completed += (op) =>
            {
                var success = webRequest.result == UnityWebRequest.Result.Success;
                var text = success ? webRequest.downloadHandler.text : string.Empty;
                if (!success) Debug.Log($"{webRequest.error}\n{webRequest.downloadHandler.text}");
                webRequest.Dispose();
                webRequest = null;

                if (!string.IsNullOrEmpty(text))
                {
                    var responseData = JsonUtility.FromJson<CompletionResponse>(text);
                    callback?.Invoke(responseData);
            
                    var tokensPrompt = responseData.usage.prompt_tokens;
                    var tokensCompletion = responseData.usage.completion_tokens;
                    var tokensTotal = responseData.usage.total_tokens;
                    var prompt_price = 0;
                    var response_price = 0;
                    var costPrompt = tokensPrompt * prompt_price / 1000;
                    var costResponse = tokensCompletion * response_price / 1000;
                    var cost = costPrompt + costResponse;
                    Debug.Log($"{model.Name} Usage({cost.ToString("F3")}$): prompt_tokens: {tokensPrompt}; completion_tokens: {tokensCompletion}; total_tokens: {tokensTotal}");
                }
            };

            return asyncOp;
        }
    }
}