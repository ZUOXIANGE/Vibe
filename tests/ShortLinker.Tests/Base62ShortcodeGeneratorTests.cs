using ShortLinker.Api.Services;

namespace ShortLinker.Tests;

public class Base62ShortcodeGeneratorTests
{
    [Fact]
    public void Should_Generate_6_Character_String()
    {
        var generator = new Base62ShortcodeGenerator();
        var code = generator.Generate();
        Assert.Equal(6, code.Length);
    }
}