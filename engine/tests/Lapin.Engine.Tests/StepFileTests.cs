using System.Text;
using Lapin.Ifc;

namespace Lapin.Engine.Tests;

/// <summary>The STEP parser on bytes: strings decoded one by one, a large file parsed in parts on every core.</summary>
public class StepFileTests
{
    const string Head = "ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));\nENDSEC;\nDATA;\n";

    [Fact]
    public void Strings_are_UTF8_or_Latin1_each_on_its_own_with_quotes_and_escapes()
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes(Head));
        bytes.AddRange(Encoding.UTF8.GetBytes("#1=IFCLABEL('Pereți ''portanți''');\n"));
        // A Latin-1 string beside a UTF-8 one: each read as it is.
        bytes.AddRange(Encoding.ASCII.GetBytes("#2=IFCLABEL('"));
        bytes.AddRange(Encoding.Latin1.GetBytes("Straße"));
        bytes.AddRange(Encoding.ASCII.GetBytes("');\n#3=IFCLABEL('\\X2\\0103\\X0\\ and ;\n#9=x');\n#4=IFCDOOR(.T.,.notdefined.,$,*,#3,(1,2.5E-1,-3));\nENDSEC;\nEND-ISO-10303-21;\n"));
        var f = StepFile.Read([.. bytes]);
        Assert.Equal("IFC4", f.Schema);
        Assert.Equal("Pereți 'portanți'", f.Entities[1][0]);
        Assert.Equal("Straße", f.Entities[2][0]);
        Assert.Equal("ă and ;\n#9=x", f.Entities[3][0]);
        var door = f.Entities[4];
        Assert.Equal("IFCDOOR", door.Type);
        Assert.Equal("T", ((StepEnum)door[0]!).Value);
        Assert.Equal("NOTDEFINED", ((StepEnum)door[1]!).Value);
        Assert.Equal(new List<object?> { 1L, 0.25, -3L }, (List<object?>)door[5]!);
        Assert.Single(f.OfType("IFCDOOR"));
    }

    [Fact]
    public void A_large_file_parsed_in_parts_gives_every_entity_once_in_the_files_order()
    {
        var s = new StringBuilder(Head);
        const int n = 200_000;
        for (var i = 1; i <= n; i++)
            s.Append(i % 3 == 0 ? $"#{i}=IFCLABEL('a;\\n#{i}=b');\n" : $"#{i}=IFCCARTESIANPOINT(({i}.,{i * 0.5},0.));\n");
        s.Append("ENDSEC;\nEND-ISO-10303-21;\n");
        var bytes = Encoding.ASCII.GetBytes(s.ToString());
        Assert.True(bytes.Length > 8 << 20);
        var f = StepFile.Read(bytes);
        Assert.Equal(n, f.Entities.Count);
        Assert.Equal(Enumerable.Range(1, n), f.Entities.Keys);
        Assert.Equal(1000.0, ((List<object?>)f.Entities[1000][0]!)[0]);
        Assert.Equal("a;\\n#999=b", f.Entities[999][0]);
        Assert.Equal(n - n / 3, f.OfType("IFCCARTESIANPOINT").Count());
        // Each type's name made once.
        Assert.Same(f.Entities[1].Type, f.Entities[2].Type);
    }
}
