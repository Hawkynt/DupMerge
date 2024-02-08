using System;
using System.Globalization;
using System.Linq;

namespace Libraries;

public static class FilesizeFormatter {
  // ReSharper disable MemberCanBePrivate.Global
  public const ulong KiB = 1024;
  public const ulong MiB = KiB * 1024;
  public const ulong GiB = MiB * 1024;
  public const ulong TiB = GiB * 1024;
  public const ulong PiB = TiB * 1024;
  public const ulong EiB = PiB * 1024;
  // ReSharper restore MemberCanBePrivate.Global

  public static string FormatIEC(ulong size, string format = @"0") {
    Guard.Against.ArgumentIsNull(format);

    if (size > EiB)
      return (((float)size / EiB).ToString(format) + " EiB");
    if (size > PiB)
      return (((float)size / PiB).ToString(format) + " PiB");
    if (size > TiB)
      return (((float)size / TiB).ToString(format) + " TiB");
    if (size > GiB)
      return (((float)size / GiB).ToString(format) + " GiB");
    if (size > MiB)
      return (((float)size / MiB).ToString(format) + " MiB");
    if (size > KiB)
      return (((float)size / KiB).ToString(format) + " KiB");
    if (size > 1)
      return (size.ToString(format) + " Bytes");
    return (size.ToString(format) + " Byte");
  }

  public static string FormatIEC(double size, string format = @"0") {
    Guard.Against.ArgumentIsNull(format);

    if (size > EiB)
      return (((float)size / EiB).ToString(format) + " EiB");
    if (size > PiB)
      return (((float)size / PiB).ToString(format) + " PiB");
    if (size > TiB)
      return (((float)size / TiB).ToString(format) + " TiB");
    if (size > GiB)
      return (((float)size / GiB).ToString(format) + " GiB");
    if (size > MiB)
      return (((float)size / MiB).ToString(format) + " MiB");
    if (size > KiB)
      return (((float)size / KiB).ToString(format) + " KiB");
    if (size > 1)
      return (size.ToString(format) + " Bytes");
    return (size.ToString(format) + " Byte");
  }

  /// <summary>
  /// Parses the specified size back into bytes.
  /// </summary>
  /// <param name="size">The size.</param>
  /// <param name="culture">The culture, default to <c>null</c>.</param>
  /// <returns>The parsed size in bytes.</returns>
  public static ulong Parse(string size, CultureInfo culture = null) {
    Guard.Against.ArgumentIsNull(size);

    var query = _GetFactorAndSize(size);
    size = query.Item1;
    var factor = query.Item2;
    if (factor < 1)
      throw new ArgumentException();

    var num = (culture == null ? double.Parse(size) : double.Parse(size, culture)) * factor;
    return ((ulong)num);
  }

  /// <summary>
  /// Parses the specified size back into bytes.
  /// </summary>
  /// <param name="size">The size.</param>
  /// <param name="numberStyles">The number style.</param>
  /// <param name="culture">The culture, default to <c>null</c>.</param>
  /// <returns>
  /// The parsed size in bytes.
  /// </returns>
  public static ulong Parse(string size, NumberStyles numberStyles, CultureInfo culture = null) {
    Guard.Against.ArgumentIsNull(size);

    var query = _GetFactorAndSize(size);
    size = query.Item1;
    var factor = query.Item2;
    if (factor < 1)
      throw new ArgumentException();
    var num = (culture == null ? double.Parse(size, numberStyles) : double.Parse(size, numberStyles, culture)) * factor;
    return ((ulong)num);
  }

  /// <summary>
  /// Gets the factor which is used for calculation and the remaining decimals from the input string.
  /// </summary>
  /// <param name="size">The input size.</param>
  /// <returns>A Tuple containing the remaining digits and the factor(which will be zero if it could not be identified).</returns>
  private static Tuple<string, ulong> _GetFactorAndSize(string size) {
    Guard.Against.ArgumentIsNull(size);

    var result = size.Trim().ToLowerInvariant();
    var length = result.Length;
    var factor = (ulong)0;
    var trimCount = 0;
    if (result.EndsWith("ib")) {
      trimCount = 3;
      factor = _GetFactor(size[length - 3]);
    } else if (result.EndsWith("b")) {
      var chr = size[length - 2];
      if (char.IsDigit(chr)) {
        trimCount = 1;
        factor = 1;
      } else {
        trimCount = 2;
        factor = _GetFactor(chr);
      }
    }
    result = trimCount > 0 ? result.Substring(0, length - trimCount).Trim() : result;
    return (Tuple.Create(result, factor));
  }

  /// <summary>
  /// Gets the factor from a given character.
  /// </summary>
  /// <param name="dimensionChar">The dimension identifier.</param>
  /// <returns>The factor or 0 on error.</returns>
  private static ulong _GetFactor(char dimensionChar) {
    switch (dimensionChar) {
      case 'e':
      case 'E':
      {
        return (EiB);
      }
      case 'p':
      case 'P':
      {
        return (PiB);
      }
      case 't':
      case 'T':
      {
        return (TiB);
      }
      case 'g':
      case 'G':
      {
        return (GiB);
      }
      case 'm':
      case 'M':
      {
        return (MiB);
      }
      case 'k':
      case 'K':
      {
        return (KiB);
      }
      default:
      {
        return (0);
      }
    }
  }

  public static string FormatUnit(double size, bool useSiPrefixes = false, double rolloverFactor = 1, string format = "0") {
    var factor = useSiPrefixes ? 1000 : 1024;
    var prefixes = useSiPrefixes ? new[] { "", "k", "M", "G", "T", "P", "E" } : new[] { "", "ki", "Mi", "Gi", "Ti", "Pi", "Ei" };
    foreach (var prefix in prefixes) {
      if (size < factor * rolloverFactor)
        return size.ToString(format) + prefix;

      size /= factor;
    }

    return size.ToString(format) + prefixes.Last();
  }

}
