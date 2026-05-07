
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Federation;
/// <summary>
/// Unit tests for ClaimMapping and ClaimMappingErrors.
/// </summary>
public sealed class ClaimMappingTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "email",
            "preferred_email",
            TransformType.Direct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SourceClaim.ShouldBe("email");
        result.Value.TargetClaim.ShouldBe("preferred_email");
        result.Value.TransformType.ShouldBe(TransformType.Direct);
        result.Value.TransformPattern.ShouldBeNull();
    }

    [Fact]
    public void Create_WithEmptySourceClaim_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "",
            "target",
            TransformType.Direct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.SourceClaimRequired);
    }

    [Fact]
    public void Create_WithWhitespaceSourceClaim_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "   ",
            "target",
            TransformType.Direct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.SourceClaimRequired);
    }

    [Fact]
    public void Create_WithEmptyTargetClaim_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "",
            TransformType.Direct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.TargetClaimRequired);
    }

    [Fact]
    public void Create_WithWhitespaceTargetClaim_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "   ",
            TransformType.Direct);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.TargetClaimRequired);
    }

    [Fact]
    public void Create_WithRegexTransformButNoPattern_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "target",
            TransformType.Regex,
            null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.TransformPatternRequired);
    }

    [Fact]
    public void Create_WithRegexTransformAndPattern_ReturnsSuccess()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "target",
            TransformType.Regex,
            @"\d+");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TransformPattern.ShouldBe(@"\d+");
    }

    [Fact]
    public void Create_TrimsSourceAndTargetClaims()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "  email  ",
            "  target_email  ",
            TransformType.Direct);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.SourceClaim.ShouldBe("email");
        result.Value.TargetClaim.ShouldBe("target_email");
    }

    [Fact]
    public void Transform_Direct_ReturnsValueUnchanged()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Direct).Value;

        // Act
        string result = mapping.Transform("Hello World");

        // Assert
        result.ShouldBe("Hello World");
    }

    [Fact]
    public void Transform_Lowercase_ReturnsLowercaseValue()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Lowercase).Value;

        // Act
        string result = mapping.Transform("Hello World");

        // Assert
        result.ShouldBe("hello world");
    }

    [Fact]
    public void Transform_Uppercase_ReturnsUppercaseValue()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Uppercase).Value;

        // Act
        string result = mapping.Transform("Hello World");

        // Assert
        result.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public void Transform_EmailPrefix_ReturnsPartBeforeAtSign()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailPrefix).Value;

        // Act
        string result = mapping.Transform("user@example.com");

        // Assert
        result.ShouldBe("user");
    }

    [Fact]
    public void Transform_EmailPrefix_WithNoAtSign_ReturnsOriginalValue()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailPrefix).Value;

        // Act
        string result = mapping.Transform("username");

        // Assert
        result.ShouldBe("username");
    }

    [Fact]
    public void Transform_EmailDomain_ReturnsPartAfterAtSign()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailDomain).Value;

        // Act
        string result = mapping.Transform("user@example.com");

        // Assert
        result.ShouldBe("example.com");
    }

    [Fact]
    public void Transform_EmailDomain_WithNoAtSign_ReturnsOriginalValue()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailDomain).Value;

        // Act
        string result = mapping.Transform("username");

        // Assert
        result.ShouldBe("username");
    }

    [Fact]
    public void Transform_Regex_RemovesMatchingPattern()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Regex, @"\d+").Value;

        // Act
        string result = mapping.Transform("user123name456");

        // Assert
        result.ShouldBe("username");
    }

    [Fact]
    public void Transform_NullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Direct).Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_EmptyValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Direct).Value;

        // Act
        string result = mapping.Transform("");

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Equals_SameSourceClaim_ReturnsTrue()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target1", TransformType.Direct).Value;
        ClaimMapping mapping2 = ClaimMapping.Create("email", "target2", TransformType.Lowercase).Value;

        // Act & Assert
        mapping1.Equals(mapping2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentSourceClaim_ReturnsFalse()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target", TransformType.Direct).Value;
        ClaimMapping mapping2 = ClaimMapping.Create("name", "target", TransformType.Direct).Value;

        // Act & Assert
        mapping1.Equals(mapping2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("email", "target", TransformType.Direct).Value;

        // Act & Assert
        mapping.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_SameSourceClaim_ReturnsSameHash()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target1", TransformType.Direct).Value;
        ClaimMapping mapping2 = ClaimMapping.Create("email", "target2", TransformType.Lowercase).Value;

        // Act & Assert
        mapping1.GetHashCode().ShouldBe(mapping2.GetHashCode());
    }

    [Fact]
    public void Equals_ObjectOverload_WithSameSourceClaim_ReturnsTrue()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target1", TransformType.Direct).Value;
        object mapping2 = ClaimMapping.Create("email", "target2", TransformType.Lowercase).Value;

        // Act & Assert
        mapping1.Equals(mapping2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_WithDifferentSourceClaim_ReturnsFalse()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target", TransformType.Direct).Value;
        object mapping2 = ClaimMapping.Create("name", "target", TransformType.Direct).Value;

        // Act & Assert
        mapping1.Equals(mapping2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_WithNullObject_ReturnsFalse()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("email", "target", TransformType.Direct).Value;
        object? nullObj = null;

        // Act & Assert
        mapping.Equals(nullObj).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("email", "target", TransformType.Direct).Value;
        object differentType = "not a claim mapping";

        // Act & Assert
        mapping.Equals(differentType).ShouldBeFalse();
    }

    [Fact]
    public void Create_WithRegexTransformAndWhitespacePattern_ReturnsError()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "target",
            TransformType.Regex,
            "   ");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClaimMappingErrors.TransformPatternRequired);
    }

    [Fact]
    public void Create_TrimsTransformPattern()
    {
        // Arrange & Act
        Result<ClaimMapping> result = ClaimMapping.Create(
            "source",
            "target",
            TransformType.Regex,
            @"  \d+  ");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TransformPattern.ShouldBe(@"\d+");
    }

    [Fact]
    public void Transform_Lowercase_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Lowercase).Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_Uppercase_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Uppercase).Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_EmailPrefix_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailPrefix).Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_EmailDomain_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailDomain).Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_Regex_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.Regex, @"\d+").Value;

        // Act
        string result = mapping.Transform(null);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_EmailPrefix_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailPrefix).Value;

        // Act
        string result = mapping.Transform("");

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Transform_EmailDomain_WithEmptyEmail_ReturnsEmptyString()
    {
        // Arrange
        ClaimMapping mapping = ClaimMapping.Create("src", "tgt", TransformType.EmailDomain).Value;

        // Act
        string result = mapping.Transform("");

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GetHashCode_DifferentSourceClaims_ReturnsDifferentHashes()
    {
        // Arrange
        ClaimMapping mapping1 = ClaimMapping.Create("email", "target", TransformType.Direct).Value;
        ClaimMapping mapping2 = ClaimMapping.Create("name", "target", TransformType.Direct).Value;

        // Act & Assert
        mapping1.GetHashCode().ShouldNotBe(mapping2.GetHashCode());
    }

    [Theory]
    [InlineData(TransformType.Direct)]
    [InlineData(TransformType.Lowercase)]
    [InlineData(TransformType.Uppercase)]
    [InlineData(TransformType.EmailPrefix)]
    [InlineData(TransformType.EmailDomain)]
    public void Create_WithVariousTransformTypes_ReturnsSuccess(TransformType transformType)
    {
        // Act
        Result<ClaimMapping> result = ClaimMapping.Create("source", "target", transformType);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TransformType.ShouldBe(transformType);
    }
}

/// <summary>
/// Tests for ClaimMappingErrors static error definitions.
/// </summary>
public sealed class ClaimMappingErrorsTests
{
    [Fact]
    public void SourceClaimRequired_HasCorrectCode()
    {
        ClaimMappingErrors.SourceClaimRequired.Code.ShouldContain("SourceClaimRequired");
    }

    [Fact]
    public void SourceClaimRequired_HasDescription()
    {
        ClaimMappingErrors.SourceClaimRequired.Description.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TargetClaimRequired_HasCorrectCode()
    {
        ClaimMappingErrors.TargetClaimRequired.Code.ShouldContain("TargetClaimRequired");
    }

    [Fact]
    public void TargetClaimRequired_HasDescription()
    {
        ClaimMappingErrors.TargetClaimRequired.Description.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TransformPatternRequired_HasCorrectCode()
    {
        ClaimMappingErrors.TransformPatternRequired.Code.ShouldContain("TransformPatternRequired");
    }

    [Fact]
    public void TransformPatternRequired_HasDescription()
    {
        ClaimMappingErrors.TransformPatternRequired.Description.ShouldNotBeNullOrEmpty();
    }
}
