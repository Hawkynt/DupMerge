using System;
using System.Threading;

namespace Classes {
  internal class Configuration {
    private long _bytesTotal;
    private long _fileCount;
    private long _folderCount;

    public long MinimumFileSizeInBytes { get; set; } = 1;
    public long MaximumFileSizeInBytes { get; set; } = long.MaxValue;
    public bool AlsoTrySymbolicLinks { get; set; }
    public bool SetReadOnlyAttributeOnNewHardLinks { get; set; }
    public bool SetReadOnlyAttributeOnNewSymbolicLinks { get; set; }
    public bool SetReadOnlyAttributeOnExistingHardLinks { get; set; }
    public bool SetReadOnlyAttributeOnExistingSymbolicLinks { get; set; }
    public bool RemoveHardLinks { get; set; }
    public bool DeleteHardLinkedFiles { get; set; }
    public bool RemoveSymbolicLinks { get; set; }
    public bool DeleteSymbolicLinkedFiles { get; set; }
    public int MaximumCrawlerThreads { get; set; } = Math.Min(Environment.ProcessorCount, 8);
    public bool ShowInfoOnly { get; set; }
    public LinkStats HardLinkStats { get; } = new LinkStats();
    public LinkStats SymbolicLinkStats { get; } = new LinkStats();
    public long FileCount => this._fileCount;
    public long FolderCount => this._folderCount;
    public long BytesTotal => this._bytesTotal;

    public void IncrementFiles(long count = 1) => Interlocked.Add(ref this._fileCount, count);
    public void IncrementFolders(long count = 1) => Interlocked.Add(ref this._folderCount, count);
    public void IncrementBytes(long count) => Interlocked.Add(ref this._bytesTotal, count);

  }
}
