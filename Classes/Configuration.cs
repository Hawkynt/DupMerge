using System;

namespace Classes {
  internal class Configuration {
    
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

  }
}
