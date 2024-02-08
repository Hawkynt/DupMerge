using System.Threading;

namespace Classes;

public class LinkStats {
  private long _seen;
  private long _created;
  private long _deleted;
  private long _removed;

  public long Seen => this._seen;
  public long Created => this._created;
  public long Deleted => this._deleted;
  public long Removed => this._removed;

  public void IncreaseSeen(long count = 1) => Interlocked.Add(ref this._seen, count);
  public void IncreaseCreated(long count = 1) => Interlocked.Add(ref this._created, count);
  public void IncreaseDeleted(long count = 1) => Interlocked.Add(ref this._deleted, count);
  public void IncreaseRemoved(long count = 1) => Interlocked.Add(ref this._removed, count);

}
