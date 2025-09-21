namespace DragonTools.Services;

public static class ServiceLocator
{
    public static readonly TodoService Todos = new(new NotificationService());
}
