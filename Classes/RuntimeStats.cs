#nullable enable
using System.Threading;

namespace Classes;

/// <summary>
/// Provides runtime statistics for file system operations, including counts of files and folders processed, total bytes processed, and detailed statistics for hard and symbolic links.
/// </summary>
internal sealed class RuntimeStats {
  private long _bytesTotal;
  private long _fileCount;
  private long _folderCount;

  /// <summary>
  /// Gets the statistics for hard links encountered during runtime.
  /// </summary>
  public LinkStats HardLinkStats { get; } = new();

  /// <summary>
  /// Gets the statistics for symbolic links encountered during runtime.
  /// </summary>
  public LinkStats SymbolicLinkStats { get; } = new();

  /// <summary>
  /// Gets the total count of files processed.
  /// </summary>
  public long FileCount => this._fileCount;

  /// <summary>
  /// Gets the total count of folders processed.
  /// </summary>
  public long FolderCount => this._folderCount;

  /// <summary>
  /// Gets the total number of bytes processed across all files.
  /// </summary>
  public long BytesTotal => this._bytesTotal;

  /// <summary>
  /// Atomically increments the file count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the file count by. The default is 1.</param>
  public void IncrementFiles(long count = 1) => Interlocked.Add(ref this._fileCount, count);

  /// <summary>
  /// Atomically increments the folder count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the folder count by. The default is 1.</param>
  public void IncrementFolders(long count = 1) => Interlocked.Add(ref this._folderCount, count);

  /// <summary>
  /// Atomically increments the total bytes processed by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the total bytes by.</param>
  public void IncrementBytes(long count) => Interlocked.Add(ref this._bytesTotal, count);

}
