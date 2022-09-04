using System;
using System.Diagnostics;
using System.IO;
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
		Debug.Assert(capacity > 4);  // 前4字节存储写入的数据的长度
		this.capacity = capacity;
		mutex = new Mutex(false, name + "Mutex");
		var semName = name + "Semaphore";
		if (createNew) {
			mmf = MemoryMappedFile.CreateNew(name, capacity);
			semaphore = new Semaphore(0, 1, semName);
		} else {
			if (!Semaphore.TryOpenExisting(semName, out var semaphore)) {
				Thread.Sleep(10);
			}
			this.semaphore = semaphore!;
			mmf = MemoryMappedFile.OpenExisting(name);
		}
	}

	/// <summary>
	/// 阻塞当前线程，直到调用了Write
	/// </summary>
	public void WaitForModified() {
		try {
			semaphore.WaitOne();
		} catch (AbandonedMutexException) { }
	}

	/// <summary>
	/// 阻塞当前线程，直到调用了Write
	/// </summary>
	public void WaitForModified(TimeSpan timeout) {
		try {
			semaphore.WaitOne(timeout);
		} catch (AbandonedMutexException) { }
	}

	/// <summary>
	/// 将数据写入区域，请注意，之前的数据会被覆盖（如果没读取的话）
	/// </summary>
	/// <param name="data"></param>
	public void Write(ReadOnlySpan<byte> data) {
		try {
			mutex.WaitOne();
		} catch (AbandonedMutexException) { }
		using var stream = mmf.CreateViewStream(0, capacity, MemoryMappedFileAccess.ReadWrite);
		var buf = new byte[4];
		if (stream.Read(buf) != 4) {
			throw new IOException();
		}
		stream.Seek(0, SeekOrigin.Begin);
		stream.Write(BitConverter.GetBytes(data.Length));
		stream.Write(data);
		var len = BitConverter.ToInt32(buf);
		if (len == 0) {  // 信息已被读取才释放，不然就不释放，相当于直接覆写
			semaphore.Release();
		}
		mutex.ReleaseMutex();
	}

	/// <summary>
	/// 读取数据，如果没有数据或者长度不匹配，返回null
	/// </summary>
	/// <returns></returns>
	public byte[]? Read() {
		try {
			mutex.WaitOne();
		} catch (AbandonedMutexException) { }
		using var stream = mmf.CreateViewStream(0, capacity, MemoryMappedFileAccess.ReadWrite);
		Span<byte> length = stackalloc byte[4];
		if (stream.Read(length) == 4) {
			var size = BitConverter.ToInt32(length);
			var buf = new byte[size];
			var len = stream.Read(buf, 0, size);
			stream.Seek(0, SeekOrigin.Begin);
			stream.Write(BitConverter.GetBytes(0));
			if (len == size) {
				mutex.ReleaseMutex();
				return buf;
			}
		}
		mutex.ReleaseMutex();
		return null;
	}

	public void Dispose() {
		mmf.Dispose();
		mutex.Dispose();
		semaphore.Dispose();
	}
}