namespace DragonTools.Services;

public static class ServiceLocator
{
    public static TodoService Todos => ServiceHelper.Get<TodoService>();
}
