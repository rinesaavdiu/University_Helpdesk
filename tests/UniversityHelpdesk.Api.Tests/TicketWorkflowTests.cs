using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tests;

public class TicketWorkflowTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    public void NextStatus_AdvancesOneStep(TicketStatus current, TicketStatus expected)
    {
        Assert.Equal(expected, TicketWorkflow.NextStatus(current));
    }

    [Fact]
    public void NextStatus_OfResolved_IsNone()
    {
        Assert.Null(TicketWorkflow.NextStatus(TicketStatus.Resolved));
    }

    [Theory]
    [InlineData(TicketStatus.InProgress, TicketStatus.Open)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    public void PreviousStatus_IsTheOnlyAllowedSource(TicketStatus target, TicketStatus expected)
    {
        Assert.Equal(expected, TicketWorkflow.PreviousStatus(target));
    }

    [Fact]
    public void PreviousStatus_OfOpen_IsNone()
    {
        Assert.Null(TicketWorkflow.PreviousStatus(TicketStatus.Open));
    }
}
