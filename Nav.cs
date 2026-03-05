using CustomCodeSystem.Pages;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CustomCodeSystem;

public static class Nav
{
    private static Frame? _frame;

    // тут будут жить страницы в памяти (кэш)
    private static readonly Dictionary<string, Page> _pages =
        new(StringComparer.OrdinalIgnoreCase);

    // тут руками прописываем ВСЕ страницы (имя -> фабрика)
    private static readonly Dictionary<string, Func<Page>> _factories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ВАЖНО: добавляй страницы сюда руками
            ["PageLogin"] = () => new PageLogin(),
            ["PageMain"] = () => new PageMain(),
            ["PageConfigSns"] = () => new PageConfigSns(),
            ["PageLinkSns"] = () => new PageLinkSns(),
            ["PageScanOperations"] = () => new PageScanOperations(),
            ["PageSearchActions"] = () => new PageSearchActions(),
        };

    public static void Init(Frame frame) => _frame = frame;

    /// <param name="name">Имя страницы, например "Home"</param>
    /// <param name="recreate">true = пересоздать страницу с нуля</param>
    public static void Go(string name, bool recreate = false)
    {
        if (_frame == null)
            throw new InvalidOperationException("Nav.Init(frame) must be called first.");

        if (!_factories.TryGetValue(name, out var factory))
            throw new ArgumentException($"Unknown page '{name}'. Add it to Nav._factories.");

        if (recreate || !_pages.TryGetValue(name, out var page))
        {
            page = factory();
            _pages[name] = page; // хранится в памяти
        }

        _frame.Content = page; // без истории, просто показ
    }

    // опционально: если вдруг нужно очистить кэш
    public static void ClearCache() => _pages.Clear();

    public static void NavigationUIHide()
    {
        _frame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
    }

    public static void NavigationUIShow()
    {
        _frame.NavigationUIVisibility = NavigationUIVisibility.Visible;
    }


    public static void ClearHistory()
    {
        if (_frame == null)
            throw new InvalidOperationException("Nav.Init(frame) must be called first.");

        var ns = _frame.NavigationService;
        if (ns == null) return;

        // На случай, если где-то всё же происходит навигация через NS,
        // чистим стек сразу после навигации.
        NavigatedEventHandler? handler = null;
        handler = (_, __) =>
        {
            while (ns.RemoveBackEntry() != null) { }
            // Forward-стек в WPF очищается при обычной навигации;
            // для большинства сценариев этого достаточно.
            ns.Navigated -= handler!;
        };

        ns.Navigated += handler;

        // Если прямо сейчас уже есть записи в back-stack — подчистим сразу
        while (ns.RemoveBackEntry() != null) { }
    }
}
