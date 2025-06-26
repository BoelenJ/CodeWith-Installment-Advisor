using Azure.AI.Agents.Persistent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using System.Threading;
using System.Threading.Tasks;
using static InstallmentAdvisor.ChatApi.Controllers.ChatController;

namespace InstallmentAdvisor.ChatApi.Agents;

public class SkillAgent : AgentApplication
{
    private readonly Kernel _kernel;
    private readonly AgentService _agentService;
    private readonly PersistentAgentsClient _aiFoundryClient;
    public SkillAgent(AgentApplicationOptions options, Kernel kernel, PersistentAgentsClient foundryClient, AgentService agentService) : base(options)
    {
        _kernel = kernel;
        _aiFoundryClient = foundryClient;
        _agentService = agentService;
        OnActivity(ActivityTypes.EndOfConversation, EndOfConversationAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync);

        OnTurnError(async (turnContext, turnState, exception, cancellationToken) =>
        {
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);

            var eoc = Activity.CreateEndOfConversationActivity();
            eoc.Code = EndOfConversationCodes.Error;
            eoc.Text = exception.Message;
            await turnContext.SendActivityAsync(eoc, cancellationToken);
        });
    }

    protected async Task EndOfConversationAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
    }

    protected async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Text.Contains("end"))
        {
            await turnContext.SendActivityAsync("(EchoSkill) Ending conversation...", cancellationToken: cancellationToken);

            // Indicate this conversation is over by sending an EndOfConversation Activity.
            // This Agent doesn't return a value, but if it did it could be put in Activity.Value.
            var endOfConversation = Activity.CreateEndOfConversationActivity();
            endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
            await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
        }
        else
        {
            AzureAIAgent orchestratorAgent = _agentService.OrchestratorAgent;
            AgentResponseItem<ChatMessageContent> chatResponse = await orchestratorAgent.InvokeAsync(turnContext.Activity.Text).FirstAsync();
            if (chatResponse != null) {
                // Send the response back to the user.
                await turnContext.SendActivityAsync(chatResponse.Message.Content!, cancellationToken: cancellationToken);
            }
            else
            {
                // If no response was generated, send a default message.
                await turnContext.SendActivityAsync("I didn't understand that. Please try again.", cancellationToken: cancellationToken);
            }
        }
    }
}
