using System;
using System.Collections.Generic;

namespace ExplorerEx.Utils;

public class Arguments {
	public bool RunInBackground { get; }

	public bool ShowHelp { get; }

	public bool OpenInNewWindow { get; }

	public bool RequireDebugger { get; }

	public List<string> Paths { get; } = new();

	public Arguments(IEnumerable<string> args) {
		foreach (var arg in args) {
			if (arg.Length > 1) {
				if (arg[0] == '-') {
					if (arg.Length > 2 && arg[1] == '-') {
						switch (arg[2..]) {
						case "help":
							ShowHelp = true;
							break;
						case "runInBackground":
							RunInBackground = true;
							break;
						case "openInNewWindow":
							OpenInNewWindow = true;
							break;
						case "requireDebugger":
							RequireDebugger = true;
							break;
						default:
							throw new ArgumentException(arg);
						}
					} else {
						switch (arg[1]) {
						case 'h':
							ShowHelp = true;
							break;
						case 'b':
							RunInBackground = true;
							break;
						case 'o':
							OpenInNewWindow = true;
							break;
						case 'd':
							RequireDebugger = true;
							break;
						default:
							throw new ArgumentException(arg);
						}
					}
				} else {
					Paths.Add(arg);
				}
			} else {
				Paths.Add(arg);
			}
		}
	}
}