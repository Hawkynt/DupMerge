#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Libraries;

namespace Classes;

internal static class DuplicateFileMerger {

  /// <summary>
  /// Processes the given folders with the given configuration.
  /// </summary>
  /// <param name="directories">The directories.</param>
  /// <param name="configuration">The configuration.</param>
  /// <param name="stats">The statistics.</param>
  public static void ProcessFolders(IList<DirectoryInfo> directories, Configuration configuration,RuntimeStats stats) {
    var seenFiles = new ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>>();
    var stack = new ConcurrentStack<DirectoryInfo>();
    stack.PushRange(directories);
    stats.IncrementFolders(directories.Count);
    var threads = new Thread[Math.Max(1, configuration.MaximumCrawlerThreads)];

    using var autoresetEvent = new AutoResetEvent(false);
    var runningWorkers = new[] {threads.Length};

    for (var i = 0; i < threads.Length; ++i) {
        
      static void Worker(object? state) {
        var (stack, configuration, stats, seenFiles, autoResetEvent, runningWorkers) 
          = (ValueTuple<ConcurrentStack<DirectoryInfo>, Configuration,RuntimeStats,  ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>>, AutoResetEvent, int[]>) 
          state!
          ;

        _ThreadWorker(stack, configuration, stats, seenFiles, autoResetEvent,runningWorkers);
      }

      threads[i] = new(Worker);
      threads[i].Start((stack, configuration, stats,seenFiles, autoresetEvent, runningWorkers));
    }

    foreach (var thread in threads)
      thread.Join();
  }

  /// <summary>
  /// A worker thread, pulling items from the stack, handling files and directories, etc.
  /// </summary>
  /// <param name="stack">The stack.</param>
  /// <param name="configuration">The configuration.</param>
  /// <param name="stats">The statistics</param>
  /// <param name="seenItems">The seen items.</param>
  /// <param name="waiter">The waiter.</param>
  /// <param name="state">The state.</param>
  private static void _ThreadWorker(
    ConcurrentStack<DirectoryInfo> stack, 
    Configuration configuration,
    RuntimeStats stats,  
    ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems, 
    EventWaitHandle waiter, 
    int[] state
  ) {
    while (true) {
      if (!stack.TryPop(out var current)) {
        // when stack is empty, signal we're lazy and if all other threads are also, end thread
        if (Interlocked.Decrement(ref state[0]) == 0) {
          // signal another thread to continue exiting
          waiter.Set();
          Console.WriteLine($"Ending Thread #{Environment.CurrentManagedThreadId}");
          return;
        }

        waiter.WaitOne();
        Interlocked.Increment(ref state[0]);
        continue;
      }

      // push directories and wake up any sleeping threads
      foreach (var directory in current.SafelyEnumerateDirectories()) {
        stack.Push(directory);
        stats.IncrementFolders();

        // notify other threads which may be waiting for work
        waiter.Set();
      }

      foreach (var item in current.SafelyEnumerateFiles())
        _HandleFile(item, configuration, stats, seenItems);
    }
  }

  /// <summary>
  /// Handles a single file.
  /// </summary>
  /// <param name="item">The item.</param>
  /// <param name="configuration">The configuration.</param>
  /// <param name="stats">The statistics.</param>
  /// <param name="seenItems">The seen items.</param>
  private static void _HandleFile(
    FileInfo item,
    Configuration configuration,
    RuntimeStats stats,
    ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems) {

    stats.IncrementFiles();
    var length = item.Length;
    stats.IncrementBytes(length);

    if (length < configuration.MinimumFileSizeInBytes || length > configuration.MaximumFileSizeInBytes)
      return;

    var knownWithThisLength = seenItems.GetOrAdd(length, _ => new());

    // preventing other threads from processing files with the same size
    // avoiding a race condition where all known links to a file are removed at once, thus loosing data completely 
    lock (knownWithThisLength)
      try {
        _HandleFileWithGivenSize(item, configuration,stats, knownWithThisLength);
      } catch (Exception e) {
        Console.WriteLine($"[Error] Could not process file {item.FullName}: {e.Message}");
      }
  }

  /// <summary>
  /// Handles a specific file along with a group of files already seen with the same size.
  /// </summary>
  /// <param name="item">The item.</param>
  /// <param name="configuration">The configuration.</param>
  /// <param name="stats">The statistics.</param>
  /// <param name="knownWithThisLength">Length of the known with this.</param>
  private static void _HandleFileWithGivenSize(
    FileInfo item,
    Configuration configuration,
    RuntimeStats stats,
    ConcurrentDictionary<string, FileEntry> knownWithThisLength) {
    var myKey = _GenerateKey(item);
    var checksum = knownWithThisLength.GetOrAdd(myKey, new FileEntry(item));

    var isHardLink = false;
    IEnumerable<FileInfo> hardlinks;

    try {
      hardlinks = item.GetHardLinkTargets().Where(i=>i.FullName != item.FullName);
    } catch (Exception e) {
      _RemoveFileEntry(item, knownWithThisLength);
      Console.WriteLine($"[Error] Could not enumerate HardLinks {item.FullName}: {e.Message}");
      return;
    }

    foreach (var target in hardlinks) {
      isHardLink = true;
      Console.WriteLine($"[Verbose] {item.FullName} > {target.FullName}");
      knownWithThisLength.TryAdd(_GenerateKey(target), new(target));
    }

    if (isHardLink) {
      stats.HardLinkStats.IncreaseSeen();
      if (configuration.ShowInfoOnly)
        return;

      _HandleExistingHardLink(item, configuration,stats, knownWithThisLength);
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
      knownWithThisLength.TryAdd(symlink, new(new(symlink)));
      stats.SymbolicLinkStats.IncreaseSeen();
      if (configuration.ShowInfoOnly)
        return;

      _HandleExistingSymbolicLink(item, configuration, stats,knownWithThisLength);
      return;
    }

    if (configuration.ShowInfoOnly)
      return;

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
        stats.SymbolicLinkStats.IncreaseCreated();
        if (configuration.SetReadOnlyAttributeOnNewSymbolicLinks)
          item.Attributes |= FileAttributes.ReadOnly;
      } else {
        stats.HardLinkStats.IncreaseCreated();
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
  /// <param name="stats">The statistics.</param>
  /// <param name="knownWithThisLength">Length of the known with this.</param>
  private static void _HandleExistingSymbolicLink(
    FileInfo item,
    Configuration configuration,
    RuntimeStats stats,
    ConcurrentDictionary<string, FileEntry> knownWithThisLength
  ) {
    if (configuration.DeleteSymbolicLinkedFiles) {
      _RemoveFileEntry(item, knownWithThisLength);
      _DeleteLink(item);
      stats.SymbolicLinkStats.IncreaseDeleted();
      Console.WriteLine($"[Info] Deleted Symlink {item.FullName}");
      return;
    }

    if (configuration.RemoveSymbolicLinks) {
      _RemoveFileEntry(item, knownWithThisLength);
      try {
        _ReplaceFileLinkWithFileContent(item);
        stats.SymbolicLinkStats.IncreaseRemoved();
        Console.WriteLine($"[Info] Removed Symlink {item.FullName}");
      } catch (Exception e) {
        Console.WriteLine($"[Error] Could not remove Symlink {item.FullName}: {e.Message}");
      }

      return;
    }

    if (configuration.SetReadOnlyAttributeOnExistingSymbolicLinks && (item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly) {
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
  /// <param name="stats">The statistics.</param>
  /// <param name="knownWithThisLength">Length of the known with this.</param>
  private static void _HandleExistingHardLink(
    FileInfo item,
    Configuration configuration,
    RuntimeStats stats,
    ConcurrentDictionary<string, FileEntry> knownWithThisLength
  ) {
    if (configuration.DeleteHardLinkedFiles) {
      _RemoveFileEntry(item, knownWithThisLength);
      _DeleteLink(item);
      stats.HardLinkStats.IncreaseDeleted();
      Console.WriteLine($"[Info] Deleted Hardlink {item.FullName}");
      return;
    }

    if (configuration.RemoveHardLinks) {
      _RemoveFileEntry(item, knownWithThisLength);
      try {
        _ReplaceFileLinkWithFileContent(item);
        stats.HardLinkStats.IncreaseRemoved();
        Console.WriteLine($"[Info] Removed Hardlink {item.FullName}");
      } catch (Exception e) {
        Console.WriteLine($"[Error] Could not remove Hardlink {item.FullName}: {e.Message}");
      }

      return;
    }

    if (configuration.SetReadOnlyAttributeOnExistingHardLinks && (item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly) {
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
    var executionState = NOT_YET_STARTED;
    FileInfo? temporaryName = null;

    try {
      temporaryName = _CreateTemporaryFileInSameDirectory(item);
      executionState = CRASH_COPYING_FILE;

      // sparse and compress before copying
      try {
        temporaryName.Attributes |= attributes & FileAttributes.SparseFile;
      } catch {
        // NOTE: could not enable sparse file - who cares?
      }

      if ((attributes & FileAttributes.Encrypted) == FileAttributes.Encrypted)
        temporaryName.Encrypt();

      if ((attributes & FileAttributes.Compressed) == FileAttributes.Compressed)
        try {
          temporaryName.TryEnableCompression();
        } catch {
          // NOTE: could not enable compression - who cares?
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
        case CRASH_DURING_ATTRIBUTION: {
          // NOTE: who cares for wrongly set attributes
          Console.WriteLine($"[Verbose] Exception during attribution {item.FullName}");
          break;
        }

        case NOT_YET_STARTED:
        case EVERYTHING_DONE: {
          // nothing yet created or all done - tidy up nothing
          break;
        }

        case CRASH_COPYING_FILE:
        case CRASH_DURING_DELETE: {
          Console.WriteLine(
            executionState == CRASH_COPYING_FILE 
              ? $"[Verbose] Exception during CopyFile {item.FullName}" 
              : $"[Verbose] Exception during DeleteFile {item.FullName}"
          );

          // just remove temp file
          temporaryName!.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System);
          temporaryName.Delete();
          break;
        }

        case CRASH_DURING_RENAME: {
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
  private static void _RemoveFileEntry(FileInfo item, ConcurrentDictionary<string, FileEntry> knownWithThisLength) 
    => knownWithThisLength.TryRemove(_GenerateKey(item), out _)
    ;

  /// <summary>
  /// Creates a temporary file in the same directory as the original.
  /// </summary>
  /// <param name="file">The original file.</param>
  /// <returns>A file with a similar name.</returns>
  private static FileInfo _CreateTemporaryFileInSameDirectory(FileInfo file) {
    const int ERROR_FILE_EXISTS = unchecked((int)0x80070050);
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
      } catch (IOException e) when (e.HResult == ERROR_FILE_EXISTS) {
        // possibly another process created the file already
      }
    }
  }

  /// <summary>
  /// Generates a key for use with the in-memory database.
  /// </summary>
  /// <param name="file">The file.</param>
  /// <returns></returns>
  private static string _GenerateKey(FileInfo file) => file.FullName;

}
