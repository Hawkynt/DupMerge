using System.Collections.Generic;

internal static class BlockIndexShuffler {
  /// <summary>
  /// Creates a stream of block indexes, which are not simply following each other the get better chances to detect differences early.
  /// NOTE: In our case we alternate between a block from the beginning and a block from the ending of a file.
  /// </summary>
  /// <param name="blockCount">The block count.</param>
  /// <returns>Block indices</returns>
  public static IEnumerable<long> Shuffle(long blockCount) {
    var lowerBlockIndex = 0;
    var upperBlockIndex = blockCount - 1;

    while (lowerBlockIndex < upperBlockIndex) {
      yield return lowerBlockIndex++;
      yield return upperBlockIndex--;
    }

    // if odd number of elements, return the last element (which is in the middle)
    if ((blockCount & 1) == 1)
      yield return lowerBlockIndex;

  }
}