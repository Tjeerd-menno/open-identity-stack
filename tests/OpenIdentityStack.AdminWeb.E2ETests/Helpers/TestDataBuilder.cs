namespace OpenIdentityStack.AdminWeb.E2ETests.Helpers;

/// <summary>
/// Builder pattern for creating test data.
/// Provides fluent API for generating test users, roles, groups, etc.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Create a builder for test user data.
    /// </summary>
    public static UserBuilder User() => new UserBuilder();
    
    /// <summary>
    /// Create a builder for test role data.
    /// </summary>
    public static RoleBuilder Role() => new RoleBuilder();
    
    /// <summary>
    /// Create a builder for test group data.
    /// </summary>
    public static GroupBuilder Group() => new GroupBuilder();
    
    /// <summary>
    /// Create a builder for test service account data.
    /// </summary>
    public static ServiceAccountBuilder ServiceAccount() => new ServiceAccountBuilder();
}

/// <summary>
/// Builder for test user data.
/// </summary>
public class UserBuilder
{
    public string Email { get; private set; } = TestHelpers.GenerateRandomEmail();
    public string DisplayName { get; private set; } = TestHelpers.GenerateRandomName();
    public string Password { get; private set; } = "TestPassword123!";
    
    public UserBuilder WithEmail(string email)
    {
        Email = email;
        return this;
    }
    
    public UserBuilder WithDisplayName(string displayName)
    {
        DisplayName = displayName;
        return this;
    }
    
    public UserBuilder WithPassword(string password)
    {
        Password = password;
        return this;
    }
    
    /// <summary>
    /// Build the user data as a dictionary for form filling.
    /// </summary>
    public Dictionary<string, string> Build()
    {
        return new Dictionary<string, string>
        {
            { "Email", Email },
            { "Display Name", DisplayName },
            { "Password", Password }
        };
    }
}

/// <summary>
/// Builder for test role data.
/// </summary>
public class RoleBuilder
{
    public string Name { get; private set; } = $"test-role-{Guid.NewGuid():N}"[..42];
    public string DisplayName { get; private set; } = $"Test Role {DateTime.UtcNow.Ticks}";
    public string Description { get; private set; } = "Test role description";
    public List<string> Permissions { get; private set; } = new();
    
    public RoleBuilder WithName(string name)
    {
        Name = name;
        return this;
    }
    
    public RoleBuilder WithDisplayName(string displayName)
    {
        DisplayName = displayName;
        return this;
    }
    
    public RoleBuilder WithDescription(string description)
    {
        Description = description;
        return this;
    }
    
    public RoleBuilder WithPermissions(params string[] permissions)
    {
        Permissions.AddRange(permissions);
        return this;
    }
    
    /// <summary>
    /// Build the role data as a dictionary for form filling.
    /// Note: Name, Display Name, and Description (Optional) fields match the RoleForm UI labels.
    /// Permissions are handled separately via checkbox selection.
    /// </summary>
    public Dictionary<string, string> Build()
    {
        return new Dictionary<string, string>
        {
            { "Name", Name },
            { "Display Name", DisplayName },
            { "Description (Optional)", Description }
        };
    }
}

/// <summary>
/// Builder for test group data.
/// </summary>
public class GroupBuilder
{
    public string Name { get; private set; } = $"Test Group {Guid.NewGuid():N}";
    public string Description { get; private set; } = "Test group description";
    
    public GroupBuilder WithName(string name)
    {
        Name = name;
        return this;
    }
    
    public GroupBuilder WithDescription(string description)
    {
        Description = description;
        return this;
    }
    
    /// <summary>
    /// Build the group data as a dictionary for form filling.
    /// Note: 'Group Name' matches the GroupForm UI label.
    /// </summary>
    public Dictionary<string, string> Build()
    {
        return new Dictionary<string, string>
        {
            { "Group Name", Name },
            { "Description", Description }
        };
    }
}

/// <summary>
/// Builder for test service account data.
/// </summary>
public class ServiceAccountBuilder
{
    public string DisplayName { get; private set; } = $"Test SA {DateTime.UtcNow.Ticks}";
    public string ClientId { get; private set; } = $"test-sa-{Guid.NewGuid():N}";
    
    public ServiceAccountBuilder WithDisplayName(string displayName)
    {
        DisplayName = displayName;
        return this;
    }
    
    public ServiceAccountBuilder WithClientId(string clientId)
    {
        ClientId = clientId;
        return this;
    }
    
    /// <summary>
    /// Build the service account data as a dictionary for form filling.
    /// Note: 'Client ID' and 'Display Name' match the ServiceAccountForm UI labels.
    /// </summary>
    public Dictionary<string, string> Build()
    {
        return new Dictionary<string, string>
        {
            { "Client ID", ClientId },
            { "Display Name", DisplayName }
        };
    }
}
