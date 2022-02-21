using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ExplorerEx.Utils;

/// <summary>
/// 一个内存映射文件，但是可以等待写入
/// </summary>
internal class NotifyMemoryMappedFile : IDisposable {
	private readonly long capacity;
	private readonly MemoryMappedFile mmf;
	/// <summary>
	/// Write或者Read时必须先锁定
	/// </summary>
	private readonly Mutex mutex;
	private readonly Semaphore semaphore;

	public NotifyMemoryMappedFile(string name, long capacity, bool createNew) {
		if (capacity < 5) {  // 前4字节存储写入的数据的长度
			throw new ArgumentOutOfRangeException(nameof(capacity));
		}
		this.capacity = capacity;
		mmf = createNew ? MemoryMappedFile.CreateNew(name, capacity) : MemoryMappedFile.OpenExisting(name);
		mutex = new Mutex(false, name + "Mutex");
		semaphore = createNew ? new Semaphore(0, 1, name + "Seam") : Semaphore.OpenExisting(name + "Seam");
	}

	/// <summary>
	/// 阻塞当前线程，直到调用了Write
	/// </summary>
	public void WaitForModified() {
		semaphore.WaitOne();
	}

	/// <summary>
	/// 阻塞当前线程，直到调用了Write
	/// </summary>
	public void WaitForModified(TimeSpan timeout) {
		semaphore.WaitOne(timeout);
	}

	/// <summary>
	/// 将数据写入区域，请注意，之前的数据会被覆盖
	/// </summary>
	/// <param name="data"></param>
	public void Write(ReadOnlySpan<byte> data) {
		mutex.WaitOne();
		using var stream = mmf.CreateViewStream(0, capacity, MemoryMappedFileAccess.ReadWrite);
		stream.Write(BitConverter.GetBytes(data.Length));
		stream.Write(data);
		semaphore.Release();
		mutex.ReleaseMutex();
	}

	/// <summary>
	/// 读取数据，如果没有数据或者长度不匹配，返回null
	/// </summary>
	/// <returns></returns>
	public byte[] Read() {
		mutex.WaitOne();
		using var stream = mmf.CreateViewStream(0, capacity, MemoryMappedFileAccess.Read);
		Span<byte> length = stackalloc byte[4];
		if (stream.Read(length) == 4) {
			var size = BitConverter.ToInt32(length);
			var buf = new byte[size];
			if (stream.Read(buf, 0, size) == size) {
				mutex.ReleaseMutex();
				return buf;
			}
		}
		mutex.ReleaseMutex();
		return null;
	}

	public void Dispose() {
		mmf?.Dispose();
		mutex?.Dispose();
		semaphore?.Dispose();
	}
}