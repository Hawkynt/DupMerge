using Classes;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class CLITests {
  private Configuration _configuration = null!;

  [SetUp]
  public void SetUp() {
    _configuration = new Configuration();
  }

  [Test]
  public void ProcessCommandLine_WithInfoSwitch_SetsShowInfoOnly() {
    // Arrange
    var switches = new[] { "-v" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.ShowInfoOnly, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithInfoLongSwitch_SetsShowInfoOnly() {
    // Arrange
    var switches = new[] { "--info" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.ShowInfoOnly, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithThreadsSwitch_SetsMaximumCrawlerThreads() {
    // Arrange
    var switches = new[] { "-t=4" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MaximumCrawlerThreads, Is.EqualTo(4));
  }

  [Test]
  public void ProcessCommandLine_WithThreadsLongSwitch_SetsMaximumCrawlerThreads() {
    // Arrange
    var switches = new[] { "--threads=8" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MaximumCrawlerThreads, Is.EqualTo(8));
  }

  [Test]
  public void ProcessCommandLine_WithMinimumSwitch_SetsMinimumFileSizeInBytes() {
    // Arrange
    var switches = new[] { "-m=1024" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MinimumFileSizeInBytes, Is.EqualTo(1024));
  }

  [Test]
  public void ProcessCommandLine_WithMinimumLongSwitch_SetsMinimumFileSizeInBytes() {
    // Arrange
    var switches = new[] { "--minimum=2048" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MinimumFileSizeInBytes, Is.EqualTo(2048));
  }

  [Test]
  public void ProcessCommandLine_WithMaximumSwitch_SetsMaximumFileSizeInBytes() {
    // Arrange
    var switches = new[] { "-M=1048576" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MaximumFileSizeInBytes, Is.EqualTo(1048576));
  }

  [Test]
  public void ProcessCommandLine_WithMaximumLongSwitch_SetsMaximumFileSizeInBytes() {
    // Arrange
    var switches = new[] { "--maximum=2097152" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MaximumFileSizeInBytes, Is.EqualTo(2097152));
  }

  [Test]
  public void ProcessCommandLine_WithAllowSymlinkSwitch_SetsAlsoTrySymbolicLinks() {
    // Arrange
    var switches = new[] { "-s" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.AlsoTrySymbolicLinks, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithAllowSymlinkLongSwitch_SetsAlsoTrySymbolicLinks() {
    // Arrange
    var switches = new[] { "--allow-symlink" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.AlsoTrySymbolicLinks, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithDeleteSwitch_SetsBothDeleteFlags() {
    // Arrange
    var switches = new[] { "-D" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.DeleteHardLinkedFiles, Is.True);
    Assert.That(_configuration.DeleteSymbolicLinkedFiles, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithDeleteLongSwitch_SetsBothDeleteFlags() {
    // Arrange
    var switches = new[] { "--delete" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.DeleteHardLinkedFiles, Is.True);
    Assert.That(_configuration.DeleteSymbolicLinkedFiles, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithDeleteHardLinksSwitch_SetsDeleteHardLinkedFiles() {
    // Arrange
    var switches = new[] { "-Dhl" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.DeleteHardLinkedFiles, Is.True);
    Assert.That(_configuration.DeleteSymbolicLinkedFiles, Is.False);
  }

  [Test]
  public void ProcessCommandLine_WithDeleteSymLinksSwitch_SetsDeleteSymbolicLinkedFiles() {
    // Arrange
    var switches = new[] { "-Dsl" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.DeleteSymbolicLinkedFiles, Is.True);
    Assert.That(_configuration.DeleteHardLinkedFiles, Is.False);
  }

  [Test]
  public void ProcessCommandLine_WithRemoveSwitch_SetsBothRemoveFlags() {
    // Arrange
    var switches = new[] { "-R" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.RemoveHardLinks, Is.True);
    Assert.That(_configuration.RemoveSymbolicLinks, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithReadOnlySwitch_SetsAllReadOnlyFlags() {
    // Arrange
    var switches = new[] { "-ro" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.SetReadOnlyAttributeOnNewHardLinks, Is.True);
    Assert.That(_configuration.SetReadOnlyAttributeOnNewSymbolicLinks, Is.True);
    Assert.That(_configuration.SetReadOnlyAttributeOnExistingHardLinks, Is.True);
    Assert.That(_configuration.SetReadOnlyAttributeOnExistingSymbolicLinks, Is.True);
  }

  [Test]
  public void ProcessCommandLine_WithMultipleSwitches_SetsAllFlags() {
    // Arrange
    var switches = new[] { "-v", "-s", "-t=2", "-m=512" };

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.ShowInfoOnly, Is.True);
    Assert.That(_configuration.AlsoTrySymbolicLinks, Is.True);
    Assert.That(_configuration.MaximumCrawlerThreads, Is.EqualTo(2));
    Assert.That(_configuration.MinimumFileSizeInBytes, Is.EqualTo(512));
  }

  [Test]
  public void ProcessCommandLine_WithInvalidThreadsValue_DoesNotSetMaximumCrawlerThreads() {
    // Arrange
    var switches = new[] { "-t=invalid" };
    var originalValue = _configuration.MaximumCrawlerThreads;

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MaximumCrawlerThreads, Is.EqualTo(originalValue));
  }

  [Test]
  public void ProcessCommandLine_WithInvalidMinimumValue_DoesNotSetMinimumFileSizeInBytes() {
    // Arrange
    var switches = new[] { "-m=invalid" };
    var originalValue = _configuration.MinimumFileSizeInBytes;

    // Act
    CLI.ProcessCommandLine(switches, _configuration);

    // Assert
    Assert.That(_configuration.MinimumFileSizeInBytes, Is.EqualTo(originalValue));
  }

  [Test]
  public void ProcessCommandLine_WithUnknownSwitch_DoesNotThrow() {
    // Arrange
    var switches = new[] { "--unknown-switch" };

    // Act & Assert
    Assert.DoesNotThrow(() => CLI.ProcessCommandLine(switches, _configuration));
  }
}