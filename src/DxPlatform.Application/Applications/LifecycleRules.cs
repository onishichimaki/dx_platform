using DxPlatform.Domain.Enums;

namespace DxPlatform.Application.Applications;

public static class LifecycleRules
{
    private static readonly Dictionary<LifecycleStatus, LifecycleStatus[]> AllowedTransitions =
        new Dictionary<LifecycleStatus, LifecycleStatus[]>
        {
            [LifecycleStatus.Draft] =
                [LifecycleStatus.Testing, LifecycleStatus.Suspended, LifecycleStatus.Retired],
            [LifecycleStatus.Testing] =
                [LifecycleStatus.Draft, LifecycleStatus.Production, LifecycleStatus.Suspended, LifecycleStatus.Retired],
            [LifecycleStatus.Production] =
                [LifecycleStatus.Suspended, LifecycleStatus.Retired],
            [LifecycleStatus.Suspended] =
                [LifecycleStatus.Testing, LifecycleStatus.Production, LifecycleStatus.Retired],
            [LifecycleStatus.Retired] = []
        };

    public static bool CanTransition(LifecycleStatus from, LifecycleStatus to) =>
        from == to || AllowedTransitions[from].Contains(to);
}
