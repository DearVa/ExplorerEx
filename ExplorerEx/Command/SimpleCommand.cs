using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ExplorerEx.Command;

public class SimpleCommand : ICommand {
	public event Action<object> Action;

	public SimpleCommand(Action action) {
		Action += _ => action();
	}

	public SimpleCommand(Action<object> action) {
		Action += action;
	}

	public SimpleCommand(Func<Task> asyncAction) {
		Action += _ => asyncAction();
	}

	public SimpleCommand(Func<object, Task> asyncAction) {
		Action += o => asyncAction(o);
	}

	public bool CanExecute(object parameter) => true;

	public void Execute(object parameter) {
		Action?.Invoke(parameter);
	}

	public event EventHandler CanExecuteChanged;
}