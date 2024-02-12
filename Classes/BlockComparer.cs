#nullable enable
using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class BlockComparer {
  /// <summary>
  /// Compares two byte-arrays starting from their beginnings.
  /// </summary>
  /// <param name="source">The source.</param>
  /// <param name="sourceLength">Length of the source.</param>
  /// <param name="comparison">The comparison.</param>
  /// <param name="comparisonLength">Length of the comparison.</param>
  /// <returns><c>true</c> if both arrays contain the same data; otherwise, <c>false</c>.</returns>
  public static unsafe bool IsEqual(byte[] source, int sourceLength, byte[] comparison, int comparisonLength) {
    if (sourceLength != comparisonLength)
      return false;

    if (ReferenceEquals(source, comparison))
      return true;

    fixed (byte* sourcePin = source, comparisonPin = comparison) {
        
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool CompareUInt64(byte* source, byte* comparison, ref int count) {
        var s = (long*)source;
        var c = (long*)comparison;

        const int UNROLL_COUNT = 8;
        const int UNROLL_BYTES = UNROLL_COUNT * sizeof(long);
        if(count > UNROLL_BYTES) {
          var n = count / UNROLL_BYTES;
          count %= UNROLL_BYTES;

          do {

            var i = *s == *c;
            var j = s[1] == c[1];
            i &= s[2] == c[2];
            j &= s[3] == c[3];
            i &= s[4] == c[4];
            j &= s[5] == c[5];
            i &= s[6] == c[6];
            j &= s[7] == c[7];
            i &= j;

            if(!i)
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
        return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool CompareUInt32(byte* source, byte* comparison, ref int count) {
        var s=(int*)source;
        var c=(int*)comparison;
        while(count>=sizeof(int)){
          if(*s!=*c)
            return false;

          ++s;
          ++c;
          count-=sizeof(int);
        }
        return true;
      }

      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool CompareBytes(byte* source, byte* comparison, ref int count) {
        while(count>0){
          if(*source!=*comparison)
            return false;

          ++source;
          ++comparison;
          --count;
        }
        return true;
      }

      var result=
          CompareUInt64(sourcePin,comparisonPin,ref sourceLength)
          && CompareUInt32(sourcePin,comparisonPin,ref sourceLength)
          && CompareBytes(sourcePin,comparisonPin,ref sourceLength)
        ;

      return result;
    }
  }
}