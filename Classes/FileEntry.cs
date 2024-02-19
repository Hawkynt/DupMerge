#nullable enable
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Libraries;

namespace Classes;

/// <summary>
/// Represents a file already seen by the crawlers, encapsulating details such as its checksum,
/// and providing utilities for comparison.
/// </summary>
internal sealed class FileEntry {

  /// <summary>
  /// P/Invoke stuff
  /// </summary>
  private static class NativeMethods {
    
    public readonly record struct DiskFreeSpace(uint SectorsPerCluster, uint BytesPerSector, uint NumberOfFreeClusters, uint TotalNumberOfClusters);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetDiskFreeSpaceW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool _GetDiskFreeSpace(
      string lpRootPathName,
      out uint lpSectorsPerCluster,
      out uint lpBytesPerSector,
      out uint lpNumberOfFreeClusters,
      out uint lpTotalNumberOfClusters
    );

    public static DiskFreeSpace GetDiskFreeSpace(FileSystemInfo entry) {
      if (!_GetDiskFreeSpace(Path.GetPathRoot(entry.FullName) ?? ".", out var sectorsPerCluster, out var bytesPerSector, out var numberOfFreeClusters, out var totalNumberOfClusters))
        throw new Win32Exception(Marshal.GetLastWin32Error());

      return new(sectorsPerCluster, bytesPerSector, numberOfFreeClusters, totalNumberOfClusters);
    }

  }

  /// <summary>
  /// The pool to rent buffers for comparison operations from.
  /// </summary>
  private static BufferPool? _pool;
  
  /// <summary>
  /// The pool to rent buffers for comparison operations from.
  /// </summary>
  private static BufferPool _Pool => _pool!;
  
  /// <summary>
  /// An empty array.
  /// </summary>
  private static readonly byte[] _EMPTY_BYTES = Array.Empty<byte>();
  
  /// <summary>
  /// Lazily calculates and stores the checksum of this file entry, ensuring the checksum is generated only when needed and cached thereafter.
  /// </summary>
  /// <remarks>
  /// The checksum is calculated the first time it is accessed, allowing for efficient resource usage by avoiding upfront computation.
  /// </remarks>
  private readonly Lazy<byte[]> _checksum;

  private static void _EnsurePoolInitialized(FileSystemInfo anyEntryFromFilesystem) {
    if (_pool != null)
      return;
    
    lock(typeof(FileEntry)) {
      if (_pool != null)
        return;
    
      Console.WriteLine($"[Info] Calculating optimal pool size for {anyEntryFromFilesystem.FullName}");
      
      int blockSize;
      try {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
          var info = NativeMethods.GetDiskFreeSpace(anyEntryFromFilesystem);
          blockSize = (int)(256 * info.SectorsPerCluster * info.BytesPerSector);
          Console.WriteLine($"[Verbose] SectorsPerCluster = {FilesizeFormatter.FormatUnit(info.SectorsPerCluster, true)}, BytesPerSector = {FilesizeFormatter.FormatUnit(info.BytesPerSector, true)}, BytesPerCluster = {FilesizeFormatter.FormatUnit(info.SectorsPerCluster * info.BytesPerSector, true)}, setting block size to {FilesizeFormatter.FormatUnit(blockSize, true)}");
        } else {
          blockSize = 4 * 1024 * 1024;
          Console.WriteLine($"[Warning] Can not determine disk cluster size (fallback block size to {FilesizeFormatter.FormatUnit(blockSize, true)}):OS-Platform unsupported");
        }
      } catch (Exception e) {
        blockSize = 4 * 1024 * 1024;
        Console.WriteLine($"[Error] Could not determine disk cluster size (fallback block size to {FilesizeFormatter.FormatUnit(blockSize, true)}):{e.Message}");
      }

      _pool = new(blockSize);
    }
  }

  public FileEntry(FileInfo source) {
    _EnsurePoolInitialized(source);

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
    
  #if NET7_0_OR_GREATER
    var checksumLength = SHA512.HashSizeInBytes;
  #else
    var checksumLength = 512 / 8;
  #endif
    
    // for small files, don't hash - use their contents
    if (length < checksumLength) {
      var result = new byte[length];
      var _ = stream.Read(result);
      return result;
    }

    using var provider = SHA512.Create();

    using var rented = _Pool.Use();
    var buffer = rented.Buffer;

    // read first block
    var bufferLength = rented.Length;
    var bytesRead = stream.Read(buffer, 0, bufferLength);
    if (length > bufferLength) {
      provider.TransformBlock(buffer, 0, bytesRead, buffer, 0);

      // read last block (or what is left of it)
      stream.Seek(Math.Max(bufferLength, length - bufferLength), SeekOrigin.Begin);
      bytesRead = stream.Read(buffer, 0, bufferLength);
    }

    provider.TransformFinalBlock(buffer, 0, bytesRead);

    return provider.Hash ?? _EMPTY_BYTES;
  }

  /// <summary>
  /// Tests if the given entry has equal file content to this entry.
  /// </summary>
  /// <param name="other">The other.</param>
  /// <returns><see langword="true"/> if both files are equal; otherwise, <see langword="false"/>.</returns>
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

      var bufferLength = _Pool.BufferSize;
    
      // NOTE: we're going to compare buffers (A, A') while reading the next blocks (B, B') in already
      using var sba = _Pool.Use();
      using var cba = _Pool.Use();
      var sourceBufferA = sba.Buffer;
      var comparisonBufferA = cba.Buffer;

      using var sbb = _Pool.Use();
      using var cbb = _Pool.Use();
      var sourceBufferB = sbb.Buffer;
      var comparisonBufferB = cbb.Buffer;

      var blockCount = Math.DivRem(myLength, bufferLength, out var lastBlockSize);

      // if there are bytes left in a partly filled last block - we need one block more
      if (lastBlockSize != 0)
        ++blockCount;

      using var enumerator = BlockIndexShuffler.Shuffle(blockCount).GetEnumerator();

      // NOTE: should never land here, because only 0-byte files would get us an empty enumerator
      if (!enumerator.MoveNext())
        return false;

      var blockIndex = enumerator.Current;

      // start reading buffers into A and A'
      var sourceAsync = sourceStream.ReadBytesAsync(blockIndex * bufferLength, sourceBufferA);
      var comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * bufferLength, comparisonBufferA);
      int sourceBytes;
      int comparisonBytes;

      while (enumerator.MoveNext()) {
        sourceBytes = sourceAsync.Result;
        comparisonBytes = comparisonAsync.Result;

        // start reading next buffers into B and B'
        blockIndex = enumerator.Current;
        sourceAsync = sourceStream.ReadBytesAsync(blockIndex * bufferLength, sourceBufferB);
        comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * bufferLength, comparisonBufferB);

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
