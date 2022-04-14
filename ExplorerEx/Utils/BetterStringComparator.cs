using System;

namespace ExplorerEx.Utils; 

internal class BetterStringComparator : StringComparer {
	public override int Compare(string? x, string? y) {
		throw new NotImplementedException();
	}

	public override bool Equals(string? x, string? y) {
		throw new NotImplementedException();
	}

	public override int GetHashCode(string obj) {
		throw new NotImplementedException();
	}
}