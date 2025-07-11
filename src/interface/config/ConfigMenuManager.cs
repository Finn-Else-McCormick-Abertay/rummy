using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ConfigMenuManager : Node
{
    [Export] private GameManager _gameManager;
    [Export] private Control _mouseBlocker;

    public IEnumerable<ConfigMenu> Menus { get; private set; }
    private void UpdateMenuCache() {
        Menus = this.FindChildrenOfType<ConfigMenu>();
        // Clear toggle actions for removed menus
        _menuToggleActions = _menuToggleActions.Where(x => Menus.Contains(x.Key)).ToDictionary();
        // Create toggle actions for added menus
        foreach (var menuMissingToggle in Menus.Where(x => !_menuToggleActions.ContainsKey(x))) _menuToggleActions[menuMissingToggle] = () => ToggleMenu(menuMissingToggle);

        foreach (var menu in Menus) {
            menu.GameManager = _gameManager;
            if (menu.SidebarButton.IsValid()) menu.SidebarButton.TryConnect(BaseButton.SignalName.Pressed, _menuToggleActions[menu]);
            menu.TryConnect(ConfigMenu.SignalName.CloseRequested, CloseMenu);
        }
    }
    private Dictionary<ConfigMenu, Action> _menuToggleActions = []; 

    public override void _Ready() {
        UpdateMenuCache(); ChildOrderChanged += UpdateMenuCache;
        CloseMenu();

        if (!_gameManager.AutoStart) {
            // Don't ask
            SwitchToMenu<NewGameMenu>();
            var closeTimer = GetTree().CreateTimer(0.05); closeTimer.Timeout += CloseMenu;
            var reopenTimer = GetTree().CreateTimer(0.1); reopenTimer.Timeout += SwitchToMenu<NewGameMenu>;
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (@event.IsPressed()) {
            foreach (var menu in Menus.Where(x => x.IsValid() && x.Shortcut.IsValid())) {
                if (menu.Shortcut.MatchesEvent(@event)) ToggleMenu(menu);
            }
        }
    }

    private void SwitchToMenu(ConfigMenu menu) {
        _mouseBlocker.Visible = menu is not null;
        foreach (var otherMenu in Menus) otherMenu.Visible = otherMenu == menu;
    }
    private void ToggleMenu(ConfigMenu menu) => SwitchToMenu(menu is null || menu.Visible ? null : menu);

    public void SwitchToMenu<T>() => SwitchToMenu(Menus.FirstOrDefault(x => x is T));
    public void ToggleMenu<T>() => ToggleMenu(Menus.FirstOrDefault(x => x is T));

    public void CloseMenu() => SwitchToMenu(null);

}