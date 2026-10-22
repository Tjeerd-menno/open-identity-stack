// Copied from src/OpenIdentityStack.Infrastructure/Identity/BackChannelLogoutNotifier.cs
// at 0943f58a7e59814996feee5a73ac224ef8ef9a7e (526cae99613eca0c9dee0ca2f2f3045b5327c49f^, PR #398).
    private static string CreateLogoutToken(SessionId sessionId, string clientId)
    {
        // Create a simple unsigned logout token for back-channel notification
        // In production, this should be signed with the server's key
        // and use proper JWT library from OpenIddict
        var payload = new
        {
            iss = "open-identity-stack", // TODO: Get from configuration
            sub = sessionId.Value.ToString(),
            aud = clientId,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            jti = Guid.NewGuid().ToString(),
            sid = sessionId.Value.ToString(),
            events = new Dictionary<string, object>
            {
                ["http://schemas.openid.net/event/backchannel-logout"] = new { }
            }
        };

        string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string payloadJson = JsonSerializer.Serialize(payload);
        string payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Unsigned JWT (alg: none) - for demo only
        // Production should use OpenIddict's token generation
        return $"{header}.{payloadBase64}.";
    }
