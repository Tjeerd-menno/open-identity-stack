namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Product profile for a registered application.
/// </summary>
public enum ApplicationProfile
{
    MachineToMachine = 0,
    Web = 1,
    SinglePage = 2,
    Native = 3,
    Device = 4,
    Custom = 5
}
