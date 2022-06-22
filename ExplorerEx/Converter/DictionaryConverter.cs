using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ExplorerEx.Converter;

public class DependencyKeyValuePair : DependencyObject {
	public object Key { get; set; }

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
		"Value", typeof(object), typeof(DependencyKeyValuePair), new PropertyMetadata(default(object)));

	public object Value {
		get => GetValue(ValueProperty);
		set => SetValue(ValueProperty, value);
	}
}

/// <summary>
/// 将一组键值对转换
/// </summary>
public class DictionaryConverter : IValueConverter {
	public ObservableCollection<DependencyKeyValuePair> Items { get; } = new();

	private readonly Dictionary<object, DependencyKeyValuePair> dictionary = new();

	public DictionaryConverter() {
		Items.CollectionChanged += (_, e) => {
			if (e.OldItems != null) {
				foreach (DependencyKeyValuePair oldItem in e.OldItems) {
					dictionary.Remove(oldItem.Key);
				}
			}
			if (e.NewItems != null) {
				foreach (DependencyKeyValuePair newItem in e.NewItems) {
					dictionary.Add(newItem.Key, newItem);
				}
			}
		};
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
		if (value == null) {
			return null;
		}
		if (dictionary.TryGetValue(value, out var kv)) {
			return kv.Value;
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
		throw new NotImplementedException();
	}
}