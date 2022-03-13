using System;
using ExplorerEx.Utils;
using ExplorerEx.Win32;

namespace ExplorerEx.Model; 

internal sealed class Home : FileViewBaseItem {
	public const string Uuid = "::{52205fd8-5dfb-447d-801a-d0b52f2e83e1}";

	public static Home Instance { get; } = new();

	// Explicit static constructor to tell C# compiler
	// not to mark type as beforefieldinit
	static Home() { }

	private Home() {
		Icon = IconHelper.ComputerBitmapImage;
	}

	public override string FullPath { get; protected set; } = Uuid;

	public override string DisplayText => "This_computer".L();

	public override string Type => "Home".L();

	public override void LoadIcon() {
		throw new NotImplementedException();
	}

	protected override bool Rename() {
		throw new NotImplementedException();
	}
}