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
  
  public static string FormatIEC(double size, string format = "0") {
    Guard.Against.ArgumentIsNull(format);

    return size switch {
      > EiB => ((float)size / EiB).ToString(format) + " EiB",
      > PiB => ((float)size / PiB).ToString(format) + " PiB",
      > TiB => ((float)size / TiB).ToString(format) + " TiB",
      > GiB => ((float)size / GiB).ToString(format) + " GiB",
      > MiB => ((float)size / MiB).ToString(format) + " MiB",
      > KiB => ((float)size / KiB).ToString(format) + " KiB",
      > 1 => size.ToString(format) + " Bytes",
      _ => size.ToString(format) + " Byte"
    };
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
