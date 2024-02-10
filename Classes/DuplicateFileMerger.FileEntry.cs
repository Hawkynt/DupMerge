using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Libraries;

namespace Classes;

partial class DuplicateFileMerger {

  private unsafe class FileEntry {
    private const int _COMPARISON_BLOCK_SIZE = 4*1024*1024;
    private static BufferPool _pool=new(_COMPARISON_BLOCK_SIZE);
    private static readonly byte[] _EMPTY_BYTES = new byte[0];
    private readonly Lazy<byte[]> _checksum;

    public FileEntry(FileInfo source) {
      this._Source = source;
      this._checksum = new Lazy<byte[]>(this._CalculateChecksum);
      this._FileSize = source.Length;
    }

    private FileInfo _Source { get; }
    private long _FileSize {get;}
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

      byte[] result;

      using var provider = new SHA512CryptoServiceProvider();
      using var stream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
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
      result = provider.Hash;
      
      return result;
    }

    /// <summary>
    /// Creates a stream of block indexes, which are not simply following each other the get better chances to detect differences early.
    /// NOTE: In our case we alternate between a block from the beginning and a block from the ending of a file.
    /// </summary>
    /// <param name="blockCount">The block count.</param>
    /// <returns>Block indices</returns>
    private static IEnumerable<long> _BlockIndexShuffler(long blockCount) {
      var lowerBlockIndex = 0;
      var upperBlockIndex = blockCount - 1;

      while (lowerBlockIndex < upperBlockIndex) {
        yield return lowerBlockIndex++;
        yield return upperBlockIndex--;
      }

      // if odd number of elements, return the last element (which is in the middle)
      if ((blockCount & 1) == 1)
        yield return lowerBlockIndex;

    }

    /// <summary>
    /// Tests if the given entry has equal file content and this entry.
    /// </summary>
    /// <param name="other">The other.</param>
    /// <returns><c>true</c> if both files are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(FileEntry other) {
      if (ReferenceEquals(other, null))
        return false;

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
        if (!_ByteArraysEqual(sourceChecksum, sourceChecksum.Length, comparisonChecksum, comparisonChecksum.Length))
          return false;

        // NOTE: STEP 3: compare bytewise
        using var sourceStream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var comparisonStream = new FileStream(other._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

        // NOTE: we're going to compare buffers (A, A') while reading the next blocks (B, B') in already
        using var sba=_pool.Use();
        using var cba=_pool.Use();
        var sourceBufferA = sba.Buffer;
        var comparisonBufferA = cba.Buffer;

        using var sbb=_pool.Use();
        using var cbb=_pool.Use();
        var sourceBufferB = sbb.Buffer;
        var comparisonBufferB = cbb.Buffer;

        long lastBlockSize;
        var blockCount = Math.DivRem(myLength, _COMPARISON_BLOCK_SIZE, out lastBlockSize);

        // if there are bytes left in a partly filled last block - we need one block more
        if (lastBlockSize != 0)
          ++blockCount;

        using var enumerator = _BlockIndexShuffler(blockCount).GetEnumerator();

        // NOTE: should never land here, because only 0-byte files would get us an empty enumerator
        if (!enumerator.MoveNext())
          return false;

        var blockIndex = enumerator.Current;

        // start reading buffers into A and A'
        var sourceAsync = _ReadBlockFromStream(sourceStream, blockIndex, sourceBufferA);
        var comparisonAsync = _ReadBlockFromStream(comparisonStream, blockIndex, comparisonBufferA);
        int sourceBytes;
        int comparisonBytes;

        while (enumerator.MoveNext()) {
          sourceBytes = sourceAsync.Result;
          comparisonBytes = comparisonAsync.Result;

          // start reading next buffers into B and B'
          blockIndex = enumerator.Current;
          sourceAsync = _ReadBlockFromStream(sourceStream, blockIndex, sourceBufferB);
          comparisonAsync = _ReadBlockFromStream(comparisonStream, blockIndex, comparisonBufferB);

          // compare A and A' and return false upon difference
          if (!_ByteArraysEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes))
            return false;

          // switch A and B and A' and B'
          (sourceBufferA, sourceBufferB, comparisonBufferA, comparisonBufferB) 
          = (sourceBufferB, sourceBufferA, comparisonBufferB, comparisonBufferA)
          ;

        }

        // compare A and A'
        sourceBytes = sourceAsync.Result;
        comparisonBytes = comparisonAsync.Result;
        return _ByteArraysEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes);
      
      } catch (Exception e) {

        // TODO: find out which side failed and remove it from the known files list
        Console.WriteLine($"[Error] A comparison failed:{e.Message}");

        // if either file could not be read - assume they are not equal because we can't be sure
        return false;
      }
    }

    #region static utils

    /// <summary>
    /// Compares two byte-arrays starting from their beginnings.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sourceLength">Length of the source.</param>
    /// <param name="comparison">The comparison.</param>
    /// <param name="comparisonLength">Length of the comparison.</param>
    /// <returns><c>true</c> if both arrays contain the same data; otherwise, <c>false</c>.</returns>
    private static unsafe bool _ByteArraysEqual(byte[] source, int sourceLength, byte[] comparison, int comparisonLength) {
      Debug.Assert(!ReferenceEquals(source, null), "Source must not be <null>");
      Debug.Assert(!ReferenceEquals(comparison, null), "Comparison must not be <null>");

      if (sourceLength != comparisonLength)
        return false;

      if (ReferenceEquals(source, comparison))
        return true;

      fixed (byte* sourcePin = source, comparisonPin = comparison) {
        
        static bool CompareUInt64(byte* source, byte* comparison, ref int count) {
          var s=(long*)source;
          var c=(long*)comparison;
          while(count>=sizeof(long)){
            if(*s!=*c)
              return false;

            ++s;
            ++c;
            count-=sizeof(long);
          }
          return true;
        }

        static bool CompareUInt32(byte* source, byte* comparison, ref int count) {
          var s=(int*)source;
          var c=(int*)comparison;
          while(count>=sizeof(int)){
            if(*s!=*c)
              return false;

            ++s;
            ++c;
            count-=sizeof(int);
          }
          return true;
        }

        static bool CompareBytes(byte* source, byte* comparison, ref int count) {
          while(count>0){
            if(*source!=*comparison)
              return false;

            ++source;
            ++comparison;
            --count;
          }
          return true;
        }

        var result=
          CompareUInt64(sourcePin,comparisonPin,ref sourceLength)
          && CompareUInt32(sourcePin,comparisonPin,ref sourceLength)
          && CompareBytes(sourcePin,comparisonPin,ref sourceLength)
          ;

        return result;
      }
    }

    /// <summary>
    /// Starts filling a block from a stream with a given index.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="blockIndex">Index of the block.</param>
    /// <param name="buffer">The buffer to store data at.</param>
    /// <returns>A Task to wait on</returns>
    private static Task<int> _ReadBlockFromStream(Stream stream, long blockIndex, byte[] buffer) {
      stream.Seek(blockIndex * _COMPARISON_BLOCK_SIZE, SeekOrigin.Begin);
      return stream.ReadAsync(buffer, 0, _COMPARISON_BLOCK_SIZE);
    }

    #endregion

  }
}
