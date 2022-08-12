using System.Collections.Generic;

namespace ExplorerEx.Utils;

/// <summary>
/// 有容量上限的字典，当容量达到上限值，就会自动删除最早添加的项目。线程安全
/// </summary>
internal class LimitedDictionary<TKey, TValue> where TKey : notnull {
	private readonly Dictionary<TKey, TValue> dictionary;
	private readonly Queue<TKey> queue;
	private readonly int capacity;

	public LimitedDictionary(int capacity) {
		this.capacity = capacity;
		dictionary = new Dictionary<TKey, TValue>(capacity);
		queue = new Queue<TKey>(capacity);
	}

	/// <summary>
	/// 添加新项，总是会成功
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	public void Add(TKey key, TValue value) {
		lock (dictionary) {
			if (dictionary.ContainsKey(key)) {
				dictionary[key] = value;
			} else {
				if (queue.Count == capacity) {
					dictionary.Remove(queue.Dequeue());
				}
				queue.Enqueue(key);
				dictionary.Add(key, value);
			}
		}
	}

	public bool TryGetValue(TKey key, out TValue? value) {
		lock (dictionary) {
			return dictionary.TryGetValue(key, out value);
		}
	}
}