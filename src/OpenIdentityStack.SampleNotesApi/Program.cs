using System.Collections.Concurrent;
using OpenIdentityStack.SampleNotesApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

ConcurrentDictionary<Guid, Note> notes = new();

app.MapGet("/notes", () => Results.Ok(notes.Values.OrderBy(note => note.CreatedAt)));

app.MapGet("/notes/{id:guid}", (Guid id) =>
{
    return notes.TryGetValue(id, out Note? note)
        ? Results.Ok(note)
        : Results.NotFound();
});

app.MapPost("/notes", (CreateNoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Note.TitleRequired", message = "Title is required." });
    }

    DateTimeOffset now = DateTimeOffset.UtcNow;
    Note note = new(Guid.NewGuid(), request.Title.Trim(), request.Content?.Trim(), now, now);
    notes[note.Id] = note;
    return Results.Created($"/notes/{note.Id}", note);
});

app.MapPut("/notes/{id:guid}", (Guid id, UpdateNoteRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title))
    {
        return Results.BadRequest(new { error = "Note.TitleRequired", message = "Title is required." });
    }

    if (!notes.TryGetValue(id, out Note? existing))
    {
        return Results.NotFound();
    }

    Note updated = existing with
    {
        Title = request.Title.Trim(),
        Content = request.Content?.Trim(),
        UpdatedAt = DateTimeOffset.UtcNow
    };

    notes[id] = updated;
    return Results.Ok(updated);
});

app.MapDelete("/notes/{id:guid}", (Guid id) =>
{
    return notes.TryRemove(id, out _)
        ? Results.NoContent()
        : Results.NotFound();
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

    return Results.Ok(manifest);
})
.AllowAnonymous();

app.Run();
