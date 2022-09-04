using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ExplorerProxy {

	/// <summary>
	/// ExplorerEx进程间通信
	/// </summary>
	internal class ExplorerIpc : IDisposable {
		private readonly long capacity;
		private readonly MemoryMappedFile mmf;

		/// <summary>
		/// Write或者Read时必须先锁定
		/// </summary>
		private readonly Mutex mutex;

		private readonly Semaphore semaphore;

		public ExplorerIpc(string name, long capacity) {
			Debug.Assert(capacity > 4); // 前4字节存储写入的数据的长度
			this.capacity = capacity;
			mutex = new Mutex(false, name + "Mutex");
			var semName = name + "Semaphore";
			if (!Semaphore.TryOpenExisting(semName, out var semaphore)) {
				throw new Exception();  // 打不开就直接扔异常了
			}
			this.semaphore = semaphore;
			mmf = MemoryMappedFile.OpenExisting(name);
		}

		/// <summary>
		/// 将数据写入区域，请注意，之前的数据会被覆盖（如果没读取的话）
		/// </summary>
		/// <param name="data"></param>
		public void Write(byte[] data) {
			try {
				if (!mutex.WaitOne(500)) {
					throw new Exception();
				}
			} catch (AbandonedMutexException) { }
			try {
				using (var stream = mmf.CreateViewStream(0, capacity, MemoryMappedFileAccess.ReadWrite)) {
					var buf = new byte[4];
					if (stream.Read(buf, 0, 4) != 4) {
						throw new IOException();
					}
					stream.Seek(0, SeekOrigin.Begin);
					stream.Write(BitConverter.GetBytes(data.Length), 0, sizeof(int));
					stream.Write(data, 0, data.Length);
					var len = BitConverter.ToInt32(buf, 0);
					if (len == 0) { // 信息已被读取才释放，不然就不释放，相当于直接覆写
						semaphore.Release();
					}
				}
			} finally {
				mutex.ReleaseMutex();
			}
		}

		public void Dispose() {
			mmf.Dispose();
			mutex.Dispose();
			semaphore.Dispose();
		}
	}
}