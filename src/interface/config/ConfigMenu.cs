using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ConfigMenu : Control
{
    public GameManager GameManager {
        get; set {
            field = value;
            TryInitialise();
            this.OnReady(OnGameManagerChanged);
            this.OnReady(Rebuild);
        }
    }

    public TitleLine TitleLine { get; private set; }

    [Signal] public delegate void CloseRequestedEventHandler();

    [Export] public BaseButton SidebarButton { get; set; }
    [Export] public Shortcut Shortcut { get; set; }

    protected virtual void OnGameManagerChanged() { }
    protected virtual void Rebuild() { }

    // This has to be here because _Ready gets overwritten by base class
    private bool _configMenuInitialised = false;
    private void TryInitialise() {
        if (_configMenuInitialised) return;
        this.OnReady(() => {
            TitleLine = this.FindChildOfType<TitleLine>();
            TitleLine.IfValid(() => {
                TitleLine.CloseButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
                if (string.IsNullOrEmpty(TitleLine.Title)) TitleLine.Title = GetType().Name.TrimSuffix("Menu").Capitalize();
            });
        });
        _configMenuInitialised = true;
    }


    protected void TextEnterDialog(string actionName, Action<string> onSubmit, IEnumerable<string> options = null) {
        var dialog = new AcceptDialog();
        dialog.Title = actionName;
        dialog.OkButtonText = actionName;
        dialog.AddCancelButton("Cancel");

        // Line edit (can enter anything)
        if (options is null) {
            var lineEdit = new LineEdit();
            dialog.AddChild(lineEdit);
            dialog.RegisterTextEnter(lineEdit);

            dialog.Confirmed += () => onSubmit(lineEdit.Text);
        }
        // Option button (can only enter one of the options)
        else {
            var optionButton = new OptionButton();
            foreach (var (index, option) in options.Index()) optionButton.AddItem(option, index);
            dialog.AddChild(optionButton);

            dialog.Confirmed += () => onSubmit(options.ElementAtOrDefault(optionButton.Selected));
        }

        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Show();
    }
    
    
    protected void Confirm(Action onConfirm, string title = null, string message = null, string acceptText = null) {
        var confirmationDialog = new ConfirmationDialog();
        confirmationDialog.Confirmed += onConfirm;

        if (title is not null) confirmationDialog.Title = title;
        if (message is not null) confirmationDialog.DialogText = message;
        if (acceptText is not null) confirmationDialog.OkButtonText = acceptText;

        AddChild(confirmationDialog);
        confirmationDialog.PopupCentered();
        confirmationDialog.Show();
    }

    protected void Message(string title = null, string message = null, string acceptText = null) {
        var dialog = new AcceptDialog();

        if (title is not null) dialog.Title = title;
        if (message is not null) dialog.DialogText = message;
        if (acceptText is not null) dialog.OkButtonText = acceptText;

        AddChild(dialog);
        dialog.PopupCentered();
        dialog.Show();
    }
}