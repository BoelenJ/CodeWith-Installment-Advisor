using Azure.AI.Agents.Persistent;
using InstallmentAdvisor.ChatApi.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using ModelContextProtocol.Client;

namespace InstallmentAdvisor.ChatApi.Agents
{
    public class FoundryAgentFactory
    {
        public async static Task<AzureAIAgent> CreateAgentAsync(PersistentAgentsClient client, string agentName, string instructions, string modelName, List<McpClientTool>? tools)
        {
            PersistentAgent agentDefinition = await client.Administration.CreateAgentAsync(
                model: modelName,
                name: agentName,
                instructions: instructions,
                tools: [new CodeInterpreterToolDefinition()]
            );
            AzureAIAgent agent = new(agentDefinition, client);
            AddMcpTools(agent, tools);

            return agent;
        }

        public static async Task<AzureAIAgent> GetAgentAsync(PersistentAgentsClient client, string agentId, List<McpClientTool>? tools)
        {

            PersistentAgent agentDefinition = await client.Administration.GetAgentAsync(agentId);
            AzureAIAgent agent = new(agentDefinition, client);

            AddMcpTools(agent, tools);

            return agent;
        }

        public static async Task<bool> DeleteAgentAsync(PersistentAgentsClient client, string agentId)
        {
            if (!string.IsNullOrEmpty(agentId))
            {
                return await client.Administration.DeleteAgentAsync(agentId);
            }

            return false;
        }
        private static void AddMcpTools(AzureAIAgent agent, List<McpClientTool>? tools)
        {
            if (tools != null && tools.Count > 0)
            {
                agent.Kernel.Plugins.AddFromFunctions("MCP", tools.Select(tool => tool.AsKernelFunction()));
            }
        }
    }
}
