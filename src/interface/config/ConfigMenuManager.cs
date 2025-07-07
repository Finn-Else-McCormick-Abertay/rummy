using Godot;

namespace Rummy.Interface;

public partial class ConfigMenuManager : Node
{
    [Export] private GameManager _gameManager;

    [Export] private Control _mouseBlocker;
    [Export] private NewGameMenu _newGameMenu;

    public override void _Ready() {
        _mouseBlocker.Hide();
        _newGameMenu.GameManager = _gameManager;
        _newGameMenu.Hide();

        if (!_gameManager.AutoStart) {
            _mouseBlocker.Show();
            _newGameMenu.Show();
        }
    }

}