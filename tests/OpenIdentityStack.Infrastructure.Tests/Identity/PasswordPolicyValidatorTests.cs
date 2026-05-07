using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Identity;

/// <summary>
/// Tests for PasswordPolicyValidator to ensure all password requirements are enforced.
/// </summary>
public sealed class PasswordPolicyValidatorTests
{
    private readonly PasswordPolicyValidator _validator = new();

    #region Success Cases

    [Theory]
    [InlineData("TestPassword123!")]
    [InlineData("MyP@ssw0rd123")]
    [InlineData("C0mpl3x!Pass")]
    [InlineData("Abcd1234!@#$")]
    [InlineData("LongP@ssw0rd123456789")]
    public void ValidatePassword_WithValidPassword_ReturnsSuccess(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Null/Empty/Whitespace Cases

    [Fact]
    public void ValidatePassword_WithNullPassword_ReturnsError()
    {
        // Act
        Result result = this._validator.ValidatePassword(null!);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
        result.Error.Description.ShouldBe("Password is required.");
    }

    [Fact]
    public void ValidatePassword_WithEmptyPassword_ReturnsError()
    {
        // Act
        Result result = this._validator.ValidatePassword(string.Empty);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
        result.Error.Description.ShouldBe("Password is required.");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ValidatePassword_WithWhitespacePassword_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.Required");
        result.Error.Description.ShouldBe("Password is required.");
    }

    #endregion

    #region Length Validation

    [Theory]
    [InlineData("Short1!")]
    [InlineData("Pass1!")]
    [InlineData("Abc123!@")]
    [InlineData("Test1234!")]
    [InlineData("OnlyEleven1")]
    public void ValidatePassword_WithTooShortPassword_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.TooShort");
        result.Error.Description.ShouldBe("Password must be at least 12 characters long.");
    }

    [Theory]
    [InlineData("ExactlyTwelv")]
    [InlineData("ThirteenChars")]
    [InlineData("FourteenCharss")]
    public void ValidatePassword_WithMinimumLength_FailsWithoutAllRequirements(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert - These will fail other requirements (digit, special char, etc.)
        result.IsFailure.ShouldBeTrue();
    }

    #endregion

    #region Uppercase Validation

    [Theory]
    [InlineData("testpassword123!")]
    [InlineData("lowercase1234!@#")]
    [InlineData("nouppercasehere123!")]
    public void ValidatePassword_WithNoUppercase_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.NoUppercase");
        result.Error.Description.ShouldBe("Password must contain at least one uppercase letter.");
    }

    [Theory]
    [InlineData("TESTPASSWORD123!")]
    [InlineData("ALLUPPERCASE1234!@#")]
    public void ValidatePassword_WithOnlyUppercase_FailsLowercaseRequirement(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.NoLowercase");
    }

    #endregion

    #region Lowercase Validation

    [Theory]
    [InlineData("TESTPASSWORD123!")]
    [InlineData("UPPERCASE1234!@#")]
    [InlineData("NOLOWERCASEHERE123!")]
    public void ValidatePassword_WithNoLowercase_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.NoLowercase");
        result.Error.Description.ShouldBe("Password must contain at least one lowercase letter.");
    }

    #endregion

    #region Digit Validation

    [Theory]
    [InlineData("TestPassword!")]
    [InlineData("OnlyLetters!@#$%")]
    [InlineData("NoDigitsHere!@#")]
    public void ValidatePassword_WithNoDigit_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.NoDigit");
        result.Error.Description.ShouldBe("Password must contain at least one number.");
    }

    #endregion

    #region Special Character Validation

    [Theory]
    [InlineData("TestPassword123")]
    [InlineData("OnlyAlphanumeric123")]
    [InlineData("NoSpecialChars456")]
    public void ValidatePassword_WithNoSpecialCharacter_ReturnsError(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Password.NoSpecialCharacter");
        result.Error.Description.ShouldBe("Password must contain at least one special character.");
    }

    [Theory]
    [InlineData("ValidPass123!")]
    [InlineData("ValidPass123@")]
    [InlineData("ValidPass123#")]
    [InlineData("ValidPass123$")]
    [InlineData("ValidPass123%")]
    [InlineData("ValidPass123^")]
    [InlineData("ValidPass123&")]
    [InlineData("ValidPass123*")]
    [InlineData("ValidPass123(")]
    [InlineData("ValidPass123)")]
    [InlineData("ValidPass123-")]
    [InlineData("ValidPass123_")]
    [InlineData("ValidPass123=")]
    [InlineData("ValidPass123+")]
    [InlineData("ValidPass123[")]
    [InlineData("ValidPass123{")]
    [InlineData("ValidPass123;")]
    [InlineData("ValidPass123:")]
    [InlineData("ValidPass123'")]
    [InlineData("ValidPass123\"")]
    [InlineData("ValidPass123,")]
    [InlineData("ValidPass123.")]
    [InlineData("ValidPass123/")]
    [InlineData("ValidPass123?")]
    [InlineData("ValidPass123<")]
    [InlineData("ValidPass123>")]
    [InlineData("ValidPass123|")]
    [InlineData("ValidPass123\\")]
    [InlineData("ValidPass123`")]
    [InlineData("ValidPass123~")]
    public void ValidatePassword_WithVariousSpecialCharacters_ReturnsSuccess(string password)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidatePassword_WithExactlyMinimumRequirements_ReturnsSuccess()
    {
        // Exactly 12 chars with all requirements met
        string password = "TestPass123!";

        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePassword_WithUnicodeCharacters_TreatsAsSpecialCharacters()
    {
        // Unicode characters should count as special characters (not letters or digits)
        string password = "TestPass123★";

        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidatePassword_WithSpaceAsSpecialCharacter_ReturnsSuccess()
    {
        // Space is not a letter or digit, so it counts as a special character
        string password = "Test Pass123";

        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    #endregion

    #region Comprehensive Failure Scenarios

    [Theory]
    [InlineData("abc123!@#", "Validation.Password.TooShort")] // Too short, no uppercase
    [InlineData("ABCDEFGH1234", "Validation.Password.NoLowercase")] // No lowercase, no special char
    [InlineData("abcdefgh1234", "Validation.Password.NoUppercase")] // No uppercase, no special char (12 chars, passes length check)
    [InlineData("AbCdEfGh!@#$", "Validation.Password.NoDigit")] // No digit
    [InlineData("12345678901234567890", "Validation.Password.NoUppercase")] // Only digits
    public void ValidatePassword_WithMultipleFailures_ReturnsFirstError(string password, string expectedErrorCode)
    {
        // Act
        Result result = this._validator.ValidatePassword(password);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedErrorCode);
    }

    #endregion
}
