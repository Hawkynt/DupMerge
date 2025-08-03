using Classes;
using System.IO.Abstractions.TestingHelpers;

namespace DupMerge.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class FileSystemIntegrationTests {
  private string? _tempDirectory;

  [SetUp]
  public void SetUp() {
    _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(_tempDirectory);
  }

  [TearDown]
  public void TearDown() {
    if (_tempDirectory != null && Directory.Exists(_tempDirectory)) {
      Directory.Delete(_tempDirectory, true);
    }
  }

  [Test]
  public void BufferPool_Integration_WithFileOperations() {
    // Arrange
    var pool = new BufferPool(1024, 5);
    var testFile = Path.Combine(_tempDirectory!, "test.txt");
    var testData = new byte[2048];
    new Random(42).NextBytes(testData);
    File.WriteAllBytes(testFile, testData);

    // Act
    byte[] readData;
    using (var rented = pool.Use()) {
      using var stream = File.OpenRead(testFile);
      readData = new byte[testData.Length];
      
      int totalRead = 0;
      int bytesRead;
      while (totalRead < readData.Length && (bytesRead = stream.Read(rented.Buffer, 0, Math.Min(rented.Length, readData.Length - totalRead))) > 0) {
        Array.Copy(rented.Buffer, 0, readData, totalRead, bytesRead);
        totalRead += bytesRead;
      }
    }

    // Assert
    Assert.That(readData, Is.EqualTo(testData));
  }

  [Test]
  public void Configuration_Integration_WithActualSettings() {
    // Arrange
    var config = new Configuration();
    var switches = new[] { "-t=2", "-m=1024", "-M=1048576", "-v", "-s" };

    // Act
    CLI.ProcessCommandLine(switches, config);

    // Assert
    Assert.That(config.MaximumCrawlerThreads, Is.EqualTo(2));
    Assert.That(config.MinimumFileSizeInBytes, Is.EqualTo(1024));
    Assert.That(config.MaximumFileSizeInBytes, Is.EqualTo(1048576));
    Assert.That(config.ShowInfoOnly, Is.True);
    Assert.That(config.AlsoTrySymbolicLinks, Is.True);
  }

  [Test]
  public void BlockComparer_Integration_WithActualFileData() {
    // Arrange
    var file1 = Path.Combine(_tempDirectory!, "file1.txt");
    var file2 = Path.Combine(_tempDirectory!, "file2.txt");
    var file3 = Path.Combine(_tempDirectory!, "file3.txt");
    
    var testData = new byte[8192];
    new Random(42).NextBytes(testData);
    
    File.WriteAllBytes(file1, testData);
    File.WriteAllBytes(file2, testData); // Same content
    
    var differentData = new byte[8192];
    new Random(123).NextBytes(differentData);
    File.WriteAllBytes(file3, differentData); // Different content

    var data1 = File.ReadAllBytes(file1);
    var data2 = File.ReadAllBytes(file2);
    var data3 = File.ReadAllBytes(file3);

    // Act & Assert
    Assert.That(BlockComparer.IsEqual(data1, data1.Length, data2, data2.Length), Is.True);
    Assert.That(BlockComparer.IsEqual(data1, data1.Length, data3, data3.Length), Is.False);
  }

  [Test]
  public void RuntimeStats_Integration_WithMultipleOperations() {
    // Arrange
    var stats = new RuntimeStats();

    // Act - Simulate file processing operations
    stats.IncrementFolders(3);
    stats.IncrementFiles(15);
    stats.IncrementBytes(1024 * 1024); // 1MB

    stats.HardLinkStats.IncreaseCreated();
    stats.HardLinkStats.IncreaseCreated();
    stats.SymbolicLinkStats.IncreaseSeen();

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(3));
    Assert.That(stats.FileCount, Is.EqualTo(15));
    Assert.That(stats.BytesTotal, Is.EqualTo(1048576));
    Assert.That(stats.HardLinkStats.Created, Is.EqualTo(2));
    Assert.That(stats.SymbolicLinkStats.Seen, Is.EqualTo(1));
  }

  [Test]
  public void FileSystem_Integration_CreateAndDetectDuplicates() {
    // Arrange
    var content = "This is test content for duplicate detection";
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    
    var file1 = Path.Combine(_tempDirectory!, "duplicate1.txt");
    var file2 = Path.Combine(_tempDirectory!, "duplicate2.txt");
    var file3 = Path.Combine(_tempDirectory!, "different.txt");
    
    File.WriteAllBytes(file1, bytes);
    File.WriteAllBytes(file2, bytes); // Same content
    File.WriteAllBytes(file3, System.Text.Encoding.UTF8.GetBytes("Different content"));

    // Act
    var data1 = File.ReadAllBytes(file1);
    var data2 = File.ReadAllBytes(file2);
    var data3 = File.ReadAllBytes(file3);

    // Assert
    Assert.That(data1.Length, Is.EqualTo(data2.Length));
    Assert.That(BlockComparer.IsEqual(data1, data1.Length, data2, data2.Length), Is.True);
    Assert.That(BlockComparer.IsEqual(data1, data1.Length, data3, data3.Length), Is.False);
  }

  [Test]
  public void FileSystem_Integration_LargeFiles() {
    // Arrange
    var largeContent = new byte[1024 * 1024]; // 1MB
    new Random(42).NextBytes(largeContent);
    
    var file1 = Path.Combine(_tempDirectory!, "large1.bin");
    var file2 = Path.Combine(_tempDirectory!, "large2.bin");
    
    File.WriteAllBytes(file1, largeContent);
    File.WriteAllBytes(file2, largeContent);

    // Act
    var data1 = File.ReadAllBytes(file1);
    var data2 = File.ReadAllBytes(file2);

    // Assert
    Assert.That(data1.Length, Is.EqualTo(1024 * 1024));
    Assert.That(data2.Length, Is.EqualTo(1024 * 1024));
    Assert.That(BlockComparer.IsEqual(data1, data1.Length, data2, data2.Length), Is.True);
  }

  [Test]
  public void DirectoryStructure_Integration_MultipleDirectories() {
    // Arrange
    var subDir1 = Path.Combine(_tempDirectory!, "subdir1");
    var subDir2 = Path.Combine(_tempDirectory!, "subdir2");
    Directory.CreateDirectory(subDir1);
    Directory.CreateDirectory(subDir2);
    
    var content = System.Text.Encoding.UTF8.GetBytes("Shared content");
    File.WriteAllBytes(Path.Combine(subDir1, "file1.txt"), content);
    File.WriteAllBytes(Path.Combine(subDir2, "file2.txt"), content);

    // Act
    var directories = new[] {
      new DirectoryInfo(subDir1),
      new DirectoryInfo(subDir2)
    };

    // Assert
    Assert.That(directories[0].Exists, Is.True);
    Assert.That(directories[1].Exists, Is.True);
    Assert.That(directories[0].GetFiles().Length, Is.EqualTo(1));
    Assert.That(directories[1].GetFiles().Length, Is.EqualTo(1));
  }
}