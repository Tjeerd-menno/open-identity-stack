
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher hasher = new();

    [Fact]
    public void HashPassword_ShouldThrow_WhenPasswordNullOrEmpty()
    {
        Should.Throw<ArgumentNullException>(() => this.hasher.HashPassword(null!));
        Should.Throw<ArgumentNullException>(() => this.hasher.HashPassword(string.Empty));
    }

    [Fact]
    public void HashPassword_ShouldReturnHash_WhenPasswordProvided()
    {
        string hash = this.hasher.HashPassword("P@ssw0rd!");

        hash.ShouldNotBeNullOrWhiteSpace();
        hash.ShouldNotBe("P@ssw0rd!");
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenInputsEmpty()
    {
        this.hasher.VerifyPassword(string.Empty, "pass").ShouldBeFalse();
        this.hasher.VerifyPassword("hash", string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_WhenPasswordMatches()
    {
        string password = "P@ssw0rd!";
        string hash = this.hasher.HashPassword(password);

        this.hasher.VerifyPassword(hash, password).ShouldBeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordDoesNotMatch()
    {
        string hash = this.hasher.HashPassword("P@ssw0rd!");

        this.hasher.VerifyPassword(hash, "wrong").ShouldBeFalse();
    }
}
