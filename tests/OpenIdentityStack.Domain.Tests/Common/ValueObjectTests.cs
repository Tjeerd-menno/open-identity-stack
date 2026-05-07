
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Common;
/// <summary>
/// Unit tests for ValueObject base class.
/// </summary>
public sealed class ValueObjectTests
{
    #region Test Value Object Implementations

    /// <summary>
    /// A simple value object for testing with single property.
    /// </summary>
    private sealed class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency)
        {
            this.Amount = amount;
            this.Currency = currency;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return this.Amount;
            yield return this.Currency;
        }
    }

    /// <summary>
    /// A value object with nullable property.
    /// </summary>
    private sealed class Address : ValueObject
    {
        public string Street { get; }
        public string? ApartmentNumber { get; }
        public string City { get; }

        public Address(string street, string? apartmentNumber, string city)
        {
            this.Street = street;
            this.ApartmentNumber = apartmentNumber;
            this.City = city;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return this.Street;
            yield return this.ApartmentNumber;
            yield return this.City;
        }
    }

    /// <summary>
    /// Another value object type for cross-type comparison tests.
    /// </summary>
    private sealed class Point : ValueObject
    {
        public int X { get; }
        public int Y { get; }

        public Point(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return this.X;
            yield return this.Y;
        }
    }

    #endregion

    #region Equals Tests

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.Equals(money2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentAmount_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(200.00m, "USD");

        // Act & Assert
        money1.Equals(money2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentCurrency_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "EUR");

        // Act & Assert
        money1.Equals(money2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_NullValueObject_ReturnsFalse()
    {
        // Arrange
        var money = new Money(100.00m, "USD");
        Money? nullMoney = null;

        // Act & Assert
        money.Equals(nullMoney).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_SameValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        object money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.Equals(money2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_NullObject_ReturnsFalse()
    {
        // Arrange
        var money = new Money(100.00m, "USD");
        object? nullObj = null;

        // Act & Assert
        money.Equals(nullObj).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ReturnsFalse()
    {
        // Arrange
        var money = new Money(100.00m, "USD");
        object differentType = "not a value object";

        // Act & Assert
        money.Equals(differentType).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentValueObjectType_ReturnsFalse()
    {
        // Arrange
        var money = new Money(100.00m, "USD");
        object point = new Point(100, 0);

        // Act & Assert
        money.Equals(point).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNullableProperty_BothNull_ReturnsTrue()
    {
        // Arrange
        var address1 = new Address("123 Main St", null, "City");
        var address2 = new Address("123 Main St", null, "City");

        // Act & Assert
        address1.Equals(address2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithNullableProperty_OneNull_ReturnsFalse()
    {
        // Arrange
        var address1 = new Address("123 Main St", null, "City");
        var address2 = new Address("123 Main St", "Apt 1", "City");

        // Act & Assert
        address1.Equals(address2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNullableProperty_SameValue_ReturnsTrue()
    {
        // Arrange
        var address1 = new Address("123 Main St", "Apt 1", "City");
        var address2 = new Address("123 Main St", "Apt 1", "City");

        // Act & Assert
        address1.Equals(address2).ShouldBeTrue();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        money1.GetHashCode().ShouldBe(money2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ReturnsDifferentHash()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(200.00m, "USD");

        // Act & Assert
        money1.GetHashCode().ShouldNotBe(money2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithNullProperty_DoesNotThrow()
    {
        // Arrange
        var address = new Address("123 Main St", null, "City");

        // Act & Assert - should not throw
        int hashCode = address.GetHashCode();
        hashCode.ShouldBeOfType<int>();
    }

    [Fact]
    public void GetHashCode_ConsistentAcrossCalls()
    {
        // Arrange
        var money = new Money(100.00m, "USD");

        // Act
        int hash1 = money.GetHashCode();
        int hash2 = money.GetHashCode();

        // Assert
        hash1.ShouldBe(hash2);
    }

    #endregion

    #region Operator Tests

    [Fact]
    public void EqualityOperator_SameValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        (money1 == money2).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(200.00m, "USD");

        // Act & Assert
        (money1 == money2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_LeftNull_ReturnsFalse()
    {
        // Arrange
        Money? money1 = null;
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        (money1 == money2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_RightNull_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        Money? money2 = null;

        // Act & Assert
        (money1 == money2).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_BothNull_ReturnsTrue()
    {
        // Arrange
        Money? money1 = null;
        Money? money2 = null;

        // Act & Assert
        (money1 == money2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValues_ReturnsFalse()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        (money1 != money2).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        var money2 = new Money(200.00m, "USD");

        // Act & Assert
        (money1 != money2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_LeftNull_ReturnsTrue()
    {
        // Arrange
        Money? money1 = null;
        var money2 = new Money(100.00m, "USD");

        // Act & Assert
        (money1 != money2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_RightNull_ReturnsTrue()
    {
        // Arrange
        var money1 = new Money(100.00m, "USD");
        Money? money2 = null;

        // Act & Assert
        (money1 != money2).ShouldBeTrue();
    }

    [Fact]
    public void InequalityOperator_BothNull_ReturnsFalse()
    {
        // Arrange
        Money? money1 = null;
        Money? money2 = null;

        // Act & Assert
        (money1 != money2).ShouldBeFalse();
    }

    #endregion
}
