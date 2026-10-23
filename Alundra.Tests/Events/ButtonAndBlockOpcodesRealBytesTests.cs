#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Alundra.Tests.Support.StoryChainOpcodeAudit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T1 (docs/plan-e20-heros.md section 1.24.6): the real bytes of the sites of the three opcodes in the export, the 20 reachable instructions of <c>0x72</c>, the 22 of <c>0xAA</c> and the 4 of <c>0xA9</c>
/// (<c>docs/plan-e20-annexe/e20-g5/g5b-disc/sites_all.json</c>; the 4 sites of <c>0x72</c> of the map 160 that no placement runs are left out). Each row checks the bytes in the exported map at the pc, then runs them
/// on a world deduced from the shape of the site (the model of <c>IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27</c>, M2 of E19.sm3): <c>0x72</c> copies Target onto Last; <c>0xAA [129]</c> and <c>0xA9 [129]</c> answer 1
/// when the hero is blocked by the logic entity (or blocks it) and 0 when there is no contact, <c>0xA9 [24]</c> (the room of Meia, map 440) answers on the hero as the logic entity and the entity of id 24. Each answer differs
/// from the 7 the test starts <c>Result</c> with, and the trace shows the opcode run as implemented with the size of the table. The exported project is required: the test fails, never skips, when it is missing (R-SM-4).
/// </summary>
public sealed class ButtonAndBlockOpcodesRealBytesTests
{
    internal sealed record Site(int Opcode, int Map, char Slot, int Index, int Pc, int Operand)
    {
        public int Size => Opcode == 0x72 ? 1 : 2;

        public string Id => $"0x{Opcode:X2}_m{Map}{Slot}{Index}@{Pc}";
    }

    private static IEnumerable<Site> AllSites()
    {
        yield return new Site(0x72, 40, 'F', 1, 1141, -1);
        yield return new Site(0x72, 40, 'F', 2, 1153, -1);
        yield return new Site(0x72, 40, 'F', 3, 1165, -1);
        yield return new Site(0x72, 40, 'F', 4, 1177, -1);
        yield return new Site(0x72, 51, 'F', 21, 856, -1);
        yield return new Site(0x72, 53, 'F', 7, 1378, -1);
        yield return new Site(0x72, 54, 'F', 39, 2243, -1);
        yield return new Site(0x72, 139, 'D', 9, 793, -1);
        yield return new Site(0x72, 140, 'D', 2, 2037, -1);
        yield return new Site(0x72, 140, 'D', 16, 2125, -1);
        yield return new Site(0x72, 159, 'D', 13, 1787, -1);
        yield return new Site(0x72, 160, 'D', 1, 1607, -1);
        yield return new Site(0x72, 160, 'D', 5, 1612, -1);
        yield return new Site(0x72, 199, 'F', 1, 1232, -1);
        yield return new Site(0x72, 394, 'F', 9, 1257, -1);
        yield return new Site(0x72, 394, 'F', 10, 1265, -1);
        yield return new Site(0x72, 444, 'D', 15, 1302, -1);
        yield return new Site(0x72, 444, 'D', 15, 1317, -1);
        yield return new Site(0x72, 446, 'D', 1, 1114, -1);
        yield return new Site(0x72, 446, 'D', 1, 1129, -1);
        yield return new Site(0xA9, 440, 'B', 9, 628, 24);
        yield return new Site(0xA9, 440, 'C', 16, 1755, 129);
        yield return new Site(0xA9, 440, 'C', 18, 1880, 129);
        yield return new Site(0xA9, 440, 'C', 21, 2000, 129);
        yield return new Site(0xAA, 159, 'C', 13, 1698, 129);
        yield return new Site(0xAA, 160, 'C', 1, 944, 129);
        yield return new Site(0xAA, 421, 'C', 8, 1467, 129);
        yield return new Site(0xAA, 421, 'C', 14, 1555, 129);
        yield return new Site(0xAA, 421, 'C', 17, 1603, 129);
        yield return new Site(0xAA, 423, 'C', 25, 971, 129);
        yield return new Site(0xAA, 423, 'C', 26, 1039, 129);
        yield return new Site(0xAA, 424, 'C', 3, 700, 129);
        yield return new Site(0xAA, 424, 'C', 4, 751, 129);
        yield return new Site(0xAA, 424, 'C', 5, 787, 129);
        yield return new Site(0xAA, 424, 'C', 6, 823, 129);
        yield return new Site(0xAA, 424, 'C', 9, 899, 129);
        yield return new Site(0xAA, 424, 'C', 15, 1039, 129);
        yield return new Site(0xAA, 424, 'C', 16, 1111, 129);
        yield return new Site(0xAA, 424, 'C', 17, 1175, 129);
        yield return new Site(0xAA, 425, 'C', 12, 402, 129);
        yield return new Site(0xAA, 426, 'C', 13, 1484, 129);
        yield return new Site(0xAA, 426, 'C', 14, 1551, 129);
        yield return new Site(0xAA, 426, 'C', 15, 1619, 129);
        yield return new Site(0xAA, 426, 'C', 16, 1687, 129);
        yield return new Site(0xAA, 426, 'C', 17, 1752, 129);
        yield return new Site(0xAA, 440, 'C', 4, 1140, 129);
    }

    public static IEnumerable<object[]> SiteIds() => AllSites().Select(s => new object[] { s.Id });

    [Fact]
    public void TheTableHolds_20Instructions0x72_22Instructions0xAA_4Instructions0xA9_AndTheRealRunnerPortsTheThree()
    {
        var sites = AllSites().ToList();

        Assert.Equal(46, sites.Count);
        Assert.Equal(46, sites.Select(s => s.Id).Distinct().Count());
        Assert.Equal(20, sites.Count(s => s.Opcode == 0x72));
        Assert.Equal(22, sites.Count(s => s.Opcode == 0xAA));
        Assert.Equal(4, sites.Count(s => s.Opcode == 0xA9));
        Assert.True(IsPorted(0x72) && IsPorted(0xA9) && IsPorted(0xAA)); // the oracle of the story-chain audit is the real runner: red while the three are skipped by their size.
    }

    [Theory]
    [MemberData(nameof(SiteIds))]
    public void TheRealBytesOfTheSite_AreTheOpcode_AndRunAsImplementedOnTheShapeOfTheSite(string id)
    {
        var site = AllSites().Single(s => s.Id == id);
        var (document, _) = LoadMap(SaveGameDirectorTestSupport.FindProjectRoot(), site.Map);

        Assert.Equal(site.Opcode, document.Codes[site.Pc]);
        if (site.Operand >= 0)
        {
            Assert.Equal(site.Operand, document.Codes[site.Pc + 1]);
        }

        var bytes = Enumerable.Range(0, site.Size).Select(i => document.Codes[site.Pc + i]).ToArray();

        switch (site.Opcode)
        {
            case 0x72:
            {
                var world = new OpcodeWorld();
                var entity = world.Add(3);
                entity.TargetAnimationId = 5;
                entity.TargetDirection = 9;
                entity.LastTargetAnimationId = 1;
                entity.LastTargetDirection = 2;

                var (state, trace) = world.Run(entity, OpcodeWorld.ResultBefore, bytes);

                Assert.Equal((5u, 9u), (entity.LastTargetAnimationId, entity.LastTargetDirection));
                Assert.Equal(OpcodeWorld.ResultBefore, state.Result);
                Assert.Equal(1, state.CodeIndex);
                OpcodeWorld.AssertImplemented(trace, 0x72, 1);
                break;
            }

            case 0xAA:
            {
                // The hero (the group) is blocked by the logic entity: 1; blocked by nothing: 0.
                var world = new OpcodeWorld();
                var logic = world.Add(4);
                world.Hero.XCollisionEntity = logic;
                var (blocked, blockedTrace) = world.Run(logic, OpcodeWorld.ResultBefore, bytes);
                world.Hero.XCollisionEntity = null;
                var (free, freeTrace) = world.Run(logic, OpcodeWorld.ResultBefore, bytes);

                Assert.Equal((1, 0), (blocked.Result, free.Result));
                Assert.Equal((2, 2), (blocked.CodeIndex, free.CodeIndex));
                OpcodeWorld.AssertImplemented(blockedTrace, 0xAA, 2);
                OpcodeWorld.AssertImplemented(freeTrace, 0xAA, 2);
                break;
            }

            case 0xA9 when site.Operand == 129:
            {
                // The logic entity is blocked by the hero (the group): 1; blocked by nothing: 0.
                var world = new OpcodeWorld();
                var logic = world.Add(4);
                logic.XCollisionEntity = world.Hero;
                var (blocked, blockedTrace) = world.Run(logic, OpcodeWorld.ResultBefore, bytes);
                logic.XCollisionEntity = null;
                var (free, freeTrace) = world.Run(logic, OpcodeWorld.ResultBefore, bytes);

                Assert.Equal((1, 0), (blocked.Result, free.Result));
                Assert.Equal((2, 2), (blocked.CodeIndex, free.CodeIndex));
                OpcodeWorld.AssertImplemented(blockedTrace, 0xA9, 2);
                OpcodeWorld.AssertImplemented(freeTrace, 0xA9, 2);
                break;
            }

            default:
            {
                // 0xA9 [24] of the program B of the map 440: the hero is the logic entity, the group is the raw id 24 (the young Meia).
                Assert.Equal((0xA9, 24), (site.Opcode, site.Operand));
                var world = new OpcodeWorld();
                var meia = world.Add(24);
                world.Hero.XCollisionEntity = meia;
                var (blocked, blockedTrace) = world.Run(world.Hero, OpcodeWorld.ResultBefore, bytes);
                world.Hero.XCollisionEntity = null;
                var (free, freeTrace) = world.Run(world.Hero, OpcodeWorld.ResultBefore, bytes);

                Assert.Equal((1, 0), (blocked.Result, free.Result));
                OpcodeWorld.AssertImplemented(blockedTrace, 0xA9, 2);
                OpcodeWorld.AssertImplemented(freeTrace, 0xA9, 2);
                break;
            }
        }
    }
}
