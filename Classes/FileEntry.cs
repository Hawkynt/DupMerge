#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;

namespace Classes;

internal sealed class FileEntry {
  private const int _COMPARISON_BLOCK_SIZE = 4 * 1024 * 1024;
  private static readonly BufferPool _pool = new(_COMPARISON_BLOCK_SIZE);
  private static readonly byte[] _EMPTY_BYTES = Array.Empty<byte>();
  private readonly Lazy<byte[]> _checksum;

  public FileEntry(FileInfo source) {
    this._Source = source;
    this._checksum = new(this._CalculateChecksum);
    this._FileSize = source.Length;
  }

  private FileInfo _Source { get; }
  private long _FileSize { get; }
  private byte[] _Checksum => this._checksum.Value;

  /// <summary>
  /// Calculates a quick checksum.
  /// NOTE: In our case we create SHA512 by using the first and last block (if available)
  /// </summary>
  /// <returns></returns>
  private byte[] _CalculateChecksum() {
    var length = this._FileSize;
    if (length <= 0)
      return _EMPTY_BYTES;

    using var stream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
    
    var checksumLength = SHA512.HashSizeInBytes;
    
    // for small files, don't hash - use their contents
    if (length < checksumLength) {
      var result = new byte[length];
      var _ = stream.Read(result, 0, result.Length);
      return result;
    }

    using var provider = SHA512.Create();

    using var rented = _pool.Use();
    var buffer = rented.Buffer;

    // read first block
    var bytesRead = stream.Read(buffer, 0, _COMPARISON_BLOCK_SIZE);
    if (length > _COMPARISON_BLOCK_SIZE) {
      provider.TransformBlock(buffer, 0, bytesRead, buffer, 0);

      // read last block (or what is left of it)
      stream.Seek(Math.Max(_COMPARISON_BLOCK_SIZE, length - _COMPARISON_BLOCK_SIZE), SeekOrigin.Begin);
      bytesRead = stream.Read(buffer, 0, _COMPARISON_BLOCK_SIZE);
    }

    provider.TransformFinalBlock(buffer, 0, bytesRead);

    return provider.Hash ?? _EMPTY_BYTES;
  }

  /// <summary>
  /// Tests if the given entry has equal file content and this entry.
  /// </summary>
  /// <param name="other">The other.</param>
  /// <returns><c>true</c> if both files are equal; otherwise, <c>false</c>.</returns>
  public bool Equals(FileEntry other) {
    try {
      var myLength = this._FileSize;

      // NOTE: STEP 1: compare sizes - should always equal because we make sure that only same size files are compared by the business logic
      if (myLength != other._FileSize)
        return false;

      if (myLength == 0)
        return true;

      // NOTE: STEP 2: compare checksums, hopefully this saves us from comparing byte-by-byte and because checksums are cached in-memory we also spare some re-read I/O
      var sourceChecksum = this._Checksum;
      var comparisonChecksum = other._Checksum;
      if (!BlockComparer.IsEqual(sourceChecksum, sourceChecksum.Length, comparisonChecksum, comparisonChecksum.Length))
        return false;

      // NOTE: STEP 3: compare bytewise
      using var sourceStream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
      using var comparisonStream = new FileStream(other._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

      // NOTE: we're going to compare buffers (A, A') while reading the next blocks (B, B') in already
      using var sba = _pool.Use();
      using var cba = _pool.Use();
      var sourceBufferA = sba.Buffer;
      var comparisonBufferA = cba.Buffer;

      using var sbb = _pool.Use();
      using var cbb = _pool.Use();
      var sourceBufferB = sbb.Buffer;
      var comparisonBufferB = cbb.Buffer;

      var blockCount = Math.DivRem(myLength, _COMPARISON_BLOCK_SIZE, out var lastBlockSize);

      // if there are bytes left in a partly filled last block - we need one block more
      if (lastBlockSize != 0)
        ++blockCount;

      using var enumerator = BlockIndexShuffler.Shuffle(blockCount).GetEnumerator();

      // NOTE: should never land here, because only 0-byte files would get us an empty enumerator
      if (!enumerator.MoveNext())
        return false;

      var blockIndex = enumerator.Current;

      // start reading buffers into A and A'
      var sourceAsync = sourceStream.ReadBytesAsync(blockIndex * _COMPARISON_BLOCK_SIZE, sourceBufferA, 0, _COMPARISON_BLOCK_SIZE);
      var comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * _COMPARISON_BLOCK_SIZE, comparisonBufferA, 0, _COMPARISON_BLOCK_SIZE);
      int sourceBytes;
      int comparisonBytes;

      while (enumerator.MoveNext()) {
        sourceBytes = sourceAsync.Result;
        comparisonBytes = comparisonAsync.Result;

        // start reading next buffers into B and B'
        blockIndex = enumerator.Current;
        sourceAsync = sourceStream.ReadBytesAsync(blockIndex * _COMPARISON_BLOCK_SIZE, sourceBufferB, 0, _COMPARISON_BLOCK_SIZE);
        comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * _COMPARISON_BLOCK_SIZE, comparisonBufferB, 0, _COMPARISON_BLOCK_SIZE);

        // compare A and A' and return false upon difference
        if (!BlockComparer.IsEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes))
          return false;

        // switch A and B and A' and B'
        (sourceBufferA, sourceBufferB, comparisonBufferA, comparisonBufferB)
          = (sourceBufferB, sourceBufferA, comparisonBufferB, comparisonBufferA)
          ;

      }

      // compare A and A'
      sourceBytes = sourceAsync.Result;
      comparisonBytes = comparisonAsync.Result;
      return BlockComparer.IsEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes);

    } catch (Exception e) {

      // TODO: find out which side failed and remove it from the known files list
      Console.WriteLine($"[Error] A comparison failed:{e.Message}");

      // if either file could not be read - assume they are not equal because we can't be sure
      return false;
    }
  }

}
