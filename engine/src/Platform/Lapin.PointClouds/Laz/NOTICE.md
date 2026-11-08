# LAZ decoder: notice

`Arithmetic.cs`, `Items.cs` and `LazDecoder.cs` are a C# port, decoding only, of parts of **laz-rs** 0.9.3
(https://github.com/laz-rs/laz-rs), licensed under the Apache License, Version 2.0:

    Copyright (c) 2019-2023, Thomas Montaigu
    Copyright (c) 2007-2015, martin isenburg, rapidlasso - fast tools to catch reality

laz-rs is itself a port of LASzip's coder and point items. Ported: the arithmetic decoder and its models, the integer
decompressor, the point items Point10, GpsTime, RGB12 and Byte (version 2), Point14 and RGB14 (version 3, only the
layers Lapin draws), the LASzip VLR and the chunk table. Changed from laz-rs: written in C#, a decoder reads zeros past
its bytes, layered items skip the layers Lapin does not use. The port is distributed under Lapin's MIT licence with this
notice kept, as the Apache License 2.0 allows (its section 4); a copy of that licence is at
http://www.apache.org/licenses/LICENSE-2.0.

LASzip itself (LGPL) is not used.
