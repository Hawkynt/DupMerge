using Classes;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ConfigurationTests {

  [Test]
  public void Constructor_SetsDefaultValues() {
    // Act
    var config = new Configuration();

    // Assert
    Assert.That(config.MinimumFileSizeInBytes, Is.EqualTo(1));
    Assert.That(config.MaximumFileSizeInBytes, Is.EqualTo(long.MaxValue));
    Assert.That(config.AlsoTrySymbolicLinks, Is.False);
    Assert.That(config.SetReadOnlyAttributeOnNewHardLinks, Is.False);
    Assert.That(config.SetReadOnlyAttributeOnNewSymbolicLinks, Is.False);
    Assert.That(config.SetReadOnlyAttributeOnExistingHardLinks, Is.False);
    Assert.That(config.SetReadOnlyAttributeOnExistingSymbolicLinks, Is.False);
    Assert.That(config.RemoveHardLinks, Is.False);
    Assert.That(config.DeleteHardLinkedFiles, Is.False);
    Assert.That(config.RemoveSymbolicLinks, Is.False);
    Assert.That(config.DeleteSymbolicLinkedFiles, Is.False);
    Assert.That(config.ShowInfoOnly, Is.False);
  }

  [Test]
  public void MaximumCrawlerThreads_DefaultValue_IsCorrect() {
    // Act
    var config = new Configuration();

    // Assert
    var expectedThreads = Math.Min(Environment.ProcessorCount, 8);
    Assert.That(config.MaximumCrawlerThreads, Is.EqualTo(expectedThreads));
  }

  [Test]
  public void MinimumFileSizeInBytes_CanBeSet() {
    // Arrange
    var config = new Configuration();
    const long newValue = 1024;

    // Act
    config.MinimumFileSizeInBytes = newValue;

    // Assert
    Assert.That(config.MinimumFileSizeInBytes, Is.EqualTo(newValue));
  }

  [Test]
  public void MaximumFileSizeInBytes_CanBeSet() {
    // Arrange
    var config = new Configuration();
    const long newValue = 1048576;

    // Act
    config.MaximumFileSizeInBytes = newValue;

    // Assert
    Assert.That(config.MaximumFileSizeInBytes, Is.EqualTo(newValue));
  }

  [Test]
  public void AlsoTrySymbolicLinks_CanBeSet() {
    // Arrange
    var config = new Configuration();

    // Act
    config.AlsoTrySymbolicLinks = true;

    // Assert
    Assert.That(config.AlsoTrySymbolicLinks, Is.True);
  }

  [Test]
  public void ReadOnlyAttributes_CanBeSetIndividually() {
    // Arrange
    var config = new Configuration();

    // Act
    config.SetReadOnlyAttributeOnNewHardLinks = true;
    config.SetReadOnlyAttributeOnNewSymbolicLinks = true;
    config.SetReadOnlyAttributeOnExistingHardLinks = true;
    config.SetReadOnlyAttributeOnExistingSymbolicLinks = true;

    // Assert
    Assert.That(config.SetReadOnlyAttributeOnNewHardLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnNewSymbolicLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnExistingHardLinks, Is.True);
    Assert.That(config.SetReadOnlyAttributeOnExistingSymbolicLinks, Is.True);
  }

  [Test]
  public void RemovalFlags_CanBeSetIndividually() {
    // Arrange
    var config = new Configuration();

    // Act
    config.RemoveHardLinks = true;
    config.RemoveSymbolicLinks = true;

    // Assert
    Assert.That(config.RemoveHardLinks, Is.True);
    Assert.That(config.RemoveSymbolicLinks, Is.True);
  }

  [Test]
  public void DeletionFlags_CanBeSetIndividually() {
    // Arrange
    var config = new Configuration();

    // Act
    config.DeleteHardLinkedFiles = true;
    config.DeleteSymbolicLinkedFiles = true;

    // Assert
    Assert.That(config.DeleteHardLinkedFiles, Is.True);
    Assert.That(config.DeleteSymbolicLinkedFiles, Is.True);
  }

  [Test]
  public void ShowInfoOnly_CanBeSet() {
    // Arrange
    var config = new Configuration();

    // Act
    config.ShowInfoOnly = true;

    // Assert
    Assert.That(config.ShowInfoOnly, Is.True);
  }

  [Test]
  public void MaximumCrawlerThreads_CanBeSet() {
    // Arrange
    var config = new Configuration();
    const int newValue = 16;

    // Act
    config.MaximumCrawlerThreads = newValue;

    // Assert
    Assert.That(config.MaximumCrawlerThreads, Is.EqualTo(newValue));
  }
}