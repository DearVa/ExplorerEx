using System;
using ExplorerEx.Shell32;
using ExplorerEx.Utils;

namespace ExplorerEx.Model; 

internal sealed class Home : FileItem {
	public const string Uuid = "::{52205fd8-5dfb-447d-801a-d0b52f2e83e1}";

	public static Home Instance { get; } = new();

	// Explicit static constructor to tell C# compiler
	// not to mark type as beforefieldinit
	static Home() { }

	private Home() {
		Name = Uuid;
		Type = "Home".L();
		Icon = IconHelper.ComputerBitmapImage;
	}

	public override string FullPath { get; protected set; }

	public override string DisplayText => "ThisPC".L();
	
	public override void LoadAttributes() {
		throw new InvalidOperationException();
	}

	public override void LoadIcon() {
		throw new InvalidOperationException();
	}

	public override void StartRename() {
		throw new InvalidOperationException();
	}

	protected override bool Rename() {
		throw new InvalidOperationException();
	}
}