using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ExplorerEx.Views.Controls; 

/// <summary>
///     A implementation of a wrap panel that supports virtualization and can be used in horizontal and vertical
///     orientation.
///     In addition the panel allows to expand one specific item.
///     <p class="note">In order to work properly all items must have the same size.</p>
/// </summary>
public class VirtualizingWrapPanelWithItemExpansion : VirtualizingWrapPanel {
	public static readonly DependencyProperty ExpandedItemTemplateProperty = DependencyProperty.Register(nameof(ExpandedItemTemplate), typeof(DataTemplate), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

	public static readonly DependencyProperty ExpandedItemProperty = DependencyProperty.Register(nameof(ExpandedItem), typeof(object), typeof(VirtualizingWrapPanelWithItemExpansion), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, a) => ((VirtualizingWrapPanelWithItemExpansion)o).ExpandedItemPropertyChanged(a)));

	/// <summary>
	///     Gets or sets the data template used for the item expansion.
	/// </summary>
	public DataTemplate? ExpandedItemTemplate {
		get => (DataTemplate?)GetValue(ExpandedItemTemplateProperty);
		set => SetValue(ExpandedItemTemplateProperty, value);
	}

	/// <summary>
	///     Gets or set the expanded item. The default value is null.
	/// </summary>
	public object? ExpandedItem {
		get => GetValue(ExpandedItemProperty);
		set => SetValue(ExpandedItemProperty, value);
	}

	private int ExpandedItemIndex => Items.IndexOf(ExpandedItem);

	private FrameworkElement? expandedItemChild;

	private int itemIndexFollowingExpansion;

	private void ExpandedItemPropertyChanged(DependencyPropertyChangedEventArgs args) {
		if (args.OldValue != null) {
			var index = InternalChildren.IndexOf(expandedItemChild);
			if (index != -1) {
				expandedItemChild = null;
				RemoveInternalChildRange(index, 1);
			}
		}
	}

	protected override Size CalculateExtent(Size availableSize) {
		var extent = base.CalculateExtent(availableSize);

		if (expandedItemChild != null) {
			if (Orientation == Orientation.Vertical) {
				extent.Height += expandedItemChild.DesiredSize.Height;
			} else {
				extent.Width += expandedItemChild.DesiredSize.Width;
			}
		}

		return extent;
	}

	protected override Size ArrangeOverride(Size finalSize) {
		double expandedItemChildHeight = 0;

		var childSize = CalculateChildArrangeSize(finalSize);

		CalculateSpacing(finalSize, out var innerSpacing, out var outerSpacing);

		for (var childIndex = 0; childIndex < InternalChildren.Count; childIndex++) {
			var child = InternalChildren[childIndex];

			if (child == expandedItemChild) {
				var rowIndex = ExpandedItemIndex / itemsPerRowCount + 1;
				var x = outerSpacing;
				var y = rowIndex * GetHeight(childSize);
				var width = GetWidth(finalSize) - 2 * outerSpacing;
				var height = GetHeight(expandedItemChild.DesiredSize);

				if (SpacingMode == SpacingMode.None) {
					width = itemsPerRowCount * GetWidth(childSize);
				}

				if (Orientation == Orientation.Vertical) {
					expandedItemChild.Arrange(CreateRect(x - GetX(Offset), y - GetY(Offset), width, height));
				} else {
					expandedItemChild.Arrange(CreateRect(x - GetX(Offset), y - GetY(Offset), height, width));
				}
				expandedItemChildHeight = height;
			} else {
				var itemIndex = GetItemIndexFromChildIndex(childIndex);

				var columnIndex = itemIndex % itemsPerRowCount;
				var rowIndex = itemIndex / itemsPerRowCount;

				var x = outerSpacing + columnIndex * (GetWidth(childSize) + innerSpacing);
				var y = rowIndex * GetHeight(childSize) + expandedItemChildHeight;

				child.Arrange(CreateRect(x - GetX(Offset), y - GetY(Offset), childSize.Width, childSize.Height));
			}
		}

		return finalSize;
	}

	protected override void RealizeItems() {
		var startPosition = ItemContainerGenerator.GeneratorPositionFromIndex(ItemRange.StartIndex);

		var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

		var expandedItemIndex = Items.IndexOf(ExpandedItem);
		var itemIndexFollowingExpansion = expandedItemIndex != -1 ? (expandedItemIndex / itemsPerRowCount + 1) * itemsPerRowCount - 1 : -1;
		itemIndexFollowingExpansion = Math.Min(itemIndexFollowingExpansion, Items.Count - 1);

		if (itemIndexFollowingExpansion != this.itemIndexFollowingExpansion && expandedItemChild != null) {
			RemoveInternalChildRange(InternalChildren.IndexOf(expandedItemChild), 1);
			expandedItemChild = null;
		}

		using (ItemContainerGenerator.StartAt(startPosition, GeneratorDirection.Forward, true)) {
			for (var itemIndex = ItemRange.StartIndex; itemIndex <= ItemRange.EndIndex; itemIndex++, childIndex++) {
				var child = (FrameworkElement)ItemContainerGenerator.GenerateNext(out var isNewlyRealized);

				if (isNewlyRealized || /*recycling*/!InternalChildren.Contains(child)) {
					if (childIndex >= InternalChildren.Count) {
						AddInternalChild(child);
					} else {
						InsertInternalChild(childIndex, child);
					}

					ItemContainerGenerator.PrepareItemContainer(child);

					if (ItemSize == Size.Empty) {
						child.Measure(CreateSize(GetWidth(Viewport), double.MaxValue));
					} else {
						child.Measure(ItemSize);
					}
				}

				if (itemIndex == itemIndexFollowingExpansion && ExpandedItemTemplate != null) {
					if (expandedItemChild == null) {
						expandedItemChild = (FrameworkElement)ExpandedItemTemplate.LoadContent();
						expandedItemChild.DataContext = Items[expandedItemIndex];
						expandedItemChild.Measure(CreateSize(GetWidth(Viewport), double.MaxValue));
					}
					if (!InternalChildren.Contains(expandedItemChild)) {
						childIndex++;
						if (childIndex >= InternalChildren.Count) {
							AddInternalChild(expandedItemChild);
						} else {
							InsertInternalChild(childIndex, expandedItemChild);
						}
					}
				}
			}

			this.itemIndexFollowingExpansion = itemIndexFollowingExpansion;
		}
	}

	protected override void OnClearChildren() {
		base.OnClearChildren();
		expandedItemChild = null;
	}

	protected override GeneratorPosition GetGeneratorPositionFromChildIndex(int childIndex) {
		var expandedItemChildIndex = InternalChildren.IndexOf(expandedItemChild);
		if (expandedItemChildIndex != -1 && childIndex > expandedItemChildIndex) {
			return new GeneratorPosition(childIndex - 1, 0);
		}
		return new GeneratorPosition(childIndex, 0);
	}

	protected override void VirtualizeItems() {
		for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--) {
			var child = (FrameworkElement)InternalChildren[childIndex];

			if (child == expandedItemChild) {
				if (!ItemRange.Contains(ExpandedItemIndex)) {
					expandedItemChild = null;
					RemoveInternalChildRange(childIndex, 1);
				}
			} else {
				var itemIndex = Items.IndexOf(child.DataContext);

				var position = ItemContainerGenerator.GeneratorPositionFromIndex(itemIndex);

				if (!ItemRange.Contains(itemIndex)) {
					if (IsRecycling) {
						ItemContainerGenerator.Recycle(position, 1);
					} else {
						ItemContainerGenerator.Remove(position, 1);
					}

					RemoveInternalChildRange(childIndex, 1);
				}
			}
		}
	}

	protected override void BringIndexIntoView(int index) {
		// ReSharper disable once PossibleLossOfFraction
		var offset = index / itemsPerRowCount * GetHeight(childSize);

		if (expandedItemChild != null && index > itemIndexFollowingExpansion) {
			offset += GetHeight(expandedItemChild.DesiredSize);
		}

		if (Orientation == Orientation.Horizontal) {
			SetHorizontalOffset(offset);
		} else {
			SetVerticalOffset(offset);
		}
	}
}