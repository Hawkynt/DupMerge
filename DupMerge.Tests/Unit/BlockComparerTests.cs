using Classes;

namespace DupMerge.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class BlockComparerTests {

  [Test]
  public void IsEqual_WithSameArrayReference_ReturnsTrue() {
    // Arrange
    var array = new byte[] { 1, 2, 3, 4, 5 };

    // Act
    var result = BlockComparer.IsEqual(array, array.Length, array, array.Length);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithDifferentLengths_ReturnsFalse() {
    // Arrange
    var array1 = new byte[] { 1, 2, 3 };
    var array2 = new byte[] { 1, 2, 3, 4 };

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.False);
  }

  [Test]
  public void IsEqual_WithEqualArrays_ReturnsTrue() {
    // Arrange
    var array1 = new byte[] { 1, 2, 3, 4, 5 };
    var array2 = new byte[] { 1, 2, 3, 4, 5 };

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithDifferentArrays_ReturnsFalse() {
    // Arrange
    var array1 = new byte[] { 1, 2, 3, 4, 5 };
    var array2 = new byte[] { 1, 2, 3, 4, 6 };

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.False);
  }

  [Test]
  public void IsEqual_WithEmptyArrays_ReturnsTrue() {
    // Arrange
    var array1 = Array.Empty<byte>();
    var array2 = Array.Empty<byte>();

    // Act
    var result = BlockComparer.IsEqual(array1, 0, array2, 0);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithPartialLength_ComparesOnlySpecifiedBytes() {
    // Arrange
    var array1 = new byte[] { 1, 2, 3, 4, 5 };
    var array2 = new byte[] { 1, 2, 3, 9, 9 };

    // Act
    var result = BlockComparer.IsEqual(array1, 3, array2, 3);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithLargeArrays_PerformsCorrectly() {
    // Arrange
    const int size = 10000;
    var array1 = new byte[size];
    var array2 = new byte[size];
    
    // Fill with same pattern
    for (int i = 0; i < size; i++) {
      array1[i] = array2[i] = (byte)(i % 256);
    }

    // Act
    var result = BlockComparer.IsEqual(array1, size, array2, size);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithLargeArraysDifferentAtEnd_ReturnsFalse() {
    // Arrange
    const int size = 10000;
    var array1 = new byte[size];
    var array2 = new byte[size];
    
    // Fill with same pattern
    for (int i = 0; i < size; i++) {
      array1[i] = array2[i] = (byte)(i % 256);
    }
    
    // Make them different at the end
    array2[size - 1] = (byte)(array2[size - 1] + 1);

    // Act
    var result = BlockComparer.IsEqual(array1, size, array2, size);

    // Assert
    Assert.That(result, Is.False);
  }

  [Test]
  public void IsEqual_WithUnalignedSizes_PerformsCorrectly() {
    // Arrange - test with sizes that don't align to 8-byte boundaries
    var array1 = new byte[] { 1, 2, 3, 4, 5, 6, 7 }; // 7 bytes
    var array2 = new byte[] { 1, 2, 3, 4, 5, 6, 7 };

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.True);
  }

  [Test]
  public void IsEqual_WithUnalignedSizesDifferent_ReturnsFalse() {
    // Arrange - test with sizes that don't align to 8-byte boundaries
    var array1 = new byte[] { 1, 2, 3, 4, 5, 6, 7 }; // 7 bytes
    var array2 = new byte[] { 1, 2, 3, 4, 5, 6, 8 };

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.False);
  }

  [Test]
  public void IsEqual_Performance_CompletesQuickly() {
    // Arrange
    const int size = 1_000_000; // 1MB
    var array1 = new byte[size];
    var array2 = new byte[size];
    
    // Fill with random data
    var random = new Random(42);
    random.NextBytes(array1);
    Array.Copy(array1, array2, size);

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Act
    var result = BlockComparer.IsEqual(array1, size, array2, size);

    // Assert
    stopwatch.Stop();
    Assert.That(result, Is.True);
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100), "Comparison should be fast");
  }

  [Test]
  public void IsEqual_WithMultipleOf8ByteArrays_WorksCorrectly() {
    // Arrange - test with sizes that are multiples of 8 to test the optimized path
    var array1 = new byte[64]; // 8 * 8 bytes
    var array2 = new byte[64];
    
    for (int i = 0; i < 64; i++) {
      array1[i] = array2[i] = (byte)(i % 256);
    }

    // Act
    var result = BlockComparer.IsEqual(array1, array1.Length, array2, array2.Length);

    // Assert
    Assert.That(result, Is.True);
  }
}