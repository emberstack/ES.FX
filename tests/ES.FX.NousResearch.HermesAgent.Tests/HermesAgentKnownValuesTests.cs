using ES.FX.NousResearch.HermesAgent.Abstractions;

namespace ES.FX.NousResearch.HermesAgent.Tests;

public class HermesAgentKnownValuesTests
{
    [Fact]
    public void Run_Statuses_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("queued", HermesAgentRunStatuses.Queued);
        Assert.Equal("running", HermesAgentRunStatuses.Running);
        Assert.Equal("waiting_for_approval", HermesAgentRunStatuses.WaitingForApproval);
        Assert.Equal("stopping", HermesAgentRunStatuses.Stopping);
        Assert.Equal("completed", HermesAgentRunStatuses.Completed);
        Assert.Equal("failed", HermesAgentRunStatuses.Failed);
        Assert.Equal("cancelled", HermesAgentRunStatuses.Cancelled);
    }

    [Fact]
    public void Response_Statuses_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("in_progress", HermesAgentResponseStatuses.InProgress);
        Assert.Equal("completed", HermesAgentResponseStatuses.Completed);
        Assert.Equal("failed", HermesAgentResponseStatuses.Failed);
        Assert.Equal("incomplete", HermesAgentResponseStatuses.Incomplete);
    }

    [Fact]
    public void Job_States_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("scheduled", HermesAgentJobStates.Scheduled);
        Assert.Equal("paused", HermesAgentJobStates.Paused);
        Assert.Equal("completed", HermesAgentJobStates.Completed);
        Assert.Equal("error", HermesAgentJobStates.Error);
    }

    [Fact]
    public void Schedule_Kinds_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("once", HermesAgentScheduleKinds.Once);
        Assert.Equal("interval", HermesAgentScheduleKinds.Interval);
        Assert.Equal("cron", HermesAgentScheduleKinds.Cron);
    }

    [Fact]
    public void Job_Last_Run_Statuses_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("ok", HermesAgentJobLastRunStatuses.Ok);
        Assert.Equal("error", HermesAgentJobLastRunStatuses.Error);
    }

    [Fact]
    public void Deliver_Modes_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("local", HermesAgentDeliverModes.Local);
    }

    [Fact]
    public void Chat_Finish_Reasons_Pin_The_Server_Vocabulary()
    {
        Assert.Equal("stop", HermesAgentChatFinishReasons.Stop);
        Assert.Equal("length", HermesAgentChatFinishReasons.Length);
        Assert.Equal("error", HermesAgentChatFinishReasons.Error);
    }
}