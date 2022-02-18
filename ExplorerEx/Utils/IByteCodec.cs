using System;

namespace ExplorerEx.Utils; 

public interface IByteCodec {
	int Length { get; }

	void Encode(Span<byte> buf);

	void Decode(ReadOnlySpan<byte> buf);
}