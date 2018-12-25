using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Libraries;

namespace DupMerge {
  class Program {

    #region nested types

    private enum ExitCode {
      Success = 0,
      DirectoryNotFound = -1,
    }

    private class Configuration {
      public long MinimumFileSizeInBytes { get; set; } = 1;
      public long MaximumFileSizeInBytes { get; set; } = long.MaxValue;
      public bool AlsoTrySymbolicLinks { get; set; }
      public bool SetReadOnlyAttributeOnNewHardLinks { get; set; }
      public bool SetReadOnlyAttributeOnNewSymbolicLinks { get; set; }
      public bool SetReadOnlyAttributeOnExistingHardLinks { get; set; }
      public bool SetReadOnlyAttributeOnExistingSymbolicLinks { get; set; }
      public bool RemoveHardLinks { get; set; }
      public bool DeleteHardLinkedFiles { get; set; }
      public bool RemoveSymbolicLinks { get; set; }
      public bool DeleteSymbolicLinkedFiles { get; set; }
      public int MaximumCrawlerThreads { get; set; } = Math.Min(Environment.ProcessorCount, 8);
    }

    private class FileEntry {
      private const int _COMPARISON_BLOCK_SIZE = 4096;
      private static readonly byte[] _EMPTY_BYTES = new byte[0];
      private readonly Lazy<long> _fileSize;
      private readonly Lazy<byte[]> _checksum;

      public FileEntry(FileInfo source) {
        this._Source = source;
        this._checksum = new Lazy<byte[]>(this._CalculateChecksum);
        this._fileSize = new Lazy<long>(this._GetLength);
      }

      private FileInfo _Source { get; }
      private long _FileSize => this._fileSize.Value;
      private byte[] _Checksum => this._checksum.Value;

      /// <summary>
      /// Gets the file length in bytes.
      /// </summary>
      /// <returns></returns>
      private long _GetLength() => this._Source.Length;

      /// <summary>
      /// Calculates a quick checksum.
      /// NOTE: In our case we create SHA512 by using the first and last block (if available)
      /// </summary>
      /// <returns></returns>
      private byte[] _CalculateChecksum() {
        var length = this._FileSize;
        if (length <= 0)
          return _EMPTY_BYTES;

        byte[] result;

        using (var provider = new SHA512CryptoServiceProvider())
        using (var stream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
          var buffer = new byte[_COMPARISON_BLOCK_SIZE];

          // read first block
          var bytesRead = stream.Read(buffer, 0, _COMPARISON_BLOCK_SIZE);
          if (length > _COMPARISON_BLOCK_SIZE) {
            provider.TransformBlock(buffer, 0, bytesRead, buffer, 0);

            // read last block (or what is left of it)
            stream.Seek(Math.Max(_COMPARISON_BLOCK_SIZE, length - _COMPARISON_BLOCK_SIZE), SeekOrigin.Begin);
            bytesRead = stream.Read(buffer, 0, _COMPARISON_BLOCK_SIZE);
          }

          provider.TransformFinalBlock(buffer, 0, bytesRead);
          result = provider.Hash;
        }

        return result;
      }

      /// <summary>
      /// Creates a stream of block indexes, which are not simply following each other the get better chances to detect differences early.
      /// NOTE: In our case we alternate between a block from the beginning and a block from the ending of a file.
      /// </summary>
      /// <param name="blockCount">The block count.</param>
      /// <returns>Block indices</returns>
      private static IEnumerable<long> _BlockIndexShuffler(long blockCount) {
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

      /// <summary>
      /// Tests if the given entry has equal file content and this entry.
      /// </summary>
      /// <param name="other">The other.</param>
      /// <returns><c>true</c> if both files are equal; otherwise, <c>false</c>.</returns>
      public bool Equals(FileEntry other) {
        if (ReferenceEquals(other, null))
          return false;

        try {
          var myLength = this._FileSize;

          // NOTE: STEP 1: compare sizes - should always equal because we make sure that only same size files are compared by the business logic
          if (myLength != other._FileSize)
            return false;

          if (myLength == 0)
            return true;

          // NOTE: STEP 2: compare checksums, hopefully this saves us from comparing byte-by-byte and because checksums are cached in-memory we also spare some re-read I/O
          var sourceChecksum = this._Checksum;
          var comparisonChecksum = other._Checksum;
          if (!_ByteArraysEqual(sourceChecksum, sourceChecksum.Length, comparisonChecksum, comparisonChecksum.Length))
            return false;

          // NOTE: STEP 3: compare bytewise
          using (var sourceStream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
          using (var comparisonStream = new FileStream(other._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)) {

            // NOTE: we're going to compare buffers (A, A') while reading the next blocks (B, B') in already
            var sourceBufferA = new byte[_COMPARISON_BLOCK_SIZE];
            var comparisonBufferA = new byte[_COMPARISON_BLOCK_SIZE];

            var sourceBufferB = new byte[_COMPARISON_BLOCK_SIZE];
            var comparisonBufferB = new byte[_COMPARISON_BLOCK_SIZE];

            long lastBlockSize;
            var blockCount = Math.DivRem(myLength, _COMPARISON_BLOCK_SIZE, out lastBlockSize);

            // if there are bytes left in a partly filled last block - we need one block more
            if (lastBlockSize != 0)
              ++blockCount;

            using (var enumerator = _BlockIndexShuffler(blockCount).GetEnumerator()) {

              // NOTE: should never land here, because only 0-byte files would get us an empty enumerator
              if (!enumerator.MoveNext())
                return false;

              var blockIndex = enumerator.Current;

              // start reading buffers into A and A'
              var sourceAsync = _ReadBlockFromStream(sourceStream, blockIndex, sourceBufferA);
              var comparisonAsync = _ReadBlockFromStream(comparisonStream, blockIndex, comparisonBufferA);
              int sourceBytes;
              int comparisonBytes;

              while (enumerator.MoveNext()) {
                sourceBytes = sourceAsync.Result;
                comparisonBytes = comparisonAsync.Result;

                // start reading next buffers into B and B'
                blockIndex = enumerator.Current;
                sourceAsync = _ReadBlockFromStream(sourceStream, blockIndex, sourceBufferB);
                comparisonAsync = _ReadBlockFromStream(comparisonStream, blockIndex, comparisonBufferB);

                // compare A and A' and return false upon difference
                if (!_ByteArraysEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes))
                  return false;

                // switch A and B and A' and B'
                _Swap(ref sourceBufferA, ref sourceBufferB);
                _Swap(ref comparisonBufferA, ref comparisonBufferB);
              }

              // compare A and A'
              sourceBytes = sourceAsync.Result;
              comparisonBytes = comparisonAsync.Result;
              return _ByteArraysEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes);
            }
          }

        } catch (Exception e) {

          // TODO: find out which side failed and remove it from the known files list
          Console.WriteLine($"[Error] A comparison failed:{e.Message}");

          // if either file could not be read - assume they are not equal because we can't be sure
          return false;
        }
      }

      #region static utils

      /// <summary>
      /// Compares two byte-arrays starting from their beginnings.
      /// </summary>
      /// <param name="source">The source.</param>
      /// <param name="sourceLength">Length of the source.</param>
      /// <param name="comparison">The comparison.</param>
      /// <param name="comparisonLength">Length of the comparison.</param>
      /// <returns><c>true</c> if both arrays contain the same data; otherwise, <c>false</c>.</returns>
      private static unsafe bool _ByteArraysEqual(byte[] source, int sourceLength, byte[] comparison, int comparisonLength) {
        Debug.Assert(!ReferenceEquals(source, null), "Source must not be <null>");
        Debug.Assert(!ReferenceEquals(comparison, null), "Comparison must not be <null>");

        if (sourceLength != comparisonLength)
          return false;

        if (ReferenceEquals(source, comparison))
          return true;

        fixed (byte* sourcePin = source, comparisonPin = comparison)
        {
          var sourcePointer = (long*)sourcePin;
          var comparisonPointer = (long*)comparisonPin;
          while (sourceLength >= 8) {
            if (*sourcePointer != *comparisonPointer)
              return false;

            ++sourcePointer;
            ++comparisonPointer;
            sourceLength -= 8;
          }

          var byteSourcePointer = (byte*)sourcePointer;
          var byteComparisonPointer = (byte*)comparisonPointer;
          while (sourceLength > 0) {
            if (*byteSourcePointer != *byteComparisonPointer)
              return false;

            ++byteSourcePointer;
            ++byteComparisonPointer;
            --sourceLength;
          }
        }
        return true;
      }

      /// <summary>
      /// Swaps the content of two byte array pointers.
      /// </summary>
      /// <param name="bufferA">The A buffer.</param>
      /// <param name="bufferB">The B buffer.</param>
      private static void _Swap(ref byte[] bufferA, ref byte[] bufferB) {
        var temp = bufferA;
        bufferA = bufferB;
        bufferB = temp;
      }

      /// <summary>
      /// Starts filling a block from a stream with a given index.
      /// </summary>
      /// <param name="stream">The stream.</param>
      /// <param name="blockIndex">Index of the block.</param>
      /// <param name="buffer">The buffer to store data at.</param>
      /// <returns>A Task to wait on</returns>
      private static Task<int> _ReadBlockFromStream(Stream stream, long blockIndex, byte[] buffer) {
        stream.Seek(blockIndex * _COMPARISON_BLOCK_SIZE, SeekOrigin.Begin);
        return stream.ReadAsync(buffer, 0, _COMPARISON_BLOCK_SIZE);
      }

      #endregion

    }

    #endregion

    /// <summary>
    /// Processes all command line switches thus setting apropriate properties in the configuration.
    /// </summary>
    /// <param name="switches">The switches.</param>
    /// <param name="configuration">The configuration.</param>
    private static void _ProcessSwitches(IEnumerable<string> switches, Configuration configuration) {
      foreach (var @switch in switches) {
        var index = @switch.IndexOf('=');

        var name = @switch;
        string value = null;
        if (index >= 0) {
          value = @switch.Substring(index + 1);
          name = @switch.Substring(0, index);
        }

        switch (name) {
          case "-t":
          case "--threads":
          {
            configuration.MaximumCrawlerThreads = int.Parse(value);
            break;
          }
          case "-m":
          case "--minimum":
          {
            configuration.MinimumFileSizeInBytes = long.Parse(value);
            break;
          }
          case "-M":
          case "--maximum":
          {
            configuration.MaximumFileSizeInBytes = long.Parse(value);
            break;
          }
          case "-s":
          case "--allow-symlink":
          {
            configuration.AlsoTrySymbolicLinks = true;
            break;
          }
          case "-D":
          case "--delete":
          {
            configuration.DeleteHardLinkedFiles = true;
            configuration.DeleteSymbolicLinkedFiles = true;
            break;
          }
          case "-Dhl":
          case "--delete-hardlinks":
          {
            configuration.DeleteHardLinkedFiles = true;
            break;
          }
          case "-Dsl":
          case "--delete-symlinks":
          case "--delete-symboliclinks":
          {
            configuration.DeleteSymbolicLinkedFiles = true;
            break;
          }
          case "-R":
          case "--remove":
          {
            configuration.RemoveHardLinks = true;
            configuration.RemoveSymbolicLinks = true;
            break;
          }
          case "-Rhl":
          case "--remove-hardlinks":
          {
            configuration.RemoveHardLinks = true;
            break;
          }
          case "-Rsl":
          case "--remove-symlinks":
          case "--remove-symboliclinks":
          {
            configuration.RemoveSymbolicLinks = true;
            break;
          }
          case "-sro":
          case "--set-readonly":
          {
            configuration.SetReadOnlyAttributeOnNewHardLinks = true;
            configuration.SetReadOnlyAttributeOnNewSymbolicLinks = true;
            break;
          }
          case "-uro":
          case "--update-readonly":
          {
            configuration.SetReadOnlyAttributeOnExistingHardLinks = true;
            configuration.SetReadOnlyAttributeOnExistingSymbolicLinks = true;
            break;
          }
          case "-ro":
          case "--readonly":
          {
            configuration.SetReadOnlyAttributeOnNewHardLinks = true;
            configuration.SetReadOnlyAttributeOnNewSymbolicLinks = true;
            configuration.SetReadOnlyAttributeOnExistingHardLinks = true;
            configuration.SetReadOnlyAttributeOnExistingSymbolicLinks = true;
            break;
          }
        }
      }
    }

    /// <summary>
    /// Processes the given folders with the given configuration.
    /// </summary>
    /// <param name="directories">The directories.</param>
    /// <param name="configuration">The configuration.</param>
    private static void _ProcessFolders(IEnumerable<DirectoryInfo> directories, Configuration configuration) {
      var seenFiles = new ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>>();
      var stack = new ConcurrentStack<DirectoryInfo>();
      stack.PushRange(directories);
      var threads = new Thread[Math.Max(1, configuration.MaximumCrawlerThreads)];

      using (var autoresetEvent = new AutoResetEvent(false)) {
        var runningWorkers = new[] { threads.Length };

        for (var i = 0; i < threads.Length; ++i) {
          threads[i] = new Thread(_ThreadWorker);
          threads[i].Start(Tuple.Create(stack, configuration, seenFiles, autoresetEvent, runningWorkers));
        }

        foreach (var thread in threads)
          thread.Join();
      }
    }

    /// <summary>
    /// Processes a single folder with the given configuration.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="configuration">The configuration.</param>
    private static void _ProcessFolder(DirectoryInfo directory, Configuration configuration)
          => _ProcessFolders(new[] { directory }, configuration)
          ;

    /// <summary>
    /// A wrapper for the worker thread - simply unwraps the state object.
    /// </summary>
    /// <param name="state">The state.</param>
    private static void _ThreadWorker(object state) {
      var t = (Tuple<ConcurrentStack<DirectoryInfo>, Configuration, ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>>, AutoResetEvent, int[]>)state;
      _ThreadWorker(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
    }

    /// <summary>
    /// A worker thread, pulling items from the stack, handling files and directories, etc.
    /// </summary>
    /// <param name="stack">The stack.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="seenItems">The seen items.</param>
    /// <param name="waiter">The waiter.</param>
    /// <param name="state">The state.</param>
    private static void _ThreadWorker(ConcurrentStack<DirectoryInfo> stack, Configuration configuration, ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems, AutoResetEvent waiter, int[] state) {
      while (true) {
        DirectoryInfo current;

        if (!stack.TryPop(out current)) {
          // when stack is empty, signal we're lazy and if all other threads are also, end thread
          if (Interlocked.Decrement(ref state[0]) == 0) {
            // signal another thread to continue exiting
            waiter.Set();
            Console.WriteLine($"Ending Thread #{Thread.CurrentThread.ManagedThreadId}");
            return;
          }

          waiter.WaitOne();
          Interlocked.Increment(ref state[0]);
          continue;
        }

        // push directories and wake up any sleeping threads
        foreach (var directory in current.EnumerateDirectories()) {
          stack.Push(directory);

          // notify other threads which may be waiting for work
          waiter.Set();
        }

        foreach (var item in current.EnumerateFiles())
          _HandleFile(item, configuration, seenItems);
      }
    }

    /// <summary>
    /// Handles a single file.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="seenItems">The seen items.</param>
    private static void _HandleFile(
          FileInfo item,
          Configuration configuration,
          ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems) {

      var length = item.Length;
      if (length < configuration.MinimumFileSizeInBytes || length > configuration.MaximumFileSizeInBytes)
        return;

      var knownWithThisLength = seenItems.GetOrAdd(length, _ => new ConcurrentDictionary<string, FileEntry>());

      // preventing othre threads from processing files with the same size
      // avoiding a race condition where all known links to a file are removed at once, thus loosing data completely 
      lock (knownWithThisLength)
        try {
          _HandleFileWithGivenSize(item, configuration, knownWithThisLength);
        } catch (Exception e) {
          Console.WriteLine($"[Error] Could not process file {item.FullName}: {e.Message}");
        }
    }

    /// <summary>
    /// Handles a specific file along with a group of files already seen with the same size.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="knownWithThisLength">Length of the known with this.</param>
    private static void _HandleFileWithGivenSize(
          FileInfo item,
          Configuration configuration,
          ConcurrentDictionary<string, FileEntry> knownWithThisLength) {
      var myKey = _GenerateKey(item);
      var checksum = knownWithThisLength.GetOrAdd(myKey, new FileEntry(item));

      var isHardLink = false;
      IEnumerable<FileInfo> hardlinks;

      try {
        hardlinks = item.GetHardLinkTargets();
      } catch (Exception e) {
        _RemoveFileEntry(item, knownWithThisLength);
        Console.WriteLine($"[Error] Could not enumerate HardLinks {item.FullName}: {e.Message}");
        return;
      }

      foreach (var target in hardlinks) {
        isHardLink = true;
        knownWithThisLength.TryAdd(_GenerateKey(target), new FileEntry(target));
      }

      if (isHardLink) {
        _HandleExistingHardLink(item, configuration, knownWithThisLength);
        return;
      }

      string symlink;

      try {
        symlink = item.GetSymbolicLinkTarget();
      } catch (Exception e) {
        _RemoveFileEntry(item, knownWithThisLength);
        Console.WriteLine($"[Error] Could not enumerate SymLink {item.FullName}: {e.Message}");
        return;
      }

      if (symlink != null) {
        knownWithThisLength.TryAdd(symlink, new FileEntry(new FileInfo(symlink)));
        _HandleExistingSymbolicLink(item, configuration, knownWithThisLength);
        return;
      }

      // find matching file in seen list and try to hard or symlink
      var sameFiles =
        knownWithThisLength
        .Where(kvp => kvp.Key != myKey)
        .Where(kvp => kvp.Value.Equals(checksum))
        .Select(kvp => kvp.Key)
        ;

      foreach (var sameFile in sameFiles) {
        var temporaryFile = _CreateTemporaryFileInSameDirectory(item);
        temporaryFile.Delete();

        var isSymlink = false;

        try {
          temporaryFile.CreateHardLinkFrom(sameFile);
        } catch (Exception e1) {
          if (configuration.AlsoTrySymbolicLinks) {
            isSymlink = true;
            try {
              temporaryFile.CreateSymbolicLinkFrom(sameFile);
            } catch (Exception e2) {
              Console.WriteLine(
                $"[Warning] Could not Symlink {item.FullName}({FilesizeFormatter.FormatUnit(item.Length, true)}) --> {sameFile}: {e2.Message}");
              continue;
            }
          } else {
            Console.WriteLine(
              $"[Warning] Could not Hardlink {item.FullName}({FilesizeFormatter.FormatUnit(item.Length, true)}) --> {sameFile}: {e1.Message}");
            continue;
          }
        }

        var isAlreadyDeleted = false;

        try {
          item.Attributes &= ~FileAttributes.ReadOnly;
          item.Delete();
          isAlreadyDeleted = true;
          File.Move(temporaryFile.FullName, item.FullName);
        } catch {
          if (isAlreadyDeleted) {

            // undo file deletion
            temporaryFile.CopyTo(item.FullName, true);
          } else {

            // undo temp file creation
            temporaryFile.Delete();
          }
          throw;
        }

        if (isSymlink) {
          if (configuration.SetReadOnlyAttributeOnNewSymbolicLinks)
            item.Attributes |= FileAttributes.ReadOnly;
        } else {
          if (configuration.SetReadOnlyAttributeOnNewHardLinks)
            item.Attributes |= FileAttributes.ReadOnly;
        }

        Console.WriteLine($"[Info] Created {(isSymlink ? "Symlink" : "Hardlink")} for {item.FullName}({FilesizeFormatter.FormatUnit(item.Length, true)}) --> {sameFile}");
        return;
      }
    }

    /// <summary>
    /// Handles files which are already symbolic links.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="knownWithThisLength">Length of the known with this.</param>
    private static void _HandleExistingSymbolicLink(
          FileInfo item,
          Configuration configuration,
          ConcurrentDictionary<string, FileEntry> knownWithThisLength
        ) {
      if (configuration.DeleteSymbolicLinkedFiles) {
        _RemoveFileEntry(item, knownWithThisLength);
        _DeleteLink(item);
        Console.WriteLine($"[Info] Deleted Symlink {item.FullName}");
        return;
      }

      if (configuration.RemoveSymbolicLinks) {
        _RemoveFileEntry(item, knownWithThisLength);
        try {
          _ReplaceFileLinkWithFileContent(item);
          Console.WriteLine($"[Info] Removed Symlink {item.FullName}");
        } catch (Exception e) {
          Console.WriteLine($"[Error] Could not remove Symlink {item.FullName}: {e.Message}");
        }
        return;
      }

      if (configuration.SetReadOnlyAttributeOnExistingSymbolicLinks && ((item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)) {
        Console.WriteLine($"[Info] Setting read-only attribute on Symlink {item.FullName}");
        item.Attributes |= FileAttributes.ReadOnly;
        return;
      }

      Console.WriteLine($"[Info] {item.FullName} is alrady a Symlink");
    }

    /// <summary>
    /// Handles files which are already hardlinks.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="knownWithThisLength">Length of the known with this.</param>
    private static void _HandleExistingHardLink(
          FileInfo item,
          Configuration configuration,
          ConcurrentDictionary<string, FileEntry> knownWithThisLength
        ) {
      if (configuration.DeleteHardLinkedFiles) {
        _RemoveFileEntry(item, knownWithThisLength);
        _DeleteLink(item);
        Console.WriteLine($"[Info] Deleted Hardlink {item.FullName}");
        return;
      }

      if (configuration.RemoveHardLinks) {
        _RemoveFileEntry(item, knownWithThisLength);
        try {
          _ReplaceFileLinkWithFileContent(item);
          Console.WriteLine($"[Info] Removed Hardlink {item.FullName}");
        } catch (Exception e) {
          Console.WriteLine($"[Error] Could not remove Hardlink {item.FullName}: {e.Message}");
        }
        return;
      }

      if (configuration.SetReadOnlyAttributeOnExistingHardLinks && ((item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)) {
        Console.WriteLine($"[Info] Setting read-only attribute on Hardlink {item.FullName}");
        item.Attributes |= FileAttributes.ReadOnly;
        return;
      }

      Console.WriteLine($"[Info] {item.FullName} is alrady a Hardlink");
    }

    /// <summary>
    /// Deletes a hardlink or symlink.
    /// </summary>
    /// <param name="item">The item.</param>
    private static void _DeleteLink(FileInfo item) {
      item.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden);
      item.Delete();
    }

    /// <summary>
    /// Replaces a hardlink or symblink with its content.
    /// </summary>
    /// <param name="item">The item.</param>
    private static void _ReplaceFileLinkWithFileContent(FileInfo item) {
      var attributes = item.Attributes;

      const int NOT_YET_STARTED = 0;
      const int CRASH_COPYING_FILE = 1;
      const int CRASH_DURING_DELETE = 2;
      const int CRASH_DURING_RENAME = 3;
      const int CRASH_DURING_ATTRIBUTION = 4;
      const int EVERYTHING_DONE = 5;
      int executionState = NOT_YET_STARTED;
      FileInfo temporaryName = null;

      try {
        temporaryName = _CreateTemporaryFileInSameDirectory(item);
        executionState = CRASH_COPYING_FILE;

        // sparse and compress before copying
        try {
          temporaryName.Attributes |= (attributes & FileAttributes.SparseFile);
        } catch {
          ; // NOTE: could not enable sparse file - who cares?
        }

        if ((attributes & FileAttributes.Encrypted) == FileAttributes.Encrypted)
          temporaryName.Encrypt();

        if ((attributes & FileAttributes.Compressed) == FileAttributes.Compressed)
          try {
            temporaryName.TryEnableCompression();
          } catch {
            ; // NOTE: could not enable compression - who cares?
          }

        item.CopyTo(temporaryName.FullName, true);

        executionState = CRASH_DURING_DELETE;
        item.Delete();

        executionState = CRASH_DURING_RENAME;
        //Microsoft.VisualBasic.FileSystem.Rename(temporaryName.FullName, item.Name);
        File.Move(temporaryName.FullName, item.FullName);

        executionState = CRASH_DURING_ATTRIBUTION;
        item.Attributes = attributes & (FileAttributes.ReadOnly | FileAttributes.Archive | FileAttributes.System | FileAttributes.Hidden);
        item.Attributes |= attributes & FileAttributes.NotContentIndexed;

        executionState = EVERYTHING_DONE;
      } finally {
        switch (executionState) {
          case CRASH_DURING_ATTRIBUTION:
          {
            // NOTE: who cares for wrongly set attributes
            Console.WriteLine($"[Verbose] Exception during attribution {item.FullName}");
            break;
          }
          case NOT_YET_STARTED:
          case EVERYTHING_DONE:
          {
            // nothing yet created or all done - tidy up nothing
            break;
          }
          case CRASH_COPYING_FILE:
          case CRASH_DURING_DELETE:
          {
            if (executionState == CRASH_COPYING_FILE)
              Console.WriteLine($"[Verbose] Exception during CopyFile {item.FullName}");
            else
              Console.WriteLine($"[Verbose] Exception during DeleteFile {item.FullName}");

            // just remove temp file
            temporaryName.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System);
            temporaryName.Delete();
            break;
          }
          case CRASH_DURING_RENAME:
          {
            Console.WriteLine($"[Verbose] Exception during RenameFile {item.FullName}");

            // undo delete operation
            temporaryName?.MoveTo(item.FullName);
            break;
          }
        }
      }
    }

    /// <summary>
    /// Removes an entry from the list of seen files.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="knownWithThisLength">List of known files with the same length.</param>
    private static void _RemoveFileEntry(FileInfo item, ConcurrentDictionary<string, FileEntry> knownWithThisLength) {
      FileEntry checksum;
      knownWithThisLength.TryRemove(_GenerateKey(item), out checksum);
    }

    /// <summary>
    /// Creates a temporary file in the same directory as the original.
    /// </summary>
    /// <param name="file">The original file.</param>
    /// <returns>A file with a similar name.</returns>
    private static FileInfo _CreateTemporaryFileInSameDirectory(FileInfo file) {
      var name = file.FullName;

      while (true) {
        var result = new FileInfo(name + ".$$$");
        name = result.FullName;
        if (result.Exists)
          continue;

        try {
          var fileHandle = result.Open(FileMode.CreateNew, FileAccess.Write);
          fileHandle.Close();
          result.Refresh();
          return result;
        } catch (IOException e) when ((uint)e.HResult == 0x80070050) {
          ; // possibly another process created the file already
        }
      }
    }

    /// <summary>
    /// Generates a key for use with the in-memory database.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns></returns>
    private static string _GenerateKey(FileInfo file) => file.FullName;

    static int Main(string[] args) {
      var directories = new List<DirectoryInfo>();
      var switches = new List<string>();

      var maybeSwitch = true;

      foreach (var arg in args) {
        if (maybeSwitch) {
          if (arg.StartsWith("-")) {
            switches.Add(arg);
            continue;
          }

          maybeSwitch = false;
        }

        var directory = new DirectoryInfo(arg);

        if (!directory.Exists) {
          Console.WriteLine($"[Error] Directory {arg} not found - Aborting!");
          return (int)ExitCode.DirectoryNotFound;
        }

        directories.Add(directory);
      }

      // add current directory if none passed
      if (!directories.Any())
        directories.Add(new DirectoryInfo(Environment.CurrentDirectory));

      var configuration = new Configuration();
      _ProcessSwitches(switches, configuration);
      _ProcessFolders(directories, configuration);

#if DEBUG
      Console.WriteLine("READY.");
      Console.ReadKey();
#endif

      return (int)ExitCode.Success;
    }

  }
}