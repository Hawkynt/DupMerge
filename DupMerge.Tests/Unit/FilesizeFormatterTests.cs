using Libraries;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class FilesizeFormatterTests {

  [Test]
  public void FormatIEC_WithZeroBytes_ReturnsZeroByte() {
    // Act
    var result = FilesizeFormatter.FormatIEC(0);

    // Assert
    Assert.That(result, Is.EqualTo("0 Byte"));
  }

  [Test]
  public void FormatIEC_WithSmallBytes_ReturnsBytes() {
    // Act
    var result = FilesizeFormatter.FormatIEC(512);

    // Assert
    Assert.That(result, Is.EqualTo("512 Bytes"));
  }

  [Test]
  public void FormatIEC_WithKilobytes_ReturnsKiB() {
    // Act
    var result = FilesizeFormatter.FormatIEC(1025);  // Must be > 1024 to trigger KiB

    // Assert
    Assert.That(result, Does.StartWith("1").And.EndsWith(" KiB"));
  }

  [Test]
  public void FormatIEC_WithMegabytes_ReturnsMiB() {
    // Act
    var result = FilesizeFormatter.FormatIEC(1024 * 1024 + 1);  // Must be > MiB to trigger MiB

    // Assert
    Assert.That(result, Does.StartWith("1").And.EndsWith(" MiB"));
  }

  [Test]
  public void FormatIEC_WithGigabytes_ReturnsGiB() {
    // Act
    var result = FilesizeFormatter.FormatIEC(1024L * 1024 * 1024 + 1);  // Must be > GiB to trigger GiB

    // Assert
    Assert.That(result, Does.StartWith("1").And.EndsWith(" GiB"));
  }

  [Test]
  public void FormatIEC_WithTerabytes_ReturnsTiB() {
    // Act - Using more than 1 TiB in bytes  
    var result = FilesizeFormatter.FormatIEC(1099511627776L + 1); // 1024^4 + 1

    // Assert
    Assert.That(result, Does.StartWith("1").And.EndsWith(" TiB"));
  }

  [Test]
  public void FormatIEC_WithCustomFormat_AppliesFormat() {
    // Act
    var result = FilesizeFormatter.FormatIEC(1536, "F2"); // 1.5 KiB

    // Assert - Allow for culture-specific decimal separator
    Assert.That(result, Does.Match(@"1[.,]50 KiB"));
  }

  [Test]
  public void FormatIEC_WithFractionalKilobytes_ShowsDecimals() {
    // Act
    var result = FilesizeFormatter.FormatIEC(1536, "F1"); // 1.5 KiB

    // Assert - Allow for culture-specific decimal separator
    Assert.That(result, Does.Match(@"1[.,]5 KiB"));
  }

  [Test]
  public void FormatIEC_WithLargeNumber_HandlesCorrectly() {
    // Act
    var result = FilesizeFormatter.FormatIEC(5L * 1024 * 1024 * 1024); // 5 GiB

    // Assert
    Assert.That(result, Is.EqualTo("5 GiB"));
  }

  [Test]
  public void FormatUnit_WithSmallNumber_ReturnsAsIs() {
    // Act
    var result = FilesizeFormatter.FormatUnit(42);

    // Assert
    Assert.That(result, Is.EqualTo("42"));
  }

  [Test]
  public void FormatUnit_WithThousands_ReturnsWithKi() {
    // Act
    var result = FilesizeFormatter.FormatUnit(5000);

    // Assert
    Assert.That(result, Is.EqualTo("5ki"));
  }

  [Test]
  public void FormatUnit_WithMillions_ReturnsWithMi() {
    // Act
    var result = FilesizeFormatter.FormatUnit(3000000);

    // Assert
    Assert.That(result, Is.EqualTo("3Mi"));
  }

  [Test]
  public void FormatUnit_WithBillions_ReturnsWithGi() {
    // Act
    var result = FilesizeFormatter.FormatUnit(2000000000);

    // Assert
    Assert.That(result, Is.EqualTo("2Gi"));
  }

  [Test]
  public void FormatUnit_WithSiPrefixesTrue_UsesSiUnits() {
    // Act
    var result = FilesizeFormatter.FormatUnit(1500, true);

    // Assert
    Assert.That(result, Is.EqualTo("2k")); // 1500/1000 = 1.5, rounded to 2k
  }

  [Test]
  public void FormatUnit_WithSiPrefixesFalse_UsesBinaryUnits() {
    // Act
    var result = FilesizeFormatter.FormatUnit(1500, false);

    // Assert
    Assert.That(result, Is.EqualTo("1ki")); // 1500/1024 = 1.46, rounded to 1ki
  }

  [Test]
  public void FormatUnit_WithZero_ReturnsZero() {
    // Act
    var result = FilesizeFormatter.FormatUnit(0);

    // Assert
    Assert.That(result, Is.EqualTo("0"));
  }

  [Test]
  public void FormatUnit_WithNegativeNumber_HandlesCorrectly() {
    // Act
    var result = FilesizeFormatter.FormatUnit(-1000);

    // Assert
    Assert.That(result, Is.EqualTo("-1000")); // Negative numbers don't get scaled
  }

  [Test]
  public void FormatIEC_WithNegativeNumber_HandlesCorrectly() {
    // Act
    var result = FilesizeFormatter.FormatIEC(-1024);

    // Assert
    Assert.That(result, Is.EqualTo("-1024 Byte")); // Negative numbers don't scale up to KiB
  }

  [Test]
  public void FormatIEC_WithVeryLargeNumber_ReturnsAppropriateUnit() {
    // Arrange - Using more than 1 PiB in bytes
    var petabyte = 1125899906842624L + 1; // 1024^5 + 1

    // Act
    var result = FilesizeFormatter.FormatIEC(petabyte);

    // Assert
    Assert.That(result, Does.StartWith("1").And.EndsWith(" PiB"));
  }

  [Test]
  public void FormatUnit_WithVeryLargeNumber_ReturnsAppropriateUnit() {
    // Act
    var result = FilesizeFormatter.FormatUnit(1099511627776L); // 1 TiB in bytes

    // Assert
    Assert.That(result, Is.EqualTo("1Ti")); // Should be 1 Ti (binary)
  }

  [Test]
  public void FormatIEC_EdgeCases_HandledCorrectly() {
    // Test edge cases around unit boundaries
    Assert.That(FilesizeFormatter.FormatIEC(1023), Is.EqualTo("1023 Bytes"));
    Assert.That(FilesizeFormatter.FormatIEC(1024), Is.EqualTo("1024 Bytes")); // Exactly 1024 is still bytes
    Assert.That(FilesizeFormatter.FormatIEC(1025), Does.StartWith("1").And.EndsWith(" KiB"));
    Assert.That(FilesizeFormatter.FormatIEC(1024 * 1023), Does.StartWith("1023").And.EndsWith(" KiB"));
  }

  [Test]
  public void FormatUnit_WithSiAndBinaryComparison() {
    // Arrange
    const double size = 1000000; // 1 million

    // Act
    var siResult = FilesizeFormatter.FormatUnit(size, true);   // SI prefixes (1000-based)
    var binaryResult = FilesizeFormatter.FormatUnit(size, false); // Binary prefixes (1024-based)

    // Assert
    Assert.That(siResult, Is.EqualTo("1M"));  // 1000000/1000/1000 = 1M
    Assert.That(binaryResult, Is.EqualTo("977ki")); // ~1000000/1024 = ~977ki (observed actual output)
  }

  [Test]
  public void FormatUnit_WithCustomFormat() {
    // Act
    var result = FilesizeFormatter.FormatUnit(1536, false, 1, "F2");

    // Assert
    Assert.That(result, Does.EndWith("ki")); // Should show binary prefix
  }

  [Test]
  public void FormatUnit_DefaultParameters() {
    // Act - Test default parameter behavior
    var result1 = FilesizeFormatter.FormatUnit(2048);
    var result2 = FilesizeFormatter.FormatUnit(2048, false);
    var result3 = FilesizeFormatter.FormatUnit(2048, false, 1);
    var result4 = FilesizeFormatter.FormatUnit(2048, false, 1, "0");

    // Assert - All should be equivalent
    Assert.That(result1, Is.EqualTo(result2));
    Assert.That(result2, Is.EqualTo(result3));
    Assert.That(result3, Is.EqualTo(result4));
    Assert.That(result1, Is.EqualTo("2ki"));
  }
}