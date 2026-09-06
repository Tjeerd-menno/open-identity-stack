namespace OpenIdentityStack.Application.Resources;

public sealed class ResourceAccessConflictException(Exception innerException) : Exception("Resource access changed; reload before saving.", innerException);
