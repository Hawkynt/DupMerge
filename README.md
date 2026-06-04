# 🚀 DupMerge

[![License](https://img.shields.io/github/license/Hawkynt/DupMerge)](https://github.com/Hawkynt/DupMerge/blob/master/LICENSE)
[![Language](https://img.shields.io/github/languages/top/Hawkynt/DupMerge?color=8957D5)](https://github.com/Hawkynt/DupMerge)

[![CI](https://github.com/Hawkynt/DupMerge/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/Hawkynt/DupMerge/actions/workflows/ci.yml)
![Last Commit](https://img.shields.io/github/last-commit/Hawkynt/DupMerge?branch=master)
![Activity](https://img.shields.io/github/commit-activity/m/Hawkynt/DupMerge)

[![Stars](https://img.shields.io/github/stars/Hawkynt/DupMerge?color=FFD700)](https://github.com/Hawkynt/DupMerge/stargazers)
[![Forks](https://img.shields.io/github/forks/Hawkynt/DupMerge?color=008080)](https://github.com/Hawkynt/DupMerge/network/members)
[![Issues](https://img.shields.io/github/issues/Hawkynt/DupMerge)](https://github.com/Hawkynt/DupMerge/issues)
![Code Size](https://img.shields.io/github/languages/code-size/Hawkynt/DupMerge?color=4CAF50)
![Repo Size](https://img.shields.io/github/repo-size/Hawkynt/DupMerge?color=FF9800)

[![Release](https://img.shields.io/github/v/release/Hawkynt/DupMerge?sort=semver)](https://github.com/Hawkynt/DupMerge/releases/latest)
[![Nightly](https://img.shields.io/github/v/release/Hawkynt/DupMerge?include_prereleases=true&sort=date&label=nightly&color=FF9800)](https://github.com/Hawkynt/DupMerge/releases)
[![Downloads](https://img.shields.io/github/downloads/Hawkynt/DupMerge/total)](https://github.com/Hawkynt/DupMerge/releases)

> Tired of duplicate files cluttering up your disk space? DupMerge to the rescue! 🦸‍♂️ This powerful tool helps you manage duplicate files efficiently by creating or removing links to them. Customize how it handles duplicates with a variety of options, from creating symbolic or hard links to setting file size limits and managing read-only attributes.

## ✨ Features

-   🚀 **Efficient File Handling:** Quickly identifies duplicate files and creates links to them, saving valuable disk space.
-   🔧 **Customizable Operations:** Offers a range of options for creating symbolic links, deleting links, and even replacing links with file copies.
-   🔩 **Flexible File Size Limits:** Allows you to specify minimum and maximum file sizes for processing, so you only handle the files you want.
-   ⚡ **Multithreading Support:** Utilizes multiple threads for faster file processing, automatically adjusting to your CPU cores for optimal performance.
-   🔒 **Read-Only Attribute Management:** Provides options to set or update the read-only attribute for linked files.
-   🔗 **Versatile Linking Options:** Enables the creation of both hard links and symbolic links, giving you flexibility in how you manage your files.

## 🛠️ Build from Source

DupMerge is developed in C# and can be compiled using the .NET SDK. To get started, you'll need the .NET SDK installed on your machine.

```batch
# Clone the repository
git clone https://github.com/Hawkynt/DupMerge.git

# C--FrameworkExtensions is needed for building a monolithic executable.
# Clone it into a parallel directory named Framework.
git clone https://github.com/Hawkynt/C--FrameworkExtensions.git Framework

# Navigate to the project directory
cd DupMerge

# Build the project
dotnet build
```

This will compile the application and place the executable in the `bin/` directory.

## 💻 Usage

```batch
DupMerge [<options>] [<directories>]
```

### Options

-   `-v`, `--info`: Show information only, without making any changes.
-   `-t <n>`, `--threads <n>`: Specify the number of threads to use for crawling. Defaults to the number of CPU cores (up to 8).
-   `-m <n>`, `--minimum <n>`: Specify the minimum file size to process (default: 1).
-   `-M <n>`, `--maximum <n>`: Specify the maximum file size to process.
-   `-s`, `--allow-symlink`: Allow creating symbolic links if a hardlink cannot be created.
-   `-Dhl`, `--delete-hardlinks`: Delete all hard links.
-   `-Dsl`, `--delete-symlinks`, `--delete-symboliclinks`: Delete all symbolic links.
-   `-D`, `--delete`: Same as `-Dhl -Dsl`.
-   `-Rhl`, `--remove-hardlinks`: Remove hard links and replace them with a copy of the linked file.
-   `-Rsl`, `--remove-symlinks`, `--remove-symboliclinks`: Remove symbolic links and replace them with a copy of the linked file.
-   `-R`, `--remove`: Same as `-Rhl -Rsl`.
-   `-sro`, `--set-readonly`: Set the read-only attribute on newly created sym/hard-links.
-   `-uro`, `--update-readonly`: Set the read-only attribute to existing sym/hard-links.
-   `-ro`, `--readonly`: Same as `-sro -uro`.

### Examples 💡

```batch
# Scan the current directory for duplicates and show info only
DupMerge --info .

# Create hardlinks for duplicates in multiple directories, using 4 threads
DupMerge.exe -t 4 C:\path\to\dir1 C:\path\to\dir2

# Delete all symbolic links in the specified directory
DupMerge.exe --delete-symlinks C:\path\to\dir
```

## 🤝 Contributing

We welcome contributions to DupMerge! If you have a bug report, feature request, or a patch, please feel free to submit an issue or pull request on GitHub.

## 📜 License

This project is licensed under the LGPL-3.0-or-later License - see the [LICENSE](LICENSE) file for details.