namespace ExplorerEx.Definitions.Interfaces;

public interface IConfigureService {
	void Save(string key, object value);

	/// <summary>
	/// 暂存到buffer，1s之后再存储
	/// </summary>
	/// <param name="key"></param>
	/// <param name="value"></param>
	void SaveToBuffer(string key, object value);

	object? Load(string key, object? defaultValue = default);

	bool LoadBoolean(string key, bool defaultValue = default);

	int LoadInt(string key, int defaultValue = default);

	double LoadDouble(string key, int defaultValue = default);

	string? LoadString(string key, string? defaultValue = default);

	bool Delete(string key);
}