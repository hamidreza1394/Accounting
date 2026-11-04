namespace Domain;

/// <summary>The reason an HR candidate change is rejected by the pure domain rules.</summary>
public enum HrRuleViolation
{
    None = 0,
    InvalidPeriod = 1,
    InvalidStatus = 2,
    TerminatedWithoutDate = 3,
    TerminationDateWithoutStatus = 4,
    DuplicateEmployeeNumber = 5,
    CrossCompanyLink = 6,
    AssignmentOverlap = 7,
    AssignmentPeriodInvalid = 8,
    UnitCycle = 9,
    UnitCycleTooDeep = 10,
    ManagerCycle = 11,
    ManagerCycleTooDeep = 12,
    SelfManagement = 13,
    AssignmentStartInFuture = 14,
    AssignmentStartBeforeHire = 15,
}

/// <summary>A unit, as the pure rules see it: either a stored one or a candidate.</summary>
public sealed record HrUnitFact(Guid UnitId, Guid? ParentUnitId, bool IsActive);

/// <summary>
/// The pure, dependency-free rules of the company-scoped organizational structure (S2,
/// D-072). Every rule is fail-closed: an unclear input is a violation, never a pass.
/// The application service loads the facts inside its transaction and calls these rules,
/// so the decisions are testable without a database and race-checked with rowversions.
/// </summary>
public static class HrRules
{
    /// <summary>The maximum chain length walked for cycle detection, bounding the traversal.</summary>
    public const int MaxChainDepth = 32;

    /// <summary>Validates the employment fields of a candidate employee state.</summary>
    public static HrRuleViolation ValidateEmployment(
        CompanyEmployeeEmploymentStatus status,
        DateTime? hiredOnUtc,
        DateTime? terminatedOnUtc)
    {
        if (!Enum.IsDefined(status))
        {
            return HrRuleViolation.InvalidStatus;
        }

        if (status == CompanyEmployeeEmploymentStatus.Terminated && terminatedOnUtc is null)
        {
            return HrRuleViolation.TerminatedWithoutDate;
        }

        if (status != CompanyEmployeeEmploymentStatus.Terminated && terminatedOnUtc is not null)
        {
            return HrRuleViolation.TerminationDateWithoutStatus;
        }

        if (hiredOnUtc is not null && terminatedOnUtc is not null && terminatedOnUtc < hiredOnUtc)
        {
            return HrRuleViolation.InvalidPeriod;
        }

        return HrRuleViolation.None;
    }

    /// <summary>Validates the effective window of an assignment.</summary>
    public static HrRuleViolation ValidateAssignmentWindow(DateTime startOnUtc, DateTime? endOnUtc) =>
        endOnUtc is not null && endOnUtc < startOnUtc
            ? HrRuleViolation.AssignmentPeriodInvalid
            : HrRuleViolation.None;

    /// <summary>
    /// Controls the effective start of a new assignment (phase 4 workflow): the model keeps
    /// at most one active assignment per employee, so a future-dated start cannot be staged
    /// as active and a start before the recorded hire date is inconsistent with the
    /// employment history. Both fail closed.
    /// </summary>
    public static HrRuleViolation ValidateAssignmentStart(
        DateTime startOnUtc, DateTime nowUtc, DateTime? hiredOnUtc)
    {
        if (startOnUtc > nowUtc)
        {
            return HrRuleViolation.AssignmentStartInFuture;
        }

        if (hiredOnUtc is not null && startOnUtc < hiredOnUtc.Value)
        {
            return HrRuleViolation.AssignmentStartBeforeHire;
        }

        return HrRuleViolation.None;
    }

    /// <summary>
    /// Whether starting a new active assignment for the employee is free of overlap: an
    /// overlapping row is one that is active at the moment of the check (status Active and
    /// not ended), regardless of its identifiers, so a race between two concurrent
    /// assignments is caught by the pre-check plus the filtered unique index.
    /// </summary>
    public static bool HasActiveAssignmentOverlap(
        IEnumerable<HrAssignmentFact> otherAssignments) =>
        otherAssignments.Any(assignment => assignment.IsActive);

    /// <summary>
    /// Validates the parent chain of a unit candidate. The facts are the units of the
    /// company (the parent map); the walk starts at the candidate parent and follows the
    /// parent links until the root, bounded by <see cref="MaxChainDepth"/>.
    /// </summary>
    public static HrRuleViolation ValidateUnitParentChain(
        Guid candidateUnitId,
        Guid? candidateParentId,
        IReadOnlyCollection<HrUnitFact> facts)
    {
        if (candidateUnitId == Guid.Empty)
        {
            return HrRuleViolation.UnitCycle;
        }

        if (candidateParentId is null)
        {
            return HrRuleViolation.None;
        }

        if (candidateParentId == candidateUnitId)
        {
            return HrRuleViolation.UnitCycle;
        }

        // Walk from the candidate parent towards the root: reaching the candidate itself
        // closes a cycle into its own subtree, a revisit closes an existing data cycle and
        // the depth bound keeps the walk finite on unexpectedly deep or broken data.
        var visited = new HashSet<Guid>();
        Guid? current = candidateParentId;
        while (current is not null)
        {
            if (current == candidateUnitId)
            {
                return HrRuleViolation.UnitCycle;
            }

            if (!visited.Add(current.Value))
            {
                return HrRuleViolation.UnitCycle;
            }

            if (visited.Count > MaxChainDepth)
            {
                return HrRuleViolation.UnitCycleTooDeep;
            }

            var next = facts.FirstOrDefault(unit => unit.UnitId == current.Value)?.ParentUnitId;
            if (next is not null && !facts.Any(unit => unit.UnitId == next.Value))
            {
                // The fact list is incomplete: fail closed instead of guessing.
                return HrRuleViolation.UnitCycleTooDeep;
            }

            current = next;
        }

        return HrRuleViolation.None;
    }

    /// <summary>
    /// Validates the manager chain of a candidate assignment. The chain is the manager
    /// path of the candidate manager (via each manager's active assignment), ordered from
    /// the manager itself upwards; the walk is bounded by <see cref="MaxChainDepth"/>.
    /// </summary>
    public static HrRuleViolation ValidateManagerChain(
        Guid candidateEmployeeId,
        Guid? candidateManagerId,
        IReadOnlyCollection<Guid> managerChain)
    {
        if (candidateEmployeeId == Guid.Empty)
        {
            return HrRuleViolation.ManagerCycle;
        }

        if (candidateManagerId is null)
        {
            return HrRuleViolation.None;
        }

        if (candidateManagerId == candidateEmployeeId)
        {
            return HrRuleViolation.SelfManagement;
        }

        if (managerChain.Count > MaxChainDepth)
        {
            return HrRuleViolation.ManagerCycleTooDeep;
        }

        var visited = new HashSet<Guid>();
        foreach (var managerId in managerChain)
        {
            if (managerId == Guid.Empty)
            {
                return HrRuleViolation.ManagerCycle;
            }

            if (managerId == candidateEmployeeId)
            {
                return HrRuleViolation.ManagerCycle;
            }

            if (!visited.Add(managerId))
            {
                return HrRuleViolation.ManagerCycle;
            }
        }

        return HrRuleViolation.None;
    }
}

/// <summary>An assignment, as the pure rules see it.</summary>
public sealed record HrAssignmentFact(
    Guid AssignmentId,
    Guid EmployeeId,
    bool IsActive,
    DateTime StartOnUtc,
    DateTime? EndOnUtc);
