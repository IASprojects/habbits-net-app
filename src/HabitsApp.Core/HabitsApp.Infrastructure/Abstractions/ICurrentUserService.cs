namespace HabitsApp.Infrastructure.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}