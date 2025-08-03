using Classes;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class LinkStatsTests {

  [Test]
  public void Constructor_InitializesWithZeroValues() {
    // Act
    var stats = new LinkStats();

    // Assert
    Assert.That(stats.Created, Is.EqualTo(0));
    Assert.That(stats.Removed, Is.EqualTo(0));
    Assert.That(stats.Deleted, Is.EqualTo(0));
    Assert.That(stats.Seen, Is.EqualTo(0));
  }

  [Test]
  public void IncreaseCreated_IncrementsCreatedCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseCreated();

    // Assert
    Assert.That(stats.Created, Is.EqualTo(1));
  }

  [Test]
  public void IncreaseCreated_MultipleCalls_AccumulatesCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseCreated();
    stats.IncreaseCreated();
    stats.IncreaseCreated();

    // Assert
    Assert.That(stats.Created, Is.EqualTo(3));
  }

  [Test]
  public void IncreaseRemoved_IncrementsRemovedCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseRemoved();

    // Assert
    Assert.That(stats.Removed, Is.EqualTo(1));
  }

  [Test]
  public void IncreaseRemoved_MultipleCalls_AccumulatesCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseRemoved();
    stats.IncreaseRemoved();

    // Assert
    Assert.That(stats.Removed, Is.EqualTo(2));
  }

  [Test]
  public void IncreaseDeleted_IncrementsDeletedCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseDeleted();

    // Assert
    Assert.That(stats.Deleted, Is.EqualTo(1));
  }

  [Test]
  public void IncreaseDeleted_MultipleCalls_AccumulatesCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseDeleted();
    stats.IncreaseDeleted();
    stats.IncreaseDeleted();
    stats.IncreaseDeleted();

    // Assert
    Assert.That(stats.Deleted, Is.EqualTo(4));
  }

  [Test]
  public void IncreaseSeen_IncrementsSeenCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseSeen();

    // Assert
    Assert.That(stats.Seen, Is.EqualTo(1));
  }

  [Test]
  public void IncreaseSeen_MultipleCalls_AccumulatesCount() {
    // Arrange
    var stats = new LinkStats();

    // Act
    for (int i = 0; i < 10; i++) {
      stats.IncreaseSeen();
    }

    // Assert
    Assert.That(stats.Seen, Is.EqualTo(10));
  }

  [Test]
  public void AllCounters_IndependentOfEachOther() {
    // Arrange
    var stats = new LinkStats();

    // Act
    stats.IncreaseCreated();
    stats.IncreaseCreated();
    stats.IncreaseRemoved();
    stats.IncreaseDeleted();
    stats.IncreaseDeleted();
    stats.IncreaseDeleted();
    stats.IncreaseSeen();
    stats.IncreaseSeen();
    stats.IncreaseSeen();
    stats.IncreaseSeen();

    // Assert
    Assert.That(stats.Created, Is.EqualTo(2));
    Assert.That(stats.Removed, Is.EqualTo(1));
    Assert.That(stats.Deleted, Is.EqualTo(3));
    Assert.That(stats.Seen, Is.EqualTo(4));
  }

  [Test]
  public void ThreadSafety_ConcurrentIncrements_AllCountsCorrect() {
    // Arrange
    var stats = new LinkStats();
    const int iterationsPerThread = 1000;
    const int threadCount = 4;

    var tasks = new Task[threadCount];

    // Act
    for (int i = 0; i < threadCount; i++) {
      tasks[i] = Task.Run(() => {
        for (int j = 0; j < iterationsPerThread; j++) {
          stats.IncreaseCreated();
          stats.IncreaseRemoved();
          stats.IncreaseDeleted();
          stats.IncreaseSeen();
        }
      });
    }

    Task.WaitAll(tasks);

    // Assert
    var expectedCount = threadCount * iterationsPerThread;
    Assert.That(stats.Created, Is.EqualTo(expectedCount));
    Assert.That(stats.Removed, Is.EqualTo(expectedCount));
    Assert.That(stats.Deleted, Is.EqualTo(expectedCount));
    Assert.That(stats.Seen, Is.EqualTo(expectedCount));
  }
}