using GoshehYar.Core.Localization;

namespace GoshehYar.Tests;

public class LocalizationMergeTests
{
    [Fact]
    public void No_key_is_defined_twice()
    {
        Assert.Empty(Loc.DuplicateKeys);
    }
}
