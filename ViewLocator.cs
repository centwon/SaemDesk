using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SaemDesk.ViewModels;
using System;
using System.Collections.Generic;

namespace SaemDesk;

/// <summary>
/// AOT-safe ViewLocator: Activator.CreateInstance 대신 수동 팩토리 딕셔너리 사용.
/// 새 ViewModel/View 쌍 추가 시 Register() 호출 추가 (App.axaml.cs).
/// </summary>
public class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> _registry = [];

    public static void Register<TViewModel>(Func<Control> factory)
        where TViewModel : ViewModelBase
        => _registry[typeof(TViewModel)] = factory;

    public Control? Build(object? param)
    {
        if (param is null) return null;

        if (_registry.TryGetValue(param.GetType(), out var factory))
            return factory();

        return new TextBlock { Text = $"View not registered: {param.GetType().Name}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
