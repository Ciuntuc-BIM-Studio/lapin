# Speckle connector: third-party notices

The Speckle connector (module `speckle`, docs/plan-speckle.md) ships these libraries beside it, unchanged, as NuGet
packages. Each keeps its own licence; none is copyleft. `scripts/check-licenses.mjs` checks every one of them.

| Package | Version | Licence | Source |
|---|---|---|---|
| Speckle.Sdk (with the object model formerly Speckle.Objects) | 2026.9.0 | Apache-2.0 | https://github.com/specklesystems/speckle-sharp-sdk |
| Speckle.Sdk.Dependencies | 2026.9.0 | Apache-2.0; merged into it (ILRepack): Polly 7.2.3, Polly.Contrib.WaitAndRetry 1.1.1, Polly.Extensions.Http 3.0.0 (BSD-3-Clause, Copyright (c) App vNext), Open.ChannelExtensions 9.1.0 (MIT), System.Threading.Channels and Microsoft.Extensions.ObjectPool (MIT, .NET Foundation) | https://github.com/specklesystems/speckle-sharp-sdk |
| Speckle.Sdk.Parquet | 2026.9.0 | Apache-2.0; merged into it: Parquet.Net 5.3.0 (MIT), IronCompress 1.6.3 (Apache-2.0), Snappier 1.1.6 (BSD-3-Clause), ZstdSharp.Port 0.8.1 (MIT), Microsoft.IO.RecyclableMemoryStream 3.0.1 (MIT) | https://github.com/specklesystems/speckle-sharp-sdk |
| Speckle.Newtonsoft.Json | 13.0.2 | MIT | https://github.com/specklesystems/Newtonsoft.Json |
| Speckle.DoubleNumerics | 4.1.0 | MIT (see below) | https://github.com/specklesystems/Speckle.DoubleNumerics |
| GraphQL.Client, GraphQL.Client.Abstractions, GraphQL.Client.Abstractions.Websocket, GraphQL.Primitives | 6.1.0 | MIT | https://github.com/graphql-dotnet/graphql-client |
| Microsoft.Data.Sqlite, Microsoft.Data.Sqlite.Core, Microsoft.Extensions.* | 10.0.0 | MIT | https://github.com/dotnet/efcore, https://github.com/dotnet/runtime |
| SQLitePCLRaw.bundle_e_sqlite3, .core, .lib.e_sqlite3, .provider.e_sqlite3 | 2.1.14 | Apache-2.0 (SQLite itself is in the public domain) | https://github.com/ericsink/SQLitePCL.raw |
| System.Reactive | 6.0.0 | MIT | https://github.com/dotnet/reactive |

Apache-2.0: http://www.apache.org/licenses/LICENSE-2.0. None of the Apache-2.0 packages above carries a NOTICE file
of its own (checked in the packages of these versions). What the two merged assemblies hold was read from their
projects' `packages.lock.json` at the SDK's commit 994761589c7a4d8a405ccbd3ee02ad3b1f048252, each licence from its
package on nuget.org.

## Speckle.DoubleNumerics

Its package declares no licence. Its source, https://github.com/specklesystems/Speckle.DoubleNumerics (the package's
commit eff10f3469d3389b67eaf69286aa054806bb0f52 is on its master branch), is a fork of
https://github.com/Weingartner/System.Numerics.DoubleVectors and carries this licence (its `LICENSE`, read on 23.11.2026):

    MIT License

    Copyright (c) 2016 Weingartner

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.

## The desktop shell's secure store

The token is kept by the desktop shell (desktop/src-tauri) through the `keyring` crate 3.6 (MIT OR Apache-2.0) and the
crates it brings (zbus, secret-service and RustCrypto's, all MIT or Apache-2.0).
