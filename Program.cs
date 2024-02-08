using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Classes;
using Libraries;

namespace DupMerge;

class Program {
  
  static int Main(string[] args) {
    var directories = new List<DirectoryInfo>();
    var switches = new List<string>();

    var maybeSwitch = true;

    foreach (var arg in args) {
      if (maybeSwitch) {
        if (arg.StartsWithAny("-","/")) {
          switches.Add(arg);
          continue;
        }

        maybeSwitch = false;
      }

      var directory = new DirectoryInfo(arg);

      if (!directory.Exists) {
        Console.WriteLine($"[Error] Directory {arg} not found - Aborting!");
        return (int)CLI.ExitCode.DirectoryNotFound;
      }

      directories.Add(directory);
    }

    // add current directory if none passed
    if (!directories.Any())
      directories.Add(new DirectoryInfo(Environment.CurrentDirectory));

    var configuration = new Configuration();
    CLI.ProcessCommandLine(switches, configuration);
    var stats=new RuntimeStats();
    DuplicateFileMerger.ProcessFolders(directories, configuration,stats);
    _ShowStatistics(stats);

#if DEBUG
    Console.WriteLine("READY.");
    Console.ReadKey();
#endif

    return (int)CLI.ExitCode.Success;
  }

  private static void _ShowStatistics(RuntimeStats stats) {
    Console.WriteLine();
    Console.WriteLine("Statistics");
    Console.WriteLine();
    Console.WriteLine( "         HardLinks  SymbolicLinks");
    Console.WriteLine($"Created  {stats.HardLinkStats.Created,9:N0}  {stats.SymbolicLinkStats.Created,13:N0}");
    Console.WriteLine($"Removed  {stats.HardLinkStats.Removed,9:N0}  {stats.SymbolicLinkStats.Removed,13:N0}");
    Console.WriteLine($"Deleted  {stats.HardLinkStats.Deleted,9:N0}  {stats.SymbolicLinkStats.Deleted,13:N0}");
    Console.WriteLine($"Seen     {stats.HardLinkStats.Seen,9:N0}  {stats.SymbolicLinkStats.Seen,13:N0}");
    Console.WriteLine();
    Console.WriteLine($"Folders Total : {stats.FolderCount:N0}");
    Console.WriteLine($"Files Total   : {stats.FileCount:N0}");
    Console.WriteLine($"Bytes Total   : {stats.BytesTotal:N0} ({FilesizeFormatter.FormatIEC(stats.BytesTotal,"N1")})");
  }

}
