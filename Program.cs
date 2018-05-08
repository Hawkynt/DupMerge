using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
// TODO: min and max size
// FIXME: can not read source file

namespace DupMerge {
  class Program {
    private enum ExitCode {
      Success = 0,
      DirectoryNotFound = -1,
    }

    private class Configuration {

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

      }

      // add current directory if none passed
      if (!directories.Any())
        directories.Add(new DirectoryInfo(Environment.CurrentDirectory));

      var configuration = new Configuration();
      _ProcessSwitches(switches, configuration);
      _ProcessFolders(directories, configuration);

      return (int)ExitCode.Success;
    }

    private static void _ProcessSwitches(IEnumerable<string> switches, Configuration configuration) {
      foreach (var @switch in switches) {
        switch (@switch) {
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

    private static void _ProcessFolders(IEnumerable<DirectoryInfo> directories, Configuration configuration) {
      var seenFiles = new ConcurrentDictionary<long, ConcurrentDictionary<string, Lazy<string>>>();
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

    private static void _ProcessFolder(DirectoryInfo directory, Configuration configuration)
      => _ProcessFolders(new[] { directory }, configuration)
      ;

    private static void _ThreadWorker(object state) {
      var t = (Tuple<ConcurrentStack<DirectoryInfo>, Configuration, ConcurrentDictionary<long, ConcurrentDictionary<string, Lazy<string>>>, AutoResetEvent, int[]>)state;
      _ThreadWorker(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5);
    }

    private static void _ThreadWorker(ConcurrentStack<DirectoryInfo> stack, Configuration configuration, ConcurrentDictionary<long, ConcurrentDictionary<string, Lazy<string>>> seenItems, AutoResetEvent waiter, int[] state) {
      while (true) {
        DirectoryInfo current;
        if (!stack.TryPop(out current)) {
          // when stack is empty, signal we're lazy and if all other threads are also, end thread
          if (Interlocked.Decrement(ref state[0]) == 0) {

            // signal another thread to continue exiting
            waiter.Set();
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

    private static void _HandleFile(
      FileInfo item,
      Configuration configuration,
      ConcurrentDictionary<long, ConcurrentDictionary<string, Lazy<string>>> seenItems) {

      var knownWithThisLength = seenItems.GetOrAdd(item.Length, _ => new ConcurrentDictionary<string, Lazy<string>>());

      // preventing othre threads from processing files with the same size
      // avoiding a race condition where all known links to a file are removed at once, thus loosing data completely 
      lock (knownWithThisLength)
        _HandleFileWithGivenSize(item, configuration, knownWithThisLength);

    }

    private static void _HandleFileWithGivenSize(
      FileInfo item,
      Configuration configuration,
      ConcurrentDictionary<string, Lazy<string>> knownWithThisLength) {
      var myKey = _GenerateKey(item);
      var checksum = knownWithThisLength.GetOrAdd(myKey, _ => new Lazy<string>(() => _CalculateChecksum(item)));

      var isHardLink = false;
      IEnumerable<FileInfo> hardlinks;
      try {
        hardlinks = item.GetHardLinkTargets();
      } catch (Exception e) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        Console.WriteLine($"[Error] Could not enumerate HardLinks {item.FullName}: {e.Message}");
        return;
      }

      foreach (var target in hardlinks) {
        isHardLink = true;
        knownWithThisLength.TryAdd(target.FullName, checksum);
      }

      if (isHardLink) {
        _HandleExistingHardLink(item, configuration, knownWithThisLength);
        return;
      }

      string symlink;
      try {
        symlink = item.GetSymbolicLinkTarget();
      } catch (Exception e) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        Console.WriteLine($"[Error] Could not enumerate SymLink {item.FullName}: {e.Message}");
        return;
      }

      if (symlink != null) {
        knownWithThisLength.TryAdd(symlink, checksum);
        _HandleExistingSymbolicLink(item, configuration, knownWithThisLength);
        return;
      }

      // find matching file in seen list and try to hard or symlink
      var rawChecksum = checksum.Value;
      var sameFile =
        knownWithThisLength.Where(kvp => kvp.Key != myKey)
          .Where(kvp => kvp.Value.Value == rawChecksum)
          .Select(kvp => kvp.Key)
          .FirstOrDefault()
          ;

      // no other file similar enough - just move on to next file
      if (sameFile == null)
        return;

      var temporaryFile = _GetTemporaryFile(item);
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
            Console.WriteLine($"[Warning] Could not Symlink {item.FullName}: {e2.Message}");
            return;
          }
        } else {
          Console.WriteLine($"[Warning] Could not Hardlink {item.FullName}: {e1.Message}");
          return;
        }
      }

      var isAlreadyDeleted = false;
      try {
        item.Delete();
        isAlreadyDeleted = true;
        Microsoft.VisualBasic.FileIO.FileSystem.RenameFile(temporaryFile.FullName, item.Name);
      } catch when (isAlreadyDeleted) {

        // undo file deletion
        temporaryFile.CopyTo(item.FullName, true);
        throw;
      }

      if (isSymlink) {
        if (configuration.SetReadOnlyAttributeOnNewSymbolicLinks)
          item.Attributes |= FileAttributes.ReadOnly;
      } else {
        if (configuration.SetReadOnlyAttributeOnNewHardLinks)
          item.Attributes |= FileAttributes.ReadOnly;
      }

      Console.WriteLine($"[Info] Created {(isSymlink ? "Symlink" : "Hardlink")} for {item.FullName} --> {sameFile}");
    }

    private static void _HandleExistingSymbolicLink(
      FileInfo item,
      Configuration configuration,
      ConcurrentDictionary<string, Lazy<string>> knownWithThisLength
    ) {

      if (configuration.DeleteSymbolicLinkedFiles) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        _DeleteLink(item);
        Console.WriteLine($"[Info] Deleted Symlink {item.FullName}");
        return;
      }

      if (configuration.RemoveSymbolicLinks) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        try {
          _ReplaceFileLinkWithFileContent(item);
          Console.WriteLine($"[Info] Removed Symlink {item.FullName}");
        } catch (Exception e) {
          Console.WriteLine($"[Error] Could not remove Symlink {item.FullName}: {e.Message}");
        }
        return;
      }

      if (configuration.SetReadOnlyAttributeOnExistingSymbolicLinks && ((item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)) {
        Console.WriteLine($"[Info] Setting read-only attribute on {item.FullName}");
        item.Attributes |= FileAttributes.ReadOnly;
        return;
      }

      Console.WriteLine($"[Info] {item.FullName} is alrady a Symlink");
    }

    private static void _HandleExistingHardLink(
      FileInfo item,
      Configuration configuration,
      ConcurrentDictionary<string, Lazy<string>> knownWithThisLength
    ) {

      if (configuration.DeleteHardLinkedFiles) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        _DeleteLink(item);
        Console.WriteLine($"[Info] Deleted Hardlink {item.FullName}");
        return;
      }

      if (configuration.RemoveHardLinks) {
        _RemoveChecksumEntry(item, knownWithThisLength);
        try {
          _ReplaceFileLinkWithFileContent(item);
          Console.WriteLine($"[Info] Removed Hardlink {item.FullName}");
        } catch (Exception e) {
          Console.WriteLine($"[Error] Could not remove Hardlink {item.FullName}: {e.Message}");
        }
        return;
      }

      if (configuration.SetReadOnlyAttributeOnExistingHardLinks && ((item.Attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)) {
        Console.WriteLine($"[Info] Setting read-only attribute on {item.FullName}");
        item.Attributes |= FileAttributes.ReadOnly;
        return;
      }

      Console.WriteLine($"[Info] {item.FullName} is alrady a Hardlink");
    }

    private static void _DeleteLink(FileInfo item) {
      item.Attributes &= ~(FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden);
      item.Delete();
    }

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
        temporaryName = _GetTemporaryFile(item);
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
        Microsoft.VisualBasic.FileIO.FileSystem.RenameFile(temporaryName.FullName, item.Name);

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

    private static void _RemoveChecksumEntry(FileInfo item, ConcurrentDictionary<string, Lazy<string>> knownWithThisLength) {
      Lazy<string> checksum;
      knownWithThisLength.TryRemove(_GenerateKey(item), out checksum);

      // force creation of checksum, otherwise value creation may fail later
      while (!checksum.IsValueCreated) {
        var dummy = checksum.Value;
      }
    }

    private static FileInfo _GetTemporaryFile(FileInfo file) {
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
        } catch (IOException e) when (e.HResult == 0x80070050) {
          ; // possibly another process created the file already
        }
      }
    }

    private static string _GenerateKey(FileInfo file) => file.FullName;

    private static string _CalculateChecksum(FileInfo file) {
      var result = new StringBuilder();
      result.Append(file.Length);
      result.Append(':');
      if (file.Length <= 0)
        return result.ToString();

      byte[] hash;
      using (var provider = new SHA512CryptoServiceProvider())
      using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
        hash = provider.ComputeHash(stream);

      foreach (var @byte in hash)
        result.Append(@byte.ToString("X2"));

      return result.ToString();
    }

  }
}
