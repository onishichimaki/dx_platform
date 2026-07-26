using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "ApplicationPermission is the required ubiquitous-language entity name.",
    Scope = "type",
    Target = "~T:DxPlatform.Domain.Applications.ApplicationPermission")]

