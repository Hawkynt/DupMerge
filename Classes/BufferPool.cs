#nullable enable
using Guard;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Classes;

/// <summary>
/// Represents a pool of byte arrays (buffers) that can be rented and returned to reduce the overhead of 
/// allocating and deallocating arrays frequently. This is particularly useful in high-performance scenarios 
/// where arrays are needed temporarily for operations like I/O processing.
/// </summary>
/// <remarks>
/// The <c>BufferPool</c> is designed to manage a set of reusable byte arrays. Each buffer can be rented 
/// for temporary use and then returned to the pool once it is no longer needed. This helps to minimize 
/// garbage collection (GC) pressure by reusing arrays instead of creating new ones for each operation.
/// </remarks>
public sealed class BufferPool {

  /// <summary>
  /// Defines a contract for an object that represents a rented buffer, which must be disposed when no longer needed.
  /// </summary>
  /// <remarks>
  /// Implementations of this interface provide access to a buffer array and ensure that the buffer is properly returned 
  /// to its pool or otherwise managed upon disposal. This pattern helps manage the lifecycle of pooled buffers, reducing
  /// memory allocation overhead and promoting efficient resource reuse.
  /// </remarks>
  public interface IRentBuffer : IDisposable {
    
    /// <summary>
    /// Gets the byte array buffer.
    /// </summary>
    /// <value>The buffer array.</value>
    /// <remarks>
    /// Accessing this property after the object has been disposed throws an <see cref="ObjectDisposedException"/>.
    /// </remarks>
    byte[] Buffer { get; }
  }

  /// <summary>
  /// A sealed class that implements <see cref="IRentBuffer"/>, representing a buffer rented from a buffer pool.
  /// </summary>
  /// <remarks>
  /// This class encapsulates a byte array buffer, ensuring that it is returned to the pool when the instance is disposed. It provides managed access to the buffer, enforcing proper cleanup and resource reuse patterns.
  /// </remarks>
  private sealed class RentBuffer : IRentBuffer {
    
    /// <summary>
    /// The <see cref="BufferPool"/> that owns this buffer, responsible for its allocation and deallocation.
    /// </summary>
    private readonly BufferPool _owner;
    
    
    /// <summary>
    /// The byte array buffer. Access to this field is managed to ensure it is not used after the buffer has been returned to the pool.
    /// </summary>
    private byte[]? _buffer;

    /// <inheritdoc/>
    public byte[] Buffer => this._buffer ?? throw new ObjectDisposedException(nameof(this.Buffer));

    /// <summary>
    /// Initializes a new instance of the <see cref="RentBuffer"/> class, renting a buffer from the specified <see cref="BufferPool"/>.
    /// </summary>
    /// <param name="owner">The buffer pool from which this buffer is rented.</param>
    public RentBuffer(BufferPool owner) {
      this._owner = owner;
      this._buffer = owner._Acquire();
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="RentBuffer"/> class, ensuring the buffer is returned to the pool if not already done.
    /// </summary>
    ~RentBuffer() => this.Dispose();

    /// <summary>
    /// Releases all resources used by the <see cref="RentBuffer"/> instance, returning the buffer to the pool.
    /// </summary>
    /// <remarks>
    /// Calling this method returns the rented buffer to the pool and suppresses finalization for this instance. This method should be called as soon as the buffer is no longer needed to ensure efficient resource reuse.
    /// </remarks>
    public void Dispose() {
      var buffer = Interlocked.Exchange(ref this._buffer, null);
      if (buffer != null)
        this._owner._Release(buffer);

      GC.SuppressFinalize(this);
    }
  }

  /// <summary>
  /// The size of each buffer in the pool, in bytes.
  /// </summary>
  /// <remarks>
  /// This field determines the size of all buffers managed by the pool. It is set during the pool's construction and remains constant throughout the lifetime of the pool.
  /// </remarks>
  private readonly int _bufferSize;

  /// <summary>
  /// The maximum number of buffers that can be waiting in the pool for reuse.
  /// </summary>
  /// <remarks>
  /// This field limits the number of idle buffers in the pool to prevent excessive memory consumption. When the pool's size exceeds this limit, the oldest buffers are removed and left for garbage collection.
  /// </remarks>
  private readonly int _maxBuffersWaitingInPool;

  /// <summary>
  /// The collection of byte arrays that are currently available for rent from the pool.
  /// </summary>
  /// <remarks>
  /// This stack manages the buffers in a last-in-first-out (LIFO) manner, optimizing for the reuse of the most recently returned buffers. This field is initialized upon the creation of the pool and is used to store and manage the available buffers.
  /// </remarks>
  private readonly Stack<byte[]> _pool = new();

  /// <summary>
  /// Initializes a new instance of the <see cref="BufferPool"/> class with a specified buffer size 
  /// and maximum number of buffers to retain in the pool.
  /// </summary>
  /// <param name="bufferSize">The size of each buffer in bytes.</param>
  /// <param name="maxBuffersWaitingInPool">The maximum number of buffers that can wait in the pool. 
  /// Excess buffers will be allowed to be garbage collected.</param>
  /// <exception cref="ArgumentException">Thrown when <paramref name="bufferSize"/> or 
  /// <paramref name="maxBuffersWaitingInPool"/> is less than or equal to zero.</exception>
  public BufferPool(int bufferSize, int maxBuffersWaitingInPool = 64) {
    Against.NegativeValuesAndZero(bufferSize);
    Against.NegativeValuesAndZero(maxBuffersWaitingInPool);

    this._bufferSize = bufferSize;
    this._maxBuffersWaitingInPool = maxBuffersWaitingInPool;
  }

  /// <summary>
  /// Returns a previously rented buffer to the pool.
  /// </summary>
  /// <param name="buffer">The buffer to return to the pool. The buffer must not be <see langword="null"/> and should be of the size specified during the pool's construction.</param>
  /// <remarks>
  /// This method adds the buffer back to the pool for reuse, helping to reduce memory allocation overhead. If the pool exceeds its capacity, the oldest buffers will be removed to maintain the pool size within the specified limits.
  /// </remarks>
  private void _Release(byte[] buffer) {
    Against.ArgumentIsNull(buffer);
    Against.ValuesBelow(buffer.Length, this._bufferSize);

    lock(this._pool) {
      this._pool.Push(buffer);
      while (this._pool.Count > this._maxBuffersWaitingInPool)
        this._pool.Pop();
    }
  }

  /// <summary>
  /// Acquires a buffer from the pool if available; otherwise, allocates a new buffer.
  /// </summary>
  /// <returns>A byte array representing the buffer. The buffer will be of the size specified during the pool's construction.</returns>
  /// <remarks>
  /// This method attempts to minimize memory allocation by reusing buffers from the pool. If no buffers are available or the pool cannot be accessed immediately, a new buffer is allocated to ensure that the operation does not block.
  /// </remarks>
  private byte[] _Acquire() {
    var lockTaken = false;
    try {
      Monitor.TryEnter(this._pool, ref lockTaken);
      if (lockTaken && this._pool.TryPop(out var result))
        return result;
    } finally {
      if(lockTaken)
        Monitor.Exit(this._pool);
    }

    return new byte[this._bufferSize];
  } 

  /// <summary>
  /// Rents a buffer from the pool or creates a new one if the pool is empty. The rented buffer is 
  /// wrapped in an <see cref="IRentBuffer"/> instance for safe management and automatic return to the pool.
  /// </summary>
  /// <returns>An <see cref="IRentBuffer"/> instance containing a byte array. The byte array is guaranteed 
  /// to be at least as large as the buffer size specified during the creation of the <see cref="BufferPool"/>.</returns>
  public IRentBuffer Use() => new RentBuffer(this);

}