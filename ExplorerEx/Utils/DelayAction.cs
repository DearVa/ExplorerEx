using System;
using System.Threading.Tasks;

namespace ExplorerEx.Utils; 

/// <summary>
/// 创建一个延迟执行的任务
/// </summary>
internal class DelayAction {
	public event Action Action;

	private DateTimeOffset desireFireTime;

	private bool isCancelled;

	private Task? task;

	private readonly TimeSpan delay;

	public DelayAction(TimeSpan delay, Action action) {
		this.delay = delay;
		Action = action;
	}

	public void Start() {
		desireFireTime = DateTimeOffset.Now + delay;
		isCancelled = false;
		if (task is { IsCompleted: false }) {
			return;
		}
		task = Task.Run(async () => {
			while (true) {
				var now = DateTimeOffset.Now;
				if (now >= desireFireTime) {
					break;
				}
				await Task.Delay(desireFireTime - now);
			}
			if (isCancelled) {
				return;
			}
			Action?.Invoke();
		});
	}

	public void Stop() {
		isCancelled = true;
	}
}