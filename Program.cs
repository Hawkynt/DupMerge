using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Classes;
using Libraries;

namespace DupMerge {
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
      DuplicateFileMerger.ProcessFolders(directories, configuration);
      Console.WriteLine();
      Console.WriteLine("Statistics");
      Console.WriteLine();
      Console.WriteLine( "         HardLinks  SymbolicLinks");
      Console.WriteLine($"Created  {configuration.HardLinkStats.Created,9:N0}  {configuration.SymbolicLinkStats.Created,13:N0}");
      Console.WriteLine($"Removed  {configuration.HardLinkStats.Removed,9:N0}  {configuration.SymbolicLinkStats.Removed,13:N0}");
      Console.WriteLine($"Deleted  {configuration.HardLinkStats.Deleted,9:N0}  {configuration.SymbolicLinkStats.Deleted,13:N0}");
      Console.WriteLine($"Seen     {configuration.HardLinkStats.Seen,9:N0}  {configuration.SymbolicLinkStats.Seen,13:N0}");
      Console.WriteLine();
      Console.WriteLine($"Folders Total : {configuration.FolderCount:N0}");
      Console.WriteLine($"Files Total   : {configuration.FileCount:N0}");
      Console.WriteLine($"Bytes Total   : {configuration.BytesTotal:N0} ({FilesizeFormatter.FormatIEC(configuration.BytesTotal,"N1")})");


#if DEBUG
      Console.WriteLine("READY.");
      Console.ReadKey();
#endif

      return (int)CLI.ExitCode.Success;
    }

  }
}