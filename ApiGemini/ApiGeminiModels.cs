// Copyright (c) Sanat. All rights reserved.
namespace Sanat.ApiGemini
{
    public enum ModelType { Chat }

    public class Model
    {
        public string Name { get; set; }
        public ModelType ModelType { get; private set; }
        public int MaxInputTokens { get; private set; }
        public int MaxOutputTokens { get; private set; }
        public float InputPricePerMil { get; private set; }
        public float OutputPricePerMil { get; private set; }
        const string FlashModelName = "gemini-1.5-flash-latest";
        const string ProModelName = "gemini-1.5-pro";
        const string Flash2ModelName = "gemini-2.0-flash";
        const string FlashLite2ModelName = "gemini-2.0-flash-lite";
        const string FlashExp2ModelName = "gemini-2.0-flash-exp";
        const string ProExpToModelName = "gemini-2.0-pro-exp-02-05";
        const string TwentyFiveProExpToModelName = "gemini-2.5-pro-exp-03-25";

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
                case Flash2ModelName:
                    return Pro;
                case ProModelName:
                    return Flash;
                case FlashModelName:
                    return Flash;
                case FlashModelName:
                    return Flash;
                case TwentyFiveProExpToModelName:
                    return ProExp25;
                default:
                    return Flash;
            }
        }
        
        public static string DowngradeModel(string modelName)
        {
            switch (modelName)
            {
                case "gemini-2.5-pro-exp-03-25":
                    return "gemini-2.0-pro-exp-02-05";
                case "gemini-2.0-pro-exp-02-05":
                    return "gemini-2.0-flash-lite";
                case "gemini-2.0-flash-lite":
                    return "gemini-2.0-flash";
                case "gemini-2.0-flash":
                    return "gemini-2.0-flash-exp";
                case "gemini-2.0-flash-exp":
                    return "gemini-1.5-pro";
                case "gemini-1.5-pro":
                    return "gemini-1.5-flash-latest";
                default:
                    return "";
            }
        }
       
        public static Model Flash { get; } = new Model(FlashModelName, ModelType.Chat, 1048576, 3f, 15f, 8192);
        public static Model FlashExp { get; } = new Model(FlashExp2ModelName, ModelType.Chat, 1048576, 3f, 15f, 8192);
        
        public static Model Pro { get; } = new Model(ProModelName, ModelType.Chat, 1048576, 3f, 15f, 8192);
        
        public static Model Flash2 { get; } = new Model(Flash2ModelName, ModelType.Chat, 1048576, 0f, 0f, 8192);

        public static Model ProExp { get; } = new Model(ProExpToModelName, ModelType.Chat, 1048576, 0f, 0f, 8192);
        public static Model ProExp25 { get; } = new Model(TwentyFiveProExpToModelName, ModelType.Chat, 1048576, 0f, 0f, 8192);

    }
}