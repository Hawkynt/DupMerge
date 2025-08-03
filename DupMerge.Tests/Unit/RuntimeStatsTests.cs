using Classes;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class RuntimeStatsTests {

  [Test]
  public void Constructor_InitializesWithZeroValues() {
    // Act
    var stats = new RuntimeStats();

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(0));
    Assert.That(stats.FileCount, Is.EqualTo(0));
    Assert.That(stats.BytesTotal, Is.EqualTo(0));
    Assert.That(stats.HardLinkStats, Is.Not.Null);
    Assert.That(stats.SymbolicLinkStats, Is.Not.Null);
  }

  [Test]
  public void IncrementFolders_IncreasesFolderCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFolders(5);

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(5));
  }

  [Test]
  public void IncrementFolders_MultipleCalls_AccumulatesFolderCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFolders(3);
    stats.IncrementFolders(2);

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(5));
  }

  [Test]
  public void IncrementFiles_IncreasesFileCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFiles(10);

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(10));
  }

  [Test]
  public void IncrementFiles_MultipleCalls_AccumulatesFileCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFiles(7);
    stats.IncrementFiles(3);

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(10));
  }

  [Test]
  public void IncrementBytes_IncreasesBytesTotal() {
    // Arrange
    var stats = new RuntimeStats();
    const long bytesToAdd = 1024;

    // Act
    stats.IncrementBytes(bytesToAdd);

    // Assert
    Assert.That(stats.BytesTotal, Is.EqualTo(bytesToAdd));
  }

  [Test]
  public void IncrementBytes_MultipleCalls_AccumulatesBytesTotal() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementBytes(512);
    stats.IncrementBytes(256);

    // Assert
    Assert.That(stats.BytesTotal, Is.EqualTo(768));
  }

  [Test]
  public void HardLinkStats_ReturnsValidLinkStats() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    var hardLinkStats = stats.HardLinkStats;

    // Assert
    Assert.That(hardLinkStats, Is.Not.Null);
    Assert.That(hardLinkStats.Created, Is.EqualTo(0));
    Assert.That(hardLinkStats.Removed, Is.EqualTo(0));
    Assert.That(hardLinkStats.Deleted, Is.EqualTo(0));
    Assert.That(hardLinkStats.Seen, Is.EqualTo(0));
  }

  [Test]
  public void SymbolicLinkStats_ReturnsValidLinkStats() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    var symbolicLinkStats = stats.SymbolicLinkStats;

    // Assert
    Assert.That(symbolicLinkStats, Is.Not.Null);
    Assert.That(symbolicLinkStats.Created, Is.EqualTo(0));
    Assert.That(symbolicLinkStats.Removed, Is.EqualTo(0));
    Assert.That(symbolicLinkStats.Deleted, Is.EqualTo(0));
    Assert.That(symbolicLinkStats.Seen, Is.EqualTo(0));
  }

  [Test]
  public void IncrementFolders_WithZero_DoesNotChangeFolderCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFolders(0);

    // Assert
    Assert.That(stats.FolderCount, Is.EqualTo(0));
  }

  [Test]
  public void IncrementFiles_WithZero_DoesNotChangeFileCount() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementFiles(0);

    // Assert
    Assert.That(stats.FileCount, Is.EqualTo(0));
  }

  [Test]
  public void IncrementBytes_WithZero_DoesNotChangeBytesTotal() {
    // Arrange
    var stats = new RuntimeStats();

    // Act
    stats.IncrementBytes(0);

    // Assert
    Assert.That(stats.BytesTotal, Is.EqualTo(0));
  }
}