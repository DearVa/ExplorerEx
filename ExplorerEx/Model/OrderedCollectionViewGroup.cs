using System.Windows.Data;

namespace ExplorerEx.Model; 

internal class OrderedCollectionViewGroup : CollectionViewGroup {
	public int Order { get; }

	public OrderedCollectionViewGroup(string name, int order) : base(name) {
		Order = order;
	}

	public override bool IsBottomLevel => true;
}