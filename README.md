# DupMerge

DupMerge is a powerful tool designed to manage duplicate files efficiently by creating or removing links to them. It offers a variety of options to customize the handling of duplicate files, including creating symbolic or hard links, setting file size limits for processing, and managing read-only attributes for linked files.

## Features

- **Efficient File Handling:** Quickly identifies duplicate files and creates links to them, reducing disk space usage.
- **Customizable Operations:** Offers options for creating symbolic links, deleting links, and removing links with file copies.
- **Flexible File Size Limits:** Allows specifying minimum and maximum file sizes for processing.
- **Multithreading Support:** Utilizes multiple threads for faster file processing, automatically adjusting to the number of CPU cores.
- **Read-Only Attribute Management:** Provides options to set or update the read-only attribute for linked files.
- **Versatile Linking Options:** Enables the creation of both hard links and symbolic links, providing flexibility in file management.

## Build from Source

DupMerge is developed in C# and can be compiled using the .NET SDK. To install and build DupMerge, you will need to have the .NET SDK installed on your machine. Follow these steps:

```batch
rem Clone the repository
git clone https://github.com/Hawkynt/DupMerge.git
rem Clone the C--FrameworkExtensions repository into a parallel directory named Framework, because we gonna need some files from it to build a monolithic executable
git clone https://github.com/Hawkynt/C--FrameworkExtensions.git Framework
rem Navigate to the project directory
cd DupMerge
rem Build the project using .NET CLI
dotnet build
```

This will compile the application and produce an executable within the `bin/` directory.

## Usage

```batch
DupMerge [<options>] [<directories>]
```

### Options

- `-v`, `--info`: Show information only, without making any changes.
- `-t <n>`, `--threads <n>`: Specify the number of threads to use for crawling. Defaults to the number of CPU cores or 8, whichever is less.
- `-m <n>`, `--minimum <n>`: Specify the minimum file size to process. Defaults to 1.
- `-M <n>`, `--maximum <n>`: Specify the maximum file size to process.
- `-s`, `--allow-symlink`: Allow creating symbolic links if a hardlink cannot be created.
- `-Dhl`, `--delete-hardlinks`: Delete all hard links.
- `-Dsl`, `--delete-symlinks`, `--delete-symboliclinks`: Delete all symbolic links.
- `-D`, `--delete`: Same as `-Dhl -Dsl`.
- `-Rhl`, `--remove-hardlinks`: Remove hard links and replace them with a copy of the linked file.
- `-Rsl`, `--remove-symlinks`, `--remove-symboliclinks`: Remove symbolic links and replace them with a copy of the linked file.
- `-R`, `--remove`: Same as `-Rhl -Rsl`.
- `-sro`, `--set-readonly`: Set the read-only attribute on newly created sym/hard-links.
- `-uro`, `--update-readonly`: Set the read-only attribute to existing sym/hard-links.
- `-ro`, `--readonly`: Same as `-sro -uro`.

### Examples

```batch
rem Scan the current directory for duplicates, show info only
DupMerge --info .

rem Create hardlinks for duplicates in multiple directories, using 4 threads
DupMerge.exe -t 4 C:\path\to\dir1 C:\path\to\dir2

rem Delete all symbolic links in the specified directory
DupMerge.exe --delete-symlinks C:\path\to\dir
```

## Contributing

We welcome contributions to DupMerge! If you have a bug report, feature request, or a patch, please feel free to submit an issue or pull request on GitHub. For more detailed information, please see our [CONTRIBUTING](CONTRIBUTING.md) file.

## License

This project is licensed under the GPL v2 License - see the [license](LICENSE) file for details.
