using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Classes;

namespace DupMerge.Tests.Performance;

[TestFixture]
[Category("Performance")]
public class PerformanceTests {

  [Test]
  public void RunBenchmarks() {
    // This test serves as an entry point to run benchmarks
    // In a real scenario, you'd run benchmarks separately
    Assert.Pass("Benchmarks should be run separately using BenchmarkDotNet runner");
  }
}

[MemoryDiagnoser]
[SimpleJob]
public class BufferPoolBenchmarks {
  private BufferPool? _pool;
  private const int BufferSize = 64 * 1024; // 64KB
  private const int MaxBuffers = 10;

  [GlobalSetup]
  public void Setup() {
    _pool = new BufferPool(BufferSize, MaxBuffers);
  }

  [Benchmark]
  public void RentAndReturn_Single() {
    using var rented = _pool!.Use();
    // Simulate some work
    var buffer = rented.Buffer;
    for (int i = 0; i < Math.Min(1000, buffer.Length); i++) {
      buffer[i] = (byte)(i % 256);
    }
  }

  [Benchmark]
  public void RentAndReturn_Multiple() {
    for (int i = 0; i < 10; i++) {
      using var rented = _pool!.Use();
      var buffer = rented.Buffer;
      for (int j = 0; j < Math.Min(100, buffer.Length); j++) {
        buffer[j] = (byte)(j % 256);
      }
    }
  }

  [Benchmark]
  public void DirectAllocation_Single() {
    var buffer = new byte[BufferSize];
    // Simulate some work
    for (int i = 0; i < Math.Min(1000, buffer.Length); i++) {
      buffer[i] = (byte)(i % 256);
    }
  }

  [Benchmark]
  public void DirectAllocation_Multiple() {
    for (int i = 0; i < 10; i++) {
      var buffer = new byte[BufferSize];
      for (int j = 0; j < Math.Min(100, buffer.Length); j++) {
        buffer[j] = (byte)(j % 256);
      }
    }
  }
}

[MemoryDiagnoser]
[SimpleJob]
public class BlockComparerBenchmarks {
  private byte[]? _array1;
  private byte[]? _array2;
  private byte[]? _differentArray;

  [Params(1024, 8192, 65536, 1048576)] // 1KB, 8KB, 64KB, 1MB
  public int Size { get; set; }

  [GlobalSetup]
  public void Setup() {
    _array1 = new byte[Size];
    _array2 = new byte[Size];
    _differentArray = new byte[Size];

    var random = new Random(42);
    random.NextBytes(_array1);
    Array.Copy(_array1, _array2, Size); // Same content
    
    random.NextBytes(_differentArray); // Different content
  }

  [Benchmark]
  public bool CompareEqual() {
    return BlockComparer.IsEqual(_array1!, Size, _array2!, Size);
  }

  [Benchmark]
  public bool CompareDifferent() {
    return BlockComparer.IsEqual(_array1!, Size, _differentArray!, Size);
  }

  [Benchmark]
  public bool CompareEqualBuiltIn() {
    return _array1!.AsSpan(0, Size).SequenceEqual(_array2!.AsSpan(0, Size));
  }
}

[TestFixture]
[Category("Performance")]
public class ManualPerformanceTests {
  
  [Test]
  public void BufferPool_Performance_RentReturn() {
    // Arrange
    var pool = new BufferPool(64 * 1024, 10);
    const int iterations = 10000;
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    for (int i = 0; i < iterations; i++) {
      using var rented = pool.Use();
      var buffer = rented.Buffer;
      // Simulate minimal work
      buffer[0] = (byte)(i % 256);
    }

    stopwatch.Stop();

    // Assert
    var avgMicroseconds = (double)stopwatch.ElapsedTicks / iterations * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"Average time per rent/return: {avgMicroseconds:F2} microseconds");
    Assert.That(avgMicroseconds, Is.LessThan(100), "Each rent/return should be very fast");
  }

  [Test]
  public void BlockComparer_Performance_LargeArrays() {
    // Arrange
    const int size = 10 * 1024 * 1024; // 10MB
    var array1 = new byte[size];
    var array2 = new byte[size];
    
    var random = new Random(42);
    random.NextBytes(array1);
    Array.Copy(array1, array2, size);

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    var result = BlockComparer.IsEqual(array1, size, array2, size);

    stopwatch.Stop();

    // Assert
    Assert.That(result, Is.True);
    var mbPerSecond = (double)size / 1024 / 1024 / stopwatch.Elapsed.TotalSeconds;
    Console.WriteLine($"Comparison speed: {mbPerSecond:F2} MB/s");
    Assert.That(mbPerSecond, Is.GreaterThan(100), "Should compare at least 100 MB/s");
  }

  [Test]
  public void BlockComparer_Performance_EarlyDifference() {
    // Arrange
    const int size = 1024 * 1024; // 1MB
    var array1 = new byte[size];
    var array2 = new byte[size];
    
    // Make arrays identical except for the first byte
    Array.Fill(array1, (byte)0xFF);
    Array.Fill(array2, (byte)0xFF);
    array2[0] = 0xFE; // Different first byte

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    var result = BlockComparer.IsEqual(array1, size, array2, size);

    stopwatch.Stop();

    // Assert
    Assert.That(result, Is.False);
    Assert.That(stopwatch.Elapsed.TotalMicroseconds, Is.LessThan(1000), "Should detect difference quickly");
  }

  [Test]
  public void Configuration_Performance_CommandLineParsing() {
    // Arrange
    var config = new Configuration();
    var switches = new[] { "-v", "-t=4", "-m=1024", "-M=1048576", "-s", "-D", "-ro" };
    const int iterations = 10000;

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    for (int i = 0; i < iterations; i++) {
      var tempConfig = new Configuration();
      CLI.ProcessCommandLine(switches, tempConfig);
    }

    stopwatch.Stop();

    // Assert
    var avgMicroseconds = (double)stopwatch.ElapsedTicks / iterations * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"Average command line parsing time: {avgMicroseconds:F2} microseconds");
    Assert.That(avgMicroseconds, Is.LessThan(100), "Command line parsing should be fast");
  }

  [Test]
  public void RuntimeStats_Performance_Increments() {
    // Arrange
    var stats = new RuntimeStats();
    const int iterations = 1000000;

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    for (int i = 0; i < iterations; i++) {
      stats.IncrementFiles(1);
      stats.IncrementBytes(1024);
      if (i % 100 == 0) {
        stats.IncrementFolders(1);
      }
    }

    stopwatch.Stop();

    // Assert
    var avgNanoseconds = (double)stopwatch.ElapsedTicks / iterations * 1_000_000_000 / System.Diagnostics.Stopwatch.Frequency;
    Console.WriteLine($"Average stats increment time: {avgNanoseconds:F2} nanoseconds");
    Assert.That(avgNanoseconds, Is.LessThan(1000), "Stats increments should be very fast");
    
    Assert.That(stats.FileCount, Is.EqualTo(iterations));
    Assert.That(stats.BytesTotal, Is.EqualTo(iterations * 1024));
    Assert.That(stats.FolderCount, Is.EqualTo(iterations / 100));
  }
}