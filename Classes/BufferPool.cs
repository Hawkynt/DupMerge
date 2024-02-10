#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Classes;

public sealed class BufferPool {

    public interface IRentBuffer:IDisposable {
        byte[] Buffer {get;}
    }

    private sealed class RentBuffer:IRentBuffer {
        private readonly BufferPool _owner;
        private byte[]? _buffer;

        public byte[] Buffer => this._buffer ?? throw new ObjectDisposedException(nameof(this.Buffer));

        public RentBuffer(BufferPool owner){
            this._owner=owner;
            this._buffer=owner._Acquire();
        }

        ~RentBuffer() => this.Dispose();

        public void Dispose() {
            var buffer = Interlocked.Exchange(ref this._buffer,null);
            if(buffer!=null)
                this._owner._Release(buffer);
            
            GC.SuppressFinalize(this);
        } 
    }

    private readonly int _bufferSize;
    private readonly int _maxBuffersWaitingInPool;
    private readonly ConcurrentBag<byte[]> _pool=new();

    public BufferPool(int bufferSize, int maxBuffersWaitingInPool = 64) {
        this._bufferSize=bufferSize;
        this._maxBuffersWaitingInPool=maxBuffersWaitingInPool;
    }

    private void _Release(byte[] buffer) {
        this._pool.Add(buffer);
        while(this._pool.Count > this._maxBuffersWaitingInPool)
            this._pool.TryTake(out _);
    }

    private byte[] _Acquire()=>this._pool.TryTake(out var result)?result:new byte[this._bufferSize];

    public IRentBuffer Use() => new RentBuffer(this);

}