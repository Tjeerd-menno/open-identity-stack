using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Common;

/// <summary>
/// Tests for the Entity base class.
/// </summary>
public sealed class EntityTests
{
    #region Test Entity Implementation

    /// <summary>
    /// Concrete implementation of Entity for testing.
    /// </summary>
    private sealed class TestEntity : Entity<Guid>
    {
        public string Name { get; private set; }

        public TestEntity(Guid id, string name) : base(id)
        {
            this.Name = name;
            this.CreatedAt = DateTimeOffset.UtcNow;
        }

        public TestEntity() : base()
        {
            this.Name = string.Empty;
        }

        public void UpdateName(string name, DateTimeOffset timestamp)
        {
            this.Name = name;
            this.SetModified(timestamp);
        }
    }

    /// <summary>
    /// Another entity type for cross-type comparison tests.
    /// </summary>
    private sealed class AnotherTestEntity : Entity<Guid>
    {
        public AnotherTestEntity(Guid id) : base(id) { }
    }

    /// <summary>
    /// Entity with int ID for type variance testing.
    /// </summary>
    private sealed class IntIdEntity : Entity<int>
    {
        public IntIdEntity(int id) : base(id) { }
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Entity_WithId_SetsIdCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var entity = new TestEntity(id, "Test");

        // Assert
        entity.Id.ShouldBe(id);
    }

    [Fact]
    public void Entity_Creation_SetsCreatedAt()
    {
        // Arrange
        DateTimeOffset beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Assert
        entity.CreatedAt.ShouldBeGreaterThanOrEqualTo(beforeCreation);
        entity.ModifiedAt.ShouldBeNull();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Entity_SameId_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Entity 1");
        var entity2 = new TestEntity(id, "Entity 2");

        // Act & Assert
        entity1.Equals(entity2).ShouldBeTrue();
        (entity1 == entity2).ShouldBeTrue();
        (entity1 != entity2).ShouldBeFalse();
    }

    [Fact]
    public void Entity_DifferentId_AreNotEqual()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid(), "Entity 1");
        var entity2 = new TestEntity(Guid.NewGuid(), "Entity 2");

        // Act & Assert
        entity1.Equals(entity2).ShouldBeFalse();
        (entity1 == entity2).ShouldBeFalse();
        (entity1 != entity2).ShouldBeTrue();
    }

    [Fact]
    public void Entity_ComparedToNull_IsNotEqual()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Act & Assert
        entity.Equals(null).ShouldBeFalse();
        (entity == null).ShouldBeFalse();
        (entity != null).ShouldBeTrue();
    }

    [Fact]
    public void Entity_ComparedToSelf_IsEqual()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Test");

        // Act & Assert
        entity.Equals(entity).ShouldBeTrue();
        ReferenceEquals(entity, entity).ShouldBeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
        (entity == entity).ShouldBeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void Entity_DifferentTypes_SameId_AreNotEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Test");
        var entity2 = new AnotherTestEntity(id);

        // Act & Assert
        entity1.Equals(entity2).ShouldBeFalse();
    }

    [Fact]
    public void Entity_WithDefaultId_AreNotEqual()
    {
        // Arrange
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        // Act & Assert - entities with default IDs are not equal (even to themselves via another reference)
        entity1.Equals(entity2).ShouldBeFalse();
    }

    [Fact]
    public void Entity_DefaultId_ComparedToEntityWithId_NotEqual()
    {
        // Arrange
        var entity1 = new TestEntity();
        var entity2 = new TestEntity(Guid.NewGuid(), "Test");

        // Act & Assert
        entity1.Equals(entity2).ShouldBeFalse();
        entity2.Equals(entity1).ShouldBeFalse();
    }

    #endregion

    #region GetHashCode Tests

    [Fact]
    public void Entity_SameId_SameHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Entity 1");
        var entity2 = new TestEntity(id, "Entity 2");

        // Act & Assert
        entity1.GetHashCode().ShouldBe(entity2.GetHashCode());
    }

    [Fact]
    public void Entity_DifferentId_DifferentHashCode()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid(), "Entity 1");
        var entity2 = new TestEntity(Guid.NewGuid(), "Entity 2");

        // Act & Assert
        entity1.GetHashCode().ShouldNotBe(entity2.GetHashCode());
    }

    [Fact]
    public void Entity_DefaultId_UsesBaseHashCode()
    {
        // Arrange
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        // Act & Assert - each instance should have different hash codes since they're different objects
        entity1.GetHashCode().ShouldNotBe(entity2.GetHashCode());
    }

    #endregion

    #region Modification Tracking Tests

    [Fact]
    public void Entity_WhenModified_SetsModifiedAt()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Original");
        DateTimeOffset modificationTime = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        entity.UpdateName("Modified", modificationTime);

        // Assert
        entity.Name.ShouldBe("Modified");
        entity.ModifiedAt.ShouldBe(modificationTime);
    }

    [Fact]
    public void Entity_CreatedAt_DoesNotChangeOnModification()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid(), "Original");
        DateTimeOffset originalCreatedAt = entity.CreatedAt;
        DateTimeOffset modificationTime = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        entity.UpdateName("Modified", modificationTime);

        // Assert
        entity.CreatedAt.ShouldBe(originalCreatedAt);
    }

    #endregion

    #region Dictionary Key Tests

    [Fact]
    public void Entity_CanBeUsedAsDictionaryKey()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Entity 1");
        var entity2 = new TestEntity(id, "Entity 2");
        var dictionary = new Dictionary<TestEntity, string>();

        // Act
        dictionary[entity1] = "Value1";
        string retrieved = dictionary[entity2];

        // Assert
        retrieved.ShouldBe("Value1");
    }

    [Fact]
    public void Entity_InHashSet_RecognizesSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id, "Entity 1");
        var entity2 = new TestEntity(id, "Entity 2");
        var hashSet = new HashSet<TestEntity>();

        // Act
        hashSet.Add(entity1);
        bool addedAgain = hashSet.Add(entity2);

        // Assert
        addedAgain.ShouldBeFalse();
        hashSet.Count.ShouldBe(1);
    }

    #endregion

    #region Int ID Tests

    [Fact]
    public void Entity_WithIntId_EqualityWorksCorrectly()
    {
        // Arrange
        var entity1 = new IntIdEntity(1);
        var entity2 = new IntIdEntity(1);
        var entity3 = new IntIdEntity(2);

        // Act & Assert
        entity1.Equals(entity2).ShouldBeTrue();
        entity1.Equals(entity3).ShouldBeFalse();
    }

    #endregion
}
