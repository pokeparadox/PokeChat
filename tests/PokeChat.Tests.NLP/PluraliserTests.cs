using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class PluraliserTests
{
    [Fact]
    public void RegularPlural_StripsS()
    {
        Pluraliser.ToSingular("trains").ShouldBe("train");
    }

    [Fact]
    public void PluralEndingInEs_StripsEs()
    {
        Pluraliser.ToSingular("boxes").ShouldBe("box");
        Pluraliser.ToSingular("watches").ShouldBe("watch");
        Pluraliser.ToSingular("buses").ShouldBe("bus");
    }

    [Fact]
    public void PluralEndingInIes_ConvertsToY()
    {
        Pluraliser.ToSingular("berries").ShouldBe("berry");
        Pluraliser.ToSingular("cities").ShouldBe("city");
    }

    [Fact]
    public void PluralEndingInVes_ConvertsToF()
    {
        Pluraliser.ToSingular("knives").ShouldBe("knif");
    }

    [Fact]
    public void IrregularPlural_ReturnsSingular()
    {
        Pluraliser.ToSingular("children").ShouldBe("child");
        Pluraliser.ToSingular("men").ShouldBe("man");
        Pluraliser.ToSingular("women").ShouldBe("woman");
        Pluraliser.ToSingular("people").ShouldBe("person");
        Pluraliser.ToSingular("teeth").ShouldBe("tooth");
        Pluraliser.ToSingular("feet").ShouldBe("foot");
    }

    [Fact]
    public void ShortWordEndingInS_ReturnsNull()
    {
        Pluraliser.ToSingular("is").ShouldBeNull();
        Pluraliser.ToSingular("as").ShouldBeNull();
        Pluraliser.ToSingular("us").ShouldBeNull();
    }

    [Fact]
    public void AlreadySingular_ReturnsNull()
    {
        Pluraliser.ToSingular("train").ShouldBeNull();
        Pluraliser.ToSingular("cat").ShouldBeNull();
    }

    [Fact]
    public void AmbiguousEndingS_ReturnsCandidate()
    {
        Pluraliser.ToSingular("this").ShouldBe("thi");
        Pluraliser.ToSingular("bus").ShouldBe("bu");
    }
}
