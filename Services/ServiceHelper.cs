using System;
using Microsoft.Extensions.DependencyInjection;

namespace DragonTools.Services;

public static class ServiceHelper
{
    private static IServiceProvider? _provider;
    public static void Init(IServiceProvider provider) => _provider = provider;
    public static T Get<T>() where T : notnull
    {
        if (_provider == null) throw new InvalidOperationException("Service provider not initialized");
        var service = _provider.GetService<T>();
        if (service == null) throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
        return service;
    }
}
