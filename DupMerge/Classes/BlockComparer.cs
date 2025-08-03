#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Classes;

internal static unsafe class BlockComparer {
  /// <summary>
  /// Compares two byte-arrays starting from their beginnings.
  /// </summary>
  /// <param name="source">The source.</param>
  /// <param name="sourceLength">Length of the source.</param>
  /// <param name="comparison">The comparison.</param>
  /// <param name="comparisonLength">Length of the comparison.</param>
  /// <returns><c>true</c> if both arrays contain the same data; otherwise, <c>false</c>.</returns>
  public static bool IsEqual(byte[] source, int sourceLength, byte[] comparison, int comparisonLength) {
    if (sourceLength != comparisonLength)
      return false;

    if (ReferenceEquals(source, comparison))
      return true;

    fixed (byte* sourcePin = source, comparisonPin = comparison) {
      var currentSource = sourcePin;
      var currentComparison = comparisonPin;
      var remainingLength = sourceLength;
      
      return
          CompareUInt64(&currentSource, &currentComparison, ref remainingLength)
          && CompareUInt32(&currentSource, &currentComparison, ref remainingLength)
          && CompareBytes(&currentSource, &currentComparison, ref remainingLength)
        ;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static bool CompareUInt64(byte** source, byte** comparison, ref int count) {
    var s = (long*)*source;
    var c = (long*)*comparison;

    const int UNROLL_COUNT = 8;
    const int UNROLL_BYTES = UNROLL_COUNT * sizeof(long);
    if(count > UNROLL_BYTES) {
      var n = count / UNROLL_BYTES;
      count %= UNROLL_BYTES;

      do {

        var r0 = *s ^ *c;
        var r1 = s[1] ^ c[1];
        var r2 = s[2] ^ c[2];
        var r3 = s[3] ^ c[3];
        var r4 = s[4] ^ c[4];
        var r5 = s[5] ^ c[5];
        var r6 = s[6] ^ c[6];
        var r7 = s[7] ^ c[7];
        r0 |= r1;
        r2 |= r3;
        r4 |= r5;
        r6 |= r7;

        r0 |= r2;
        r4 |= r6;

        r0 |= r4;

        if (r0!=0)
          return false;

        s += UNROLL_COUNT;
        c += UNROLL_COUNT;
      } while (--n > 0);
    }

    while(count >= sizeof(long)){
      if(*s != *c)
        return false;

      ++s;
      ++c;
      count -= sizeof(long);
    }
    
    *source = (byte*)s;
    *comparison = (byte*)c;
    return true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static bool CompareUInt32(byte** source, byte** comparison, ref int count) {
    var s=(int*)*source;
    var c=(int*)*comparison;
    while(count>=sizeof(int)){
      if(*s!=*c)
        return false;

      ++s;
      ++c;
      count-=sizeof(int);
    }
    
    *source = (byte*)s;
    *comparison = (byte*)c;
    return true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static bool CompareBytes(byte** source, byte** comparison, ref int count) {
    var s = *source;
    var c = *comparison;
    while(count>0){
      if(*s!=*c)
        return false;

      ++s;
      ++c;
      --count;
    }
    
    *source = s;
    *comparison = c;
    return true;
  }

}