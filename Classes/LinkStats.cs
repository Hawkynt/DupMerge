#nullable enable
using System.Threading;

namespace Classes;

/// <summary>
/// Represents statistics for link operations, providing thread-safe counters for seen, created, deleted, and removed links.
/// </summary>
public sealed class LinkStats {
  private long _seen;
  private long _created;
  private long _deleted;
  private long _removed;

  /// <summary>
  /// Gets the number of links seen.
  /// </summary>
  public long Seen => this._seen;
  
  /// <summary>
  /// Gets the number of links created.
  /// </summary>
  public long Created => this._created;
  
  /// <summary>
  /// Gets the number of links deleted.
  /// </summary>
  public long Deleted => this._deleted;
  
  /// <summary>
  /// Gets the number of links removed.
  /// </summary>
  public long Removed => this._removed;

  /// <summary>
  /// Atomically increases the seen link count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the seen link count by. The default is 1.</param>
  public void IncreaseSeen(long count = 1) => Interlocked.Add(ref this._seen, count);
  
  /// <summary>
  /// Atomically increases the created link count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the created link count by. The default is 1.</param>
  public void IncreaseCreated(long count = 1) => Interlocked.Add(ref this._created, count);
  
  /// <summary>
  /// Atomically increases the deleted link count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the deleted link count by. The default is 1.</param>
  public void IncreaseDeleted(long count = 1) => Interlocked.Add(ref this._deleted, count);
  
  /// <summary>
  /// Atomically increases the removed link count by the specified count.
  /// </summary>
  /// <param name="count">The amount to increase the removed link count by. The default is 1.</param>
  public void IncreaseRemoved(long count = 1) => Interlocked.Add(ref this._removed, count);

}
