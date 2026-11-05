using Domain;
using Xunit;

namespace Template.Tests;

/// <summary>
/// The pure rules of the company-scoped organizational structure (D-072): employment
/// state machine, effective windows, unit parent cycles and manager chains. Every rule
/// must fail closed on unclear input.
/// </summary>
public sealed class HrRulesTests
{
    // ------------------------------------------------------------------ employment

    [Fact]
    public void Employment_ActiveWithoutDates_IsValid()
    {
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Active, null, null));
    }

    [Fact]
    public void Employment_TerminatedWithoutDate_Fails()
    {
        Assert.Equal(HrRuleViolation.TerminatedWithoutDate, HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Terminated, null, null));
    }

    [Fact]
    public void Employment_NonTerminatedWithTerminationDate_Fails()
    {
        Assert.Equal(HrRuleViolation.TerminationDateWithoutStatus, HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Active, null, DateTime.UtcNow));
    }

    [Fact]
    public void Employment_UndefinedStatus_Fails()
    {
        Assert.Equal(HrRuleViolation.InvalidStatus, HrRules.ValidateEmployment(
            (Domain.CompanyEmployeeEmploymentStatus)99, null, null));
    }

    [Fact]
    public void Employment_TerminationBeforeHire_Fails()
    {
        var hired = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var terminated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.InvalidPeriod, HrRules.ValidateEmployment(
            Domain.CompanyEmployeeEmploymentStatus.Terminated, hired, terminated));
    }

    // ------------------------------------------------------------------ assignment window

    [Fact]
    public void AssignmentWindow_EndBeforeStart_Fails()
    {
        Assert.Equal(HrRuleViolation.AssignmentPeriodInvalid, HrRules.ValidateAssignmentWindow(
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void AssignmentWindow_OpenEnded_IsValid()
    {
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateAssignmentWindow(
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), null));
    }

    // ------------------------------------------------- assignment start (phase 4)

    [Fact]
    public void AssignmentStart_InTheFuture_Fails()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.AssignmentStartInFuture, HrRules.ValidateAssignmentStart(
            now.AddTicks(1), now, null));
    }

    [Fact]
    public void AssignmentStart_ExactlyNow_IsValid()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateAssignmentStart(now, now, null));
    }

    [Fact]
    public void AssignmentStart_BeforeTheHireDate_Fails()
    {
        var hired = new DateTime(2026, 1, 10, 8, 30, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.AssignmentStartBeforeHire, HrRules.ValidateAssignmentStart(
            hired.AddTicks(-1), hired.AddDays(1), hired));
    }

    [Fact]
    public void AssignmentStart_ExactlyOnTheHireDate_IsValid()
    {
        var hired = new DateTime(2026, 1, 10, 8, 30, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateAssignmentStart(
            hired, hired.AddDays(1), hired));
    }

    [Fact]
    public void AssignmentStart_WithoutHireDate_PastStart_IsValid()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateAssignmentStart(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), now, null));
    }

    [Fact]
    public void AssignmentStart_MidnightBoundaries_AreComparedInUtc()
    {
        // The midnight of the day after "now" is still future-dated; the midnight of the
        // hire day itself is a valid start (the rule compares instants, not calendar days).
        var now = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var hired = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(HrRuleViolation.AssignmentStartInFuture, HrRules.ValidateAssignmentStart(
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), now, hired));
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateAssignmentStart(hired, now, hired));
    }

    // ------------------------------------------------------------------ unit cycles

    [Fact]
    public void UnitChain_SelfParent_Fails()
    {
        var unitId = Guid.NewGuid();
        Assert.Equal(HrRuleViolation.UnitCycle, HrRules.ValidateUnitParentChain(
            unitId, unitId, []));
    }

    [Fact]
    public void UnitChain_RootUnit_IsValid()
    {
        var unitId = Guid.NewGuid();
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateUnitParentChain(
            unitId, null, []));
    }

    [Fact]
    public void UnitChain_MoveUnderOwnDescendant_Fails()
    {
        // Tree: root -> middle -> leaf. Moving root under leaf would close a cycle.
        var root = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var leaf = Guid.NewGuid();
        var facts = new List<HrUnitFact>
        {
            new(root, null, true),
            new(middle, root, true),
            new(leaf, middle, true),
        };

        Assert.Equal(HrRuleViolation.UnitCycle, HrRules.ValidateUnitParentChain(
            root, leaf, facts));
    }

    [Fact]
    public void UnitChain_MoveUnderUnrelatedUnit_IsValid()
    {
        var root = Guid.NewGuid();
        var other = Guid.NewGuid();
        var facts = new List<HrUnitFact>
        {
            new(root, null, true),
            new(other, null, true),
        };

        Assert.Equal(HrRuleViolation.None, HrRules.ValidateUnitParentChain(
            root, other, facts));
    }

    [Fact]
    public void UnitChain_ExistingCycleInData_FailsClosed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var facts = new List<HrUnitFact>
        {
            new(a, b, true),
            new(b, a, true),
        };

        Assert.Equal(HrRuleViolation.UnitCycle, HrRules.ValidateUnitParentChain(
            Guid.NewGuid(), a, facts));
    }

    [Fact]
    public void UnitChain_DeepChain_BeyondLimit_FailsClosed()
    {
        var candidate = Guid.NewGuid();
        var facts = new List<HrUnitFact>();
        var previous = Guid.Empty;
        for (var index = 0; index <= HrRules.MaxChainDepth + 1; index++)
        {
            var unitId = Guid.NewGuid();
            facts.Add(new HrUnitFact(unitId, previous == Guid.Empty ? null : previous, true));
            previous = unitId;
        }

        // The candidate's parent is the DEEPEST unit, so the walk has to climb the whole
        // chain before it can reach the root.
        Assert.Equal(HrRuleViolation.UnitCycleTooDeep, HrRules.ValidateUnitParentChain(
            candidate, facts[^1].UnitId, facts));
    }

    // ------------------------------------------------------------------ manager chains

    [Fact]
    public void ManagerChain_SelfManagement_Fails()
    {
        var employeeId = Guid.NewGuid();
        Assert.Equal(HrRuleViolation.SelfManagement, HrRules.ValidateManagerChain(
            employeeId, employeeId, []));
    }

    [Fact]
    public void ManagerChain_NoManager_IsValid()
    {
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateManagerChain(
            Guid.NewGuid(), null, []));
    }

    [Fact]
    public void ManagerChain_CycleThroughManagers_Fails()
    {
        // A is managed by B, B is managed by C, C is managed by A. Making A the manager
        // of the candidate closes the cycle through C -> A.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var chain = new List<Guid> { a, c, b, a };

        Assert.Equal(HrRuleViolation.ManagerCycle, HrRules.ValidateManagerChain(
            candidate, a, chain));
    }

    [Fact]
    public void ManagerChain_WhereCandidateIsInTheChain_Fails()
    {
        var candidate = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var chain = new List<Guid> { manager, candidate };

        Assert.Equal(HrRuleViolation.ManagerCycle, HrRules.ValidateManagerChain(
            candidate, manager, chain));
    }

    [Fact]
    public void ManagerChain_SimpleLine_IsValid()
    {
        var candidate = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var grandManager = Guid.NewGuid();
        Assert.Equal(HrRuleViolation.None, HrRules.ValidateManagerChain(
            candidate, manager, [manager, grandManager]));
    }

    [Fact]
    public void ManagerChain_TooDeep_FailsClosed()
    {
        var candidate = Guid.NewGuid();
        var chain = new List<Guid>();
        for (var index = 0; index <= HrRules.MaxChainDepth + 1; index++)
            chain.Add(Guid.NewGuid());

        Assert.Equal(HrRuleViolation.ManagerCycleTooDeep, HrRules.ValidateManagerChain(
            candidate, chain[0], chain));
    }

    // ------------------------------------------------------------------ overlap

    [Fact]
    public void Overlap_ActiveAssignmentBlocksTheStart()
    {
        var facts = new List<HrAssignmentFact>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), true, DateTime.UtcNow.AddDays(-30), null),
        };

        Assert.True(HrRules.HasActiveAssignmentOverlap(facts));
    }

    [Fact]
    public void Overlap_EndedAssignmentDoesNotBlockTheStart()
    {
        var facts = new List<HrAssignmentFact>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), false, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-1)),
        };

        Assert.False(HrRules.HasActiveAssignmentOverlap(facts));
    }

    // ------------------------------------------------- deactivation guards (D-072)

    [Fact]
    public void UnitDeactivation_ActiveChildBlocksTheTransition()
    {
        Assert.Equal(CompanyUnitDeactivationBlock.HasActiveChildUnit,
            HrRules.ValidateUnitDeactivation(hasActiveChildUnit: true, hasActiveAssignment: false));
    }

    [Fact]
    public void UnitDeactivation_ActiveAssignmentBlocksTheTransition()
    {
        Assert.Equal(CompanyUnitDeactivationBlock.HasActiveAssignment,
            HrRules.ValidateUnitDeactivation(hasActiveChildUnit: false, hasActiveAssignment: true));
    }

    [Fact]
    public void UnitDeactivation_ClearUnit_IsNotBlocked()
    {
        Assert.Equal(CompanyUnitDeactivationBlock.None,
            HrRules.ValidateUnitDeactivation(hasActiveChildUnit: false, hasActiveAssignment: false));
    }

    [Fact]
    public void UnitDeactivation_ActiveChildTakesPrecedenceOverTheAssignment()
    {
        Assert.Equal(CompanyUnitDeactivationBlock.HasActiveChildUnit,
            HrRules.ValidateUnitDeactivation(hasActiveChildUnit: true, hasActiveAssignment: true));
    }

    [Fact]
    public void PositionDeactivation_ActiveAssignmentBlocksTheTransition()
    {
        Assert.Equal(HrRuleViolation.PositionDeactivationBlocked,
            HrRules.ValidatePositionDeactivation(hasActiveAssignment: true));
    }

    [Fact]
    public void PositionDeactivation_WithoutActiveAssignment_IsValid()
    {
        Assert.Equal(HrRuleViolation.None,
            HrRules.ValidatePositionDeactivation(hasActiveAssignment: false));
    }
}
