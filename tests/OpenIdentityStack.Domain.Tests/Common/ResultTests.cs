using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Common;

/// <summary>
/// Tests for the Result and Result{T} types.
/// </summary>
public sealed class ResultTests
{
    #region Result<T> Success Tests

    [Fact]
    public void Result_WithValue_IsSuccess()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Result_WithValue_AccessingError_ThrowsInvalidOperationException()
    {
        // Arrange
        Result<int> result = 42;

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Result_WithStringValue_IsSuccess()
    {
        // Arrange & Act
        Result<string> result = "test value";

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("test value");
    }

    #endregion

    #region Result<T> Failure Tests

    [Fact]
    public void Result_WithError_IsFailure()
    {
        // Arrange
        var error = new DomainError("TestError", "Test error description");

        // Act
        Result<int> result = error;

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Result_WithError_AccessingValue_ThrowsInvalidOperationException()
    {
        // Arrange
        Result<int> result = new DomainError("TestError", "Test error");

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _ = result.Value);
    }

    #endregion

    #region Result<T> Match Tests

    [Fact]
    public void Result_Match_WithSuccess_ExecutesOnSuccess()
    {
        // Arrange
        Result<int> result = 42;

        // Act
        string matched = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.ShouldBe("Success: 42");
    }

    [Fact]
    public void Result_Match_WithFailure_ExecutesOnFailure()
    {
        // Arrange
        Result<int> result = new DomainError("Test.Error", "Test error");

        // Act
        string matched = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.ShouldBe("Failure: Test.Error");
    }

    #endregion

    #region Result<T> Map Tests

    [Fact]
    public void Result_Map_WithSuccess_TransformsValue()
    {
        // Arrange
        Result<int> result = 10;

        // Act
        Result<int> mapped = result.Map(value => value * 2);

        // Assert
        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBe(20);
    }

    [Fact]
    public void Result_Map_WithFailure_PropagatesError()
    {
        // Arrange
        var error = new DomainError("Test.Error", "Test error");
        Result<int> result = error;

        // Act
        Result<int> mapped = result.Map(value => value * 2);

        // Assert
        mapped.IsFailure.ShouldBeTrue();
        mapped.Error.ShouldBe(error);
    }

    #endregion

    #region Result<T> Bind Tests

    [Fact]
    public void Result_Bind_WithSuccess_ChainsOperation()
    {
        // Arrange
        Result<int> result = 10;

        // Act
        Result<string> bound = result.Bind(value =>
            value > 5
                ? (Result<string>)$"Value is {value}"
                : new DomainError("TooSmall", "Value is too small"));

        // Assert
        bound.IsSuccess.ShouldBeTrue();
        bound.Value.ShouldBe("Value is 10");
    }

    [Fact]
    public void Result_Bind_WithSuccess_CanReturnFailure()
    {
        // Arrange
        Result<int> result = 3;

        // Act
        Result<string> bound = result.Bind(value =>
            value > 5
                ? (Result<string>)$"Value is {value}"
                : new DomainError("TooSmall", "Value is too small"));

        // Assert
        bound.IsFailure.ShouldBeTrue();
        bound.Error.Code.ShouldBe("TooSmall");
    }

    [Fact]
    public void Result_Bind_WithFailure_PropagatesError()
    {
        // Arrange
        var error = new DomainError("Original.Error", "Original error");
        Result<int> result = error;

        // Act
        Result<string> bound = result.Bind(value => (Result<string>)$"Value is {value}");

        // Assert
        bound.IsFailure.ShouldBeTrue();
        bound.Error.ShouldBe(error);
    }

    #endregion

    #region Result (Non-Generic) Tests

    [Fact]
    public void Result_Success_IsSuccess()
    {
        // Arrange & Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Result_Success_AccessingError_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result.Success();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Result_Failure_IsFailure()
    {
        // Arrange
        var error = new DomainError("TestError", "Test error description");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Result_ImplicitConversionFromError_CreatesFailure()
    {
        // Arrange
        var error = new DomainError("TestError", "Test error");

        // Act
        Result result = error;

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Result_NonGeneric_Match_WithSuccess_ExecutesOnSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        string matched = result.Match(
            onSuccess: () => "Success",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.ShouldBe("Success");
    }

    [Fact]
    public void Result_NonGeneric_Match_WithFailure_ExecutesOnFailure()
    {
        // Arrange
        Result result = new DomainError("Test.Error", "Test error");

        // Act
        string matched = result.Match(
            onSuccess: () => "Success",
            onFailure: error => $"Failure: {error.Code}");

        // Assert
        matched.ShouldBe("Failure: Test.Error");
    }

    #endregion

    #region DomainError Tests

    [Fact]
    public void DomainError_None_HasEmptyValues()
    {
        // Assert
        DomainError.None.Code.ShouldBe(string.Empty);
        DomainError.None.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void DomainError_NullValue_HasCorrectCode()
    {
        // Assert
        DomainError.NullValue.Code.ShouldBe("Error.NullValue");
        DomainError.NullValue.Description.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void DomainError_Validation_PrefixesCode()
    {
        // Act
        var error = DomainError.Validation("InvalidEmail", "Email format is invalid");

        // Assert
        error.Code.ShouldBe("Validation.InvalidEmail");
        error.Description.ShouldBe("Email format is invalid");
    }

    [Fact]
    public void DomainError_NotFound_PrefixesCode()
    {
        // Act
        var error = DomainError.NotFound("User", "User not found");

        // Assert
        error.Code.ShouldBe("NotFound.User");
        error.Description.ShouldBe("User not found");
    }

    [Fact]
    public void DomainError_Conflict_PrefixesCode()
    {
        // Act
        var error = DomainError.Conflict("Duplicate", "Email already exists");

        // Assert
        error.Code.ShouldBe("Conflict.Duplicate");
        error.Description.ShouldBe("Email already exists");
    }

    [Fact]
    public void DomainError_Unauthorized_PrefixesCode()
    {
        // Act
        var error = DomainError.Unauthorized("InvalidCredentials", "Invalid credentials");

        // Assert
        error.Code.ShouldBe("Unauthorized.InvalidCredentials");
    }

    [Fact]
    public void DomainError_Forbidden_PrefixesCode()
    {
        // Act
        var error = DomainError.Forbidden("AccessDenied", "Access denied");

        // Assert
        error.Code.ShouldBe("Forbidden.AccessDenied");
    }

    [Fact]
    public void DomainError_Failure_PrefixesCode()
    {
        // Act
        var error = DomainError.Failure("Operation", "Operation failed");

        // Assert
        error.Code.ShouldBe("Failure.Operation");
    }

    [Fact]
    public void DomainError_Equality_WorksCorrectly()
    {
        // Arrange
        var error1 = new DomainError("Test", "Description");
        var error2 = new DomainError("Test", "Description");
        var error3 = new DomainError("Other", "Description");

        // Assert
        error1.ShouldBe(error2);
        error1.ShouldNotBe(error3);
    }

    #endregion
}
