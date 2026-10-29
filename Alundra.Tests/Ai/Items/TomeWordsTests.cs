#nullable enable
using System.Linq;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Entities;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1, phase A1, test (t1) (docs/plan-e20-heros.md section 1.31.6, D-H4D2-3, D-E20-55): the 32-bit accessors of the tomes to the word <c>+0x274</c> (<see cref="TomeWords"/>): a signed word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3.
/// </summary>
public sealed class TomeWordsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(0x01020304)]
    [InlineData(0x7FFFFFFF)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-256)]
    [InlineData(-65536)]
    [InlineData(int.MinValue)]
    [InlineData(unchecked((int)0xFFFD0000))]
    public void T1_AWordWritten_IsReadBack_WhateverItsSign(int value)
    {
        var entity = new EntityScriptProxy();

        TomeWords.Write274(entity, value);

        Assert.Equal(value, TomeWords.Read274(entity));
    }

    [Fact]
    public void T1_TheWordIsLittleEndian_TheByte0IsTheLowest()
    {
        var entity = new EntityScriptProxy();

        TomeWords.Write274(entity, 0x01020304);

        Assert.Equal(new byte[] { 0x04, 0x03, 0x02, 0x01 }, entity.Bytes);
        entity.Bytes[0] = 0xFF;
        entity.Bytes[1] = 0x00;
        entity.Bytes[2] = 0x00;
        entity.Bytes[3] = 0x00;
        Assert.Equal(255, TomeWords.Read274(entity));
    }

    [Fact]
    public void T1_TheHighBytes_AreReadAndWritten_TheByte3IsTheSign()
    {
        var entity = new EntityScriptProxy();

        TomeWords.Write274(entity, unchecked((int)0x80000000));
        Assert.Equal(new byte[] { 0, 0, 0, 0x80 }, entity.Bytes);
        Assert.Equal(int.MinValue, TomeWords.Read274(entity));

        TomeWords.Write274(entity, -1);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, entity.Bytes);

        entity.Bytes[0] = 0x00;
        entity.Bytes[1] = 0x00;
        entity.Bytes[2] = 0x01;
        entity.Bytes[3] = 0x00;
        Assert.Equal(65536, TomeWords.Read274(entity));
        entity.Bytes[3] = 0x01;
        Assert.Equal(0x01010000, TomeWords.Read274(entity));
        entity.Bytes[3] = 0xFF;
        Assert.Equal(unchecked((int)0xFF010000), TomeWords.Read274(entity));
    }

    [Fact]
    public void T1_AWriteReplacesTheFourBytes_AndTouchesNoOtherEntity()
    {
        var entity = new EntityScriptProxy();
        var other = new EntityScriptProxy();
        TomeWords.Write274(entity, -1);
        TomeWords.Write274(other, 0x11223344);

        TomeWords.Write274(entity, 16);

        Assert.Equal(new byte[] { 16, 0, 0, 0 }, entity.Bytes); // the high bytes of the old value are gone.
        Assert.Equal(0x11223344, TomeWords.Read274(other));
        Assert.Equal(4, entity.Bytes.Length);
        Assert.All(new EntityScriptProxy().Bytes, b => Assert.Equal(0, b)); // a new entity holds a zero word.
        Assert.Equal(0, TomeWords.Read274(new EntityScriptProxy()));
    }

    [Fact]
    public void T1_TheWordDoesNotShareTheStorageWithAClone()
    {
        var entity = new EntityScriptProxy();
        TomeWords.Write274(entity, 7);

        var clone = (EntityScriptProxy)entity.Clone();
        TomeWords.Write274(clone, 9);

        Assert.Equal((7, 9), (TomeWords.Read274(entity), TomeWords.Read274(clone)));
        Assert.True(clone.Bytes.SequenceEqual(new byte[] { 9, 0, 0, 0 }));
    }
}
