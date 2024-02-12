using System.Threading;

namespace Classes;

internal sealed class RuntimeStats {
  private long _bytesTotal;
  private long _fileCount;
  private long _folderCount;

  public LinkStats HardLinkStats { get; } = new();
  public LinkStats SymbolicLinkStats { get; } = new();
  public long FileCount => this._fileCount;
  public long FolderCount => this._folderCount;
  public long BytesTotal => this._bytesTotal;

  public void IncrementFiles(long count = 1) => Interlocked.Add(ref this._fileCount, count);
  public void IncrementFolders(long count = 1) => Interlocked.Add(ref this._folderCount, count);
  public void IncrementBytes(long count) => Interlocked.Add(ref this._bytesTotal, count);

}
