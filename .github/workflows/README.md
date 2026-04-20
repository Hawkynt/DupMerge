# CI/CD Pipeline — DupMerge

> Everything in this folder is the automated pipeline. Workflows live here, scripts live in `scripts/`.

## Files

| File                            | Trigger                             | Purpose                                 |
|---------------------------------|-------------------------------------|-----------------------------------------|
| `ci.yml`                        | push + PR + `workflow_call`         | Build + tests (win + linux)             |
| `release.yml`                   | tag push `v*`                       | GitHub Release (no NuGet — exe only)    |
| `nightly.yml`                   | CI success on `master`/`main`       | `nightly-YYYY-MM-DD` + GFS prune        |
| `_build.yml`                    | `workflow_call` (internal)          | net7.0 self-contained single-file build |
| `scripts/*`                     | invoked by workflows                | Shared version/changelog/prune tools    |

## Framework sibling checkout

DupMerge's csproj imports `..\..\Framework\VersionSpecificSymbols.Common.prop` and compiles in source files from `..\..\Framework\Corlib.Extensions\`. The workflows check out `Hawkynt/C--FrameworkExtensions` alongside DupMerge so the `..\..\Framework\` path resolves:

```
$WORKSPACE/
├── DupMerge/       <-- this repo (actions/checkout with path: DupMerge)
│   └── DupMerge/
│       └── DupMerge.csproj  --> ..\..\Framework\ resolves to siblings below
└── Framework/      <-- C--FrameworkExtensions (actions/checkout with path: Framework)
```

If FrameworkExtensions stops being available (renamed, archived), vendor the needed source files into this repo to break the external dep.

## Release artifacts

| Artifact                              | Produced by          |
|---------------------------------------|----------------------|
| `DupMerge-win-x64-<version>.zip`      | release + nightly    |
| `DupMerge-linux-x64-<version>.zip`    | release + nightly    |

Both are self-contained single-file net7.0 binaries.
