using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExplorerEx.Model; 

/// <summary>
/// 回收站的一项文件
/// </summary>
public class RecycleBinItem : FileViewBaseItem {
	public override string FullPath { get; protected set; }
	public override string DisplayText { get; }
	public override string Type { get; }
	public override void LoadIcon() {
		throw new NotImplementedException();
	}

	protected override bool Rename() {
		throw new NotImplementedException();
	}
}