#nullable enable
using System;

namespace Classes;

/// <summary>
/// Encapsulates configuration settings for the application.
/// </summary>
internal sealed class Configuration {
  
  /// <summary>
  /// Gets or sets the minimum file size in bytes for processed files. The default is <see langword="1" /> byte.
  /// </summary>
  public long MinimumFileSizeInBytes { get; set; } = 1;
  
  /// <summary>
  /// Gets or sets the maximum file size in bytes for processing. The default is <see cref="long.MaxValue" />.
  /// </summary>
  public long MaximumFileSizeInBytes { get; set; } = long.MaxValue;
  
  /// <summary>
  /// Indicates whether to also try processing symbolic links. The default is <see langword="false" />.
  /// </summary>
  public bool AlsoTrySymbolicLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to set the read-only attribute on new hard links. The default is <see langword="false" />.
  /// </summary>
  public bool SetReadOnlyAttributeOnNewHardLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to set the read-only attribute on new symbolic links. The default is <see langword="false" />.
  /// </summary>
  public bool SetReadOnlyAttributeOnNewSymbolicLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to set the read-only attribute on existing hard links. The default is <see langword="false" />.
  /// </summary>
  public bool SetReadOnlyAttributeOnExistingHardLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to set the read-only attribute on existing symbolic links. The default is <see langword="false" />.
  /// </summary>
  public bool SetReadOnlyAttributeOnExistingSymbolicLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to remove hard links. The default is <see langword="false" />.
  /// </summary>
  public bool RemoveHardLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to delete files that are hard linked. The default is <see langword="false" />.
  /// </summary>
  public bool DeleteHardLinkedFiles { get; set; }
  
  /// <summary>
  /// Indicates whether to remove symbolic links. The default is <see langword="false" />.
  /// </summary>
  public bool RemoveSymbolicLinks { get; set; }
  
  /// <summary>
  /// Indicates whether to delete files that are symbolically linked. The default is <see langword="false" />.
  /// </summary>
  public bool DeleteSymbolicLinkedFiles { get; set; }
  
  /// <summary>
  /// Gets or sets the maximum number of threads for the crawler. The default is the lesser of the processor count or <see langword="8" />.
  /// </summary>
  public int MaximumCrawlerThreads { get; set; } = Math.Min(Environment.ProcessorCount, 8);
  
  /// <summary>
  /// Indicates whether to show information only, without performing operations. The default is <see langword="false" />.
  /// </summary>
  public bool ShowInfoOnly { get; set; }

}
