using Azure.AI.Agents.Persistent;
using InstallmentAdvisor.ChatApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;

namespace InstallmentAdvisor.ChatApi.Agents
{
    public class OrchestratorAgentFactory
    {
        public static async Task<AzureAIAgent> CreateAgentAsync(PersistentAgentsClient client, string agentName, string instructions, string modelName, List<Agent>? subAgents, List<ToolCall>? toolCallList)
        {

            PersistentAgent agentDefinition = await client.Administration.CreateAgentAsync(
                model: modelName,
                name: agentName,
                instructions: instructions
            );
            AzureAIAgent agent = new(agentDefinition, client);

            RegisterSubAgents(agent, subAgents);

            if (toolCallList != null)
            {
                agent.Kernel.AutoFunctionInvocationFilters.Add(new AutoFunctionInvocationFilter(toolCallList));
            }

            return agent;
        }
        public static AzureAIAgentThread GetOrCreateThread(AzureAIAgent agent, string? threadId)
        {

            if (threadId == null)
            {
                return new AzureAIAgentThread(agent.Client);
            } else
            {
                return new AzureAIAgentThread(agent.Client, threadId);
            }
        }

        public static async Task<AzureAIAgent> GetAgentAsync(PersistentAgentsClient client, string agentId, List<Agent>? subAgents, List<ToolCall>? toolCallList)
        {

            PersistentAgent agentDefinition = await client.Administration.GetAgentAsync(agentId);
            AzureAIAgent agent = new(agentDefinition, client);

            RegisterSubAgents(agent, subAgents);

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

        private static void RegisterSubAgents(AzureAIAgent agent, List<Agent>? subAgents)
        {
            if (subAgents != null && subAgents.Count > 0)
            {
                List<KernelFunction> subAgentAsFunctions = new List<KernelFunction>();
                foreach (Agent subAgent in subAgents)
                {
                    subAgentAsFunctions.Add(AgentKernelFunctionFactory.CreateFromAgent(subAgent));
                }
                KernelPlugin agentPlugin = KernelPluginFactory.CreateFromFunctions("AgentsPlugin", subAgentAsFunctions);
                agent.Kernel.Plugins.Add(agentPlugin);
            }
        }
        public class AutoFunctionInvocationFilter(List<ToolCall> toolCallList) : IAutoFunctionInvocationFilter
        {
            public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
            {
                await next(context);
                var parametersList = new List<ToolParameter>();

                if (context.Arguments != null)
                {
                    parametersList = context.Arguments.Select(p => new ToolParameter { Key = p.Key, Value = p.Value?.ToString() }).ToList();
                }
                var response = context.Result.GetValue<ChatMessageContent[]>()?.First();
                ToolCall toolCallInfo = new ToolCall
                {
                    FunctionName = context.Function.Name,
                    PluginName = context.Function.PluginName!,
                    Parameters = parametersList,
                    Response = response?.Content
                };

                toolCallList.Add(toolCallInfo);
            }
        }
    }
}
