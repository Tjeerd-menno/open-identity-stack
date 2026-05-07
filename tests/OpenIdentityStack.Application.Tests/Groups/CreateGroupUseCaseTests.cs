using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
public sealed class CreateGroupUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CreateGroupUseCase _useCase;

    public CreateGroupUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._useCase = new CreateGroupUseCase(this._groupRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConflict_WhenNameExists()
    {
        // Arrange
        var command = new CreateGroupCommand("group", "desc");
        this._groupRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result<CreateGroupResponse> result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NameAlreadyExists");
        await this._groupRepository.DidNotReceive().AddAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenGroupInvalid()
    {
        // Arrange
        var command = new CreateGroupCommand(string.Empty, "desc");
        this._groupRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateGroupResponse> result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NameRequired");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateGroup_WhenValid()
    {
        // Arrange
        var command = new CreateGroupCommand("group", "desc");
        this._groupRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result<CreateGroupResponse> result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("group");
        await this._groupRepository.Received(1).AddAsync(Arg.Any<Group>(), Arg.Any<CancellationToken>());
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
