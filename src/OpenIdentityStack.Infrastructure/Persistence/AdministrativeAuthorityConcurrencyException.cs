using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence;

internal sealed class AdministrativeAuthorityConcurrencyException(string message)
    : DbUpdateConcurrencyException(message), IConcurrencyConflict;
