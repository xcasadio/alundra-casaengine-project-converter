using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

public class AlundraEntityScriptProxyCloneTests
{
    [Fact]
    public void Clone_CopiesScalarFields()
    {
        var original = new AlundraEntityScriptProxy
        {
            EntityRefId = 7,
            SpriteTableIndex = 25u,
            Status = EntityStatus.Normal,
            Hp = 42,
            PosX = 100,
            PosY = 200,
            PosZ = 300,
        };

        var clone = (AlundraEntityScriptProxy)original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.EntityRefId, clone.EntityRefId);
        Assert.Equal(original.SpriteTableIndex, clone.SpriteTableIndex);
        Assert.Equal(original.Status, clone.Status);
        Assert.Equal(original.Hp, clone.Hp);
        Assert.Equal(original.PosX, clone.PosX);
        Assert.Equal(original.PosY, clone.PosY);
        Assert.Equal(original.PosZ, clone.PosZ);
    }

    [Fact]
    public void Clone_CopiesArrayContentsButArraysAreNotReferenceEqual()
    {
        var original = new AlundraEntityScriptProxy();
        original.ProgramIndexes[3] = 99;
        original.SpriteProgramIndexes[1] = 12;
        original.MapHeights[2] = 5;
        original.Bytes[0] = 7;
        original.AIValues[4] = 3;

        var clone = (AlundraEntityScriptProxy)original.Clone();

        Assert.Equal(original.ProgramIndexes, clone.ProgramIndexes);
        Assert.NotSame(original.ProgramIndexes, clone.ProgramIndexes);

        Assert.Equal(original.SpriteProgramIndexes, clone.SpriteProgramIndexes);
        Assert.NotSame(original.SpriteProgramIndexes, clone.SpriteProgramIndexes);

        Assert.Equal(original.MapHeights, clone.MapHeights);
        Assert.NotSame(original.MapHeights, clone.MapHeights);

        Assert.Equal(original.Bytes, clone.Bytes);
        Assert.NotSame(original.Bytes, clone.Bytes);

        Assert.Equal(original.AIValues, clone.AIValues);
        Assert.NotSame(original.AIValues, clone.AIValues);
    }

    [Fact]
    public void Clone_MutatingCloneArrayDoesNotAffectOriginal()
    {
        var original = new AlundraEntityScriptProxy();
        original.ProgramIndexes[0] = 1;

        var clone = (AlundraEntityScriptProxy)original.Clone();
        clone.ProgramIndexes[0] = 999;

        Assert.Equal(1, original.ProgramIndexes[0]);
        Assert.Equal(999, clone.ProgramIndexes[0]);
    }
}
