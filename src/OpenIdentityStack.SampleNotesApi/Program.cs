using System.Collections.Concurrent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

ConcurrentDictionary<Guid, Note> notes = new();

app.MapGet("/notes", () => TypedResults.Ok(notes.Values.OrderBy(note => note.CreatedAt)));

app.MapGet("/notes/{id:guid}", (Guid id) =>
{
    return notes.TryGetValue(id, out Note? note)
        ? TypedResults.Ok(note)
        : TypedResults.NotFound();
});

app.MapPost("/notes", (CreateNoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return TypedResults.BadRequest(new { error = "Note.TitleRequired", message = "Title is required." });
    }

    DateTimeOffset now = DateTimeOffset.UtcNow;
    Note note = new(Guid.NewGuid(), request.Title.Trim(), request.Content?.Trim(), now, now);
    notes[note.Id] = note;
    return TypedResults.Created($"/notes/{note.Id}", note);
});

app.MapPut("/notes/{id:guid}", (Guid id, UpdateNoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return TypedResults.BadRequest(new { error = "Note.TitleRequired", message = "Title is required." });
    }

    if (!notes.TryGetValue(id, out Note? existing))
    {
        return TypedResults.NotFound();
    }

    Note updated = existing with
    {
        Title = request.Title.Trim(),
        Content = request.Content?.Trim(),
        UpdatedAt = DateTimeOffset.UtcNow
    };

    notes[id] = updated;
    return TypedResults.Ok(updated);
});

app.MapDelete("/notes/{id:guid}", (Guid id) =>
{
    return notes.TryRemove(id, out _)
        ? TypedResults.NoContent()
        : TypedResults.NotFound();
});

app.MapGet("/.well-known/permissions", () =>
{
    PermissionManifestResponse manifest = new(
        new PermissionManifestApplication(
            "openidentitystack.sample-notes-api",
            "OpenIdentityStack Sample Notes API",
            "1.0.0"),
        [
            new PermissionManifestEntry("notes.read", "Read notes", "notes"),
            new PermissionManifestEntry("notes.write", "Create, update, and delete notes", "notes")
        ]);

    return TypedResults.Ok(manifest);
})
.AllowAnonymous();

app.Run();

public sealed record Note(Guid Id, string Title, string? Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateNoteRequest(string Title, string? Content);

public sealed record UpdateNoteRequest(string Title, string? Content);

public sealed record PermissionManifestResponse(
    PermissionManifestApplication Application,
    IReadOnlyList<PermissionManifestEntry> Permissions);

public sealed record PermissionManifestApplication(string Id, string Name, string Version);

public sealed record PermissionManifestEntry(string Name, string Description, string? Category);
