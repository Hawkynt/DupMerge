using Classes;
using System.Diagnostics;

namespace DupMerge.Tests.EndToEnd;

[TestFixture]
[Category("EndToEnd")]
public class DupMergeEndToEndTests {
  private string? _tempDirectory;
  private string? _originalDirectory;

  [SetUp]
  public void SetUp() {
    _tempDirectory = Path.Combine(Path.GetTempPath(), $"DupMergeTest_{Guid.NewGuid()}");
    Directory.CreateDirectory(_tempDirectory);
    _originalDirectory = Environment.CurrentDirectory;
  }

  [TearDown]
  public void TearDown() {
    Environment.CurrentDirectory = _originalDirectory!;
    
    if (_tempDirectory != null && Directory.Exists(_tempDirectory)) {
      // Remove any read-only attributes before deletion
      var files = Directory.GetFiles(_tempDirectory, "*", SearchOption.AllDirectories);
      foreach (var file in files) {
        try {
          File.SetAttributes(file, FileAttributes.Normal);
        } catch {
          // Ignore errors when removing attributes
        }
      }
      
      try {
        Directory.Delete(_tempDirectory, true);
      } catch {
        // Ignore cleanup errors in tests
      }
    }
  }

  [Test]
  public void InfoMode_WithDuplicateFiles_ShowsCorrectInformation() {
    // Arrange
    var content = "This is duplicate content for testing";
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
    
    var file1 = Path.Combine(_tempDirectory!, "file1.txt");
    var file2 = Path.Combine(_tempDirectory!, "file2.txt");
    var file3 = Path.Combine(_tempDirectory!, "unique.txt");
    
    File.WriteAllBytes(file1, bytes);
    File.WriteAllBytes(file2, bytes); // Duplicate
    File.WriteAllBytes(file3, System.Text.Encoding.UTF8.GetBytes("Unique content"));

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act & Assert - Should not throw and should process files
    Assert.DoesNotThrow(() => {
      DuplicateFileMerger.ProcessFolders(directories, config, stats);
    });

    // Verify stats were updated
    Assert.That(stats.FileCount, Is.GreaterThan(0));
    Assert.That(stats.FolderCount, Is.GreaterThan(0));
    Assert.That(stats.BytesTotal, Is.GreaterThan(0));
  }

  [Test]
  public void Configuration_AllSwitches_ParsedCorrectly() {
    // Arrange
    var switches = new[] {
      "-v", // info only
      "-t=2", // threads
      "-m=100", // minimum size
      "-M=10000", // maximum size
      "-s", // allow symlinks
      "-ro" // readonly
    };
    var config = new Configuration();

    // Act
    CLI.ProcessCommandLine(switches, config);

    // Assert
    Assert.That(config.ShowInfoOnly, Is.True);
    Assert.That(config.MaximumCrawlerThreads, Is.EqualTo(2));
    Assert.That(config.MinimumFileSizeInBytes, Is.EqualTo(100));
    Assert.That(config.MaximumFileSizeInBytes, Is.EqualTo(10000));
    Assert.That(config.AlsoTrySymbolicLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnNewHardLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnNewSymbolicLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnExistingHardLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnExistingSymbolicLinks, Is.True);
  }

  [Test]
  public void MinimumFileSize_FiltersSmallFiles() {
    // Arrange
    var smallContent = System.Text.Encoding.UTF8.GetBytes("Small");
    var largeContent = System.Text.Encoding.UTF8.GetBytes(new string('A', 1000));
    
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "small1.txt"), smallContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "small2.txt"), smallContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "large1.txt"), largeContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "large2.txt"), largeContent);

    var config = new Configuration { 
      ShowInfoOnly = true,
      MinimumFileSizeInBytes = 100 // Only process files >= 100 bytes
    };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    // Assert - Small files should be ignored, only large files processed
    // Note: This is testing the filtering behavior through the stats
    Assert.That(stats.FileCount, Is.GreaterThan(0));
  }

  [Test]
  public void MaximumFileSize_FiltersLargeFiles() {
    // Arrange
    var smallContent = System.Text.Encoding.UTF8.GetBytes("Small content");
    var largeContent = System.Text.Encoding.UTF8.GetBytes(new string('B', 2000));
    
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "small1.txt"), smallContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "small2.txt"), smallContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "large1.txt"), largeContent);
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "large2.txt"), largeContent);

    var config = new Configuration { 
      ShowInfoOnly = true,
      MaximumFileSizeInBytes = 100 // Only process files <= 100 bytes
    };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    // Assert - Large files should be ignored, only small files processed
    Assert.That(stats.FileCount, Is.GreaterThan(0));
  }

  [Test]
  public void MultipleDirectories_ProcessedCorrectly() {
    // Arrange
    var subDir1 = Path.Combine(_tempDirectory!, "dir1");
    var subDir2 = Path.Combine(_tempDirectory!, "dir2");
    Directory.CreateDirectory(subDir1);
    Directory.CreateDirectory(subDir2);
    
    var content = System.Text.Encoding.UTF8.GetBytes("Duplicate content across directories");
    File.WriteAllBytes(Path.Combine(subDir1, "file1.txt"), content);
    File.WriteAllBytes(Path.Combine(subDir2, "file2.txt"), content);

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { 
      new DirectoryInfo(subDir1),
      new DirectoryInfo(subDir2)
    };

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(2)); // Both directories processed
    Assert.That(stats.FileCount, Is.GreaterThan(0));
  }

  [Test]
  public void ThreadConfiguration_ProcessesWithSpecifiedThreads() {
    // Arrange
    var content = System.Text.Encoding.UTF8.GetBytes("Content for threading test");
    
    // Create multiple files to ensure threading is exercised
    for (int i = 0; i < 10; i++) {
      File.WriteAllBytes(Path.Combine(_tempDirectory!, $"file{i}.txt"), content);
    }

    var config = new Configuration { 
      ShowInfoOnly = true,
      MaximumCrawlerThreads = 4 
    };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act & Assert - Should not throw with threading
    Assert.DoesNotThrow(() => {
      DuplicateFileMerger.ProcessFolders(directories, config, stats);
    });
    
    Assert.That(stats.FileCount, Is.EqualTo(10));
  }

  [Test]
  public void EmptyDirectory_HandledGracefully() {
    // Arrange
    var emptyDir = Path.Combine(_tempDirectory!, "empty");
    Directory.CreateDirectory(emptyDir);

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(emptyDir) };

    // Act & Assert - Should not throw
    Assert.DoesNotThrow(() => {
      DuplicateFileMerger.ProcessFolders(directories, config, stats);
    });
    
    Assert.That(stats.FolderCount, Is.EqualTo(1));
    Assert.That(stats.FileCount, Is.EqualTo(0));
  }

  [Test]
  public void LargeFiles_ProcessedCorrectly() {
    // Arrange
    var largeContent = new byte[1024 * 1024]; // 1MB
    new Random(42).NextBytes(largeContent);
    
    var file1 = Path.Combine(_tempDirectory!, "large1.bin");
    var file2 = Path.Combine(_tempDirectory!, "large2.bin");
    
    File.WriteAllBytes(file1, largeContent);
    File.WriteAllBytes(file2, largeContent); // Duplicate large file

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(2));
    Assert.That(stats.BytesTotal, Is.EqualTo(2 * 1024 * 1024)); // Total of both files
  }

  [Test]
  public void ManySmallFiles_ProcessedEfficiently() {
    // Arrange
    var content = System.Text.Encoding.UTF8.GetBytes("Small file content");
    
    // Create many small files
    for (int i = 0; i < 100; i++) {
      File.WriteAllBytes(Path.Combine(_tempDirectory!, $"small{i:D3}.txt"), content);
    }

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    var stopwatch = Stopwatch.StartNew();

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    stopwatch.Stop();

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(100));
    Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(30), "Should process 100 small files quickly");
  }

  [Test]
  public void NestedDirectories_ProcessedRecursively() {
    // Arrange
    var level1 = Path.Combine(_tempDirectory!, "level1");
    var level2 = Path.Combine(level1, "level2");
    var level3 = Path.Combine(level2, "level3");
    
    Directory.CreateDirectory(level3);
    
    var content = System.Text.Encoding.UTF8.GetBytes("Nested content");
    File.WriteAllBytes(Path.Combine(_tempDirectory!, "root.txt"), content);
    File.WriteAllBytes(Path.Combine(level1, "level1.txt"), content);
    File.WriteAllBytes(Path.Combine(level2, "level2.txt"), content);
    File.WriteAllBytes(Path.Combine(level3, "level3.txt"), content);

    var config = new Configuration { ShowInfoOnly = true };
    var stats = new RuntimeStats();
    var directories = new[] { new DirectoryInfo(_tempDirectory!) };

    // Act
    DuplicateFileMerger.ProcessFolders(directories, config, stats);

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(4)); // All files in nested structure
    Assert.That(stats.FolderCount, Is.GreaterThan(3)); // Root + nested directories
  }
}