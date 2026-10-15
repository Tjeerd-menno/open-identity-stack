// Copied from src/OpenIdentityStack.Api/Federation/ProvidersApi.cs:172
// at 9120ea8bee3804b6c06850840323d75fae4b5569 (a950bd3^, PR #434).
namespace History;

public static class ProvidersApiHistory
{
    public static bool IsNotFound(string code) => code.Contains("NotFound");
}
