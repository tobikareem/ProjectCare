namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>Platform-level host configuration the Application layer needs when registering organizations.</summary>
public interface IPlatformDomainSettings
{
    /// <summary>Base domain under which <c>{slug}.</c> subdomains are generated, e.g. <c>carepathhealth.com</c>.</summary>
    string PlatformBaseDomain { get; }

    /// <summary>Normalized hosts that serve platform (operator) traffic; never assignable to an organization.</summary>
    IReadOnlySet<string> PlatformHostNames { get; }

    /// <summary>True only for synthetic environments; production keeps one Active organization until CP-06.</summary>
    bool AllowMultipleActiveOrganizations { get; }
}
