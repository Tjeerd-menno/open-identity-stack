namespace OpenIdentityStack.SampleNotesApi;

public sealed record Note(Guid Id, string Title, string? Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateNoteRequest(string Title, string? Content);

public sealed record UpdateNoteRequest(string Title, string? Content);

public sealed record PermissionManifestResponse(
    PermissionManifestApplication Application,
    IReadOnlyList<PermissionManifestEntry> Permissions);

public sealed record PermissionManifestApplication(string Id, string Name, string Version);

public sealed record PermissionManifestEntry(string Name, string Description, string? Category);
