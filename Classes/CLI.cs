#nullable enable
using System;
using System.Collections.Generic;

namespace Classes;

internal static class CLI {
  
  public enum ExitCode {
    Success = 0,
    DirectoryNotFound = -1,
  }

  /// <summary>
  /// Processes all command line switches thus setting apropriate properties in the configuration.
  /// </summary>
  /// <param name="switches">The switches.</param>
  /// <param name="configuration">The configuration.</param>
  public static void ProcessCommandLine(IEnumerable<string> switches, Configuration configuration) {
    foreach (var @switch in switches) {
      var index = @switch.IndexOf('=');

      var name = @switch;
      string? value = null;
      if (index >= 0) {
        value = @switch[(index + 1)..];
        name = @switch[..index];
      }

      switch (name) {
        case "/?":
        case "-H":
        case "--help": {
            Console.WriteLine(@"
DupMerge (c)2018-2024 Hawkynt
Creates or removes links to duplicate files.
Usage: DupMerge [<options>] [<directories>]
  Options:
  -v    , --info
      Shows info only
  -t <n>, --threads <n>
      Specifies the number of threads to use for crawling - defaults to number of CPU cores or 8 - whatever is less
  -m <n>, --minimum <n>
      Specifies the minimum file size to process, defaults to 1
  -M <n>, --maximum <n>
      Specifies the maximum file size to process

  -s    , --allow-symlink
      Allows creating symbolic links in case a hardlink could not be created
  
  -Dhl  , --delete-hardlinks
      Deletes all hard links
  -Dsl  , --delete-symlinks, --delete-symboliclinks
      Deletes all symbolic links
  -D    , --delete
      Sames as -Dhl -Dsl

  -Rhl  , --remove-hardlinks
      Removes hard links and replaces them with a copy of the linked file
  -Rsl  , --remove-symlinks, --remove-symboliclinks
      Removes symbolic links and replaces them with a copy of the linked file
  -R    , --remove
      Same as -Rhl -Rsl

  -sro  , --set-readonly
      Sets read-only attribute on newly created sym/hard-links
  -uro  , --update-readonly
      Sets read-only attribute to existing sym/hard-links
  -ro   , --readonly
      Same as -sro -uro
          ");
            Environment.Exit(0);
            break;
          }
        case "-v":
        case "--info": {
            configuration.ShowInfoOnly = true;
            break;
          }
        case "-t" when value is not null:
        case "--threads" when value is not null: {
            configuration.MaximumCrawlerThreads = int.Parse(value);
            break;
          }
        case "-m" when value is not null:
        case "--minimum" when value is not null: {
            configuration.MinimumFileSizeInBytes = long.Parse(value);
            break;
          }
        case "-M" when value is not null:
        case "--maximum" when value is not null: {
            configuration.MaximumFileSizeInBytes = long.Parse(value);
            break;
          }
        case "-s":
        case "--allow-symlink": {
            configuration.AlsoTrySymbolicLinks = true;
            break;
          }
        case "-D":
        case "--delete": {
            configuration.DeleteHardLinkedFiles = true;
            configuration.DeleteSymbolicLinkedFiles = true;
            break;
          }
        case "-Dhl":
        case "--delete-hardlinks": {
            configuration.DeleteHardLinkedFiles = true;
            break;
          }
        case "-Dsl":
        case "--delete-symlinks":
        case "--delete-symboliclinks": {
            configuration.DeleteSymbolicLinkedFiles = true;
            break;
          }
        case "-R":
        case "--remove": {
            configuration.RemoveHardLinks = true;
            configuration.RemoveSymbolicLinks = true;
            break;
          }
        case "-Rhl":
        case "--remove-hardlinks": {
            configuration.RemoveHardLinks = true;
            break;
          }
        case "-Rsl":
        case "--remove-symlinks":
        case "--remove-symboliclinks": {
            configuration.RemoveSymbolicLinks = true;
            break;
          }
        case "-sro":
        case "--set-readonly": {
            configuration.SetReadOnlyAttributeOnNewHardLinks = true;
            configuration.SetReadOnlyAttributeOnNewSymbolicLinks = true;
            break;
          }
        case "-uro":
        case "--update-readonly": {
            configuration.SetReadOnlyAttributeOnExistingHardLinks = true;
            configuration.SetReadOnlyAttributeOnExistingSymbolicLinks = true;
            break;
          }
        case "-ro":
        case "--readonly": {
            configuration.SetReadOnlyAttributeOnNewHardLinks = true;
            configuration.SetReadOnlyAttributeOnNewSymbolicLinks = true;
            configuration.SetReadOnlyAttributeOnExistingHardLinks = true;
            configuration.SetReadOnlyAttributeOnExistingSymbolicLinks = true;
            break;
          }
      }
    }
  }
}
