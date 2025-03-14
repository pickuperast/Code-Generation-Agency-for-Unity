// Copyright (c) Sanat. All rights reserved.
namespace Sanat.ApiAnthropic
{
    public enum ModelType { Chat, Transcription }

    public class Model
    {
        public string Name { get; set; }
        public ModelType ModelType { get; private set; }
        public int MaxInputTokens { get; private set; }
        public int MaxOutputTokens { get; private set; }
        public float InputPricePerMil { get; private set; }
        public float OutputPricePerMil { get; private set; }

        public Model(string name, ModelType modelType, int maxInputTokens, float inputPricePerMil, float outputPricePerMil, int maxOutputTokens = 4095)
        {
            Name = name;
            ModelType = modelType;
            MaxInputTokens = maxInputTokens;
            MaxOutputTokens = maxOutputTokens;
            InputPricePerMil = inputPricePerMil;
            OutputPricePerMil = outputPricePerMil;
        }
        
        public static Model GetModelByName(string modelName)
        {
            switch (modelName)
            {
                case "claude-3-7-sonnet-20250219":
                    return Claude37;
                case "claude-3-5-sonnet-20240620":
                    return Claude35;
                case "claude-3-5-sonnet-20241022":
                    return Claude35Latest;
                case "claude-3-5-haiku-20241022":
                    return Haiku35Latest;
                default:
                    return Claude35;
            }
        }
        
        public static string DowngradeModel(string modelName)
        {
            switch (modelName)
            {
                case "claude-3-7-sonnet-20250219":
                    return "claude-3-5-sonnet-20240620";
                case "claude-3-5-sonnet-20241022":
                    return "claude-3-5-sonnet-20240620";
                case "claude-3-5-sonnet-20240620":
                    return "claude-3-5-haiku-20241022";
                case "claude-3-5-haiku-20240620":
                    return "claude-3-5-sonnet-20241022";
                default:
                    return "";
            }
        }
        
        public static Model Claude37 { get; } = new Model("claude-3-7-sonnet-20250219", ModelType.Chat, 200000, 3f, 15f, 8192);
       
        public static Model Claude35 { get; } = new Model("claude-3-5-sonnet-20240620", ModelType.Chat, 200000, 3f, 15f, 8192);
        
        public static Model Claude35Latest { get; } = new Model("claude-3-5-sonnet-20241022", ModelType.Chat, 200000, 3f, 15f, 8192);
        
        public static Model Haiku35Latest { get; } = new Model("claude-3-5-haiku-20241022", ModelType.Chat, 200000, 1f, 5f, 8192);
    }
}