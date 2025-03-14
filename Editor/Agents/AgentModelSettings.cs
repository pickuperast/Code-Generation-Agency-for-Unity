using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Sanat.CodeGenerator.Agents
{
    [System.Serializable]
    public class AgentModelSettings
    {
        public string AgentName;
        public AbstractAgentHandler.ApiProviders ApiProvider;
        public string ModelName;
        public List<string> SelectedMemoryFiles = new List<string>();
    }
}