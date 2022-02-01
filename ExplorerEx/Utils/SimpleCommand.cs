using System;
using System.Windows.Input;

namespace ExplorerEx.Utils; 

internal class SimpleCommand : ICommand {
	public event Action<object> Action;

	public SimpleCommand() { }

	public SimpleCommand(Action<object> action) {
		Action += action;
	}

	public bool CanExecute(object parameter) => true;

	public void Execute(object parameter) {
		Action?.Invoke(parameter);
	}

	public event EventHandler CanExecuteChanged;
}