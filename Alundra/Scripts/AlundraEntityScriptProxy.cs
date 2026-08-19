using System;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;

namespace Alundra.Scripts;

public class AlundraEntityScriptProxy : GameplayProxy
{
    public bool IsLoadedNormalOrDeactivated =>
                Status is EntityStatus.Loaded or EntityStatus.Normal or EntityStatus.Deactivated;

    public int Index;
    public int Index2;
    public Entity? ChildEntity;
    public Entity? ParentEntity;
    public EntityStatus Status;//10
    public int Hp;
    public int HpMax;
    public int FrameCounter;//1c
    public Entity? BlockedByEntity;//20
    public int Flags2;//24
    public Entity? PlatformEntity; //28
    public Entity? CarriedEntity;
    public int RelativeWarpOffsetX;
    public int RelativeWarpOffsetY;
    public int RelativeWarpOffsetZ;
    public uint ContentsItemId; //3c
    public int ContentsGameFlag;
    //public SiEntityRecord? EntityRecord;
    public int EntityRefId;
    public readonly int[] ProgramIndexes = new int[6]; //4c
    //public SpriteRecord? SpriteRecord;
    public uint SpriteTableIndex;
    public uint Flags;//6c
    public readonly int[] SpriteProgramIndexes = new int[6]; //70
    public uint TargetAnimationId; //88
    public uint TargetDirection;
    public uint CurrentAnimationId;
    public uint CurrentDirection;
    public int AnimationDirection;
    //public AnimationSet? AnimationSet;
    //public SiFrame? FirstFrame;
    //public SiFrame? Frame;
    public int NextFrameDelay;
    public int ForceResetAnimationFlag;
    public int AnimCompleteCounter;
    public int AnimFlags;
    public int ForceZ;//rise/fall speed
    public int TargetForceX, TargetForceY;
    public int ForceX, ForceY;
    public int PreviousAdjustedForceX;//?cc
    public int PreviousAdjustedForceY;//?d0
    public int ForceStepX, ForceStepY;//d4,d8
    public int AdjustedForceX, AdjustedForceY;//dc,e0
    public int FinalForceX, FinalForceY, FinalForceZ;//e4,e8,ec
    public int Acceleration;//f0
    public int Speed;//f4
    public int IsZForceApplied;//this is probably named wrong, has to do with animation  f8
    public int ScreenClipX, ScreenClipY, ScreenClipZ;
    public int NegModX, NegModY, NegModZ;
    public int PosX; //114
    public int PosY;
    public int PosZ;
    public int TileX;
    public int TileY;
    public int TileZ;
    public Entity? RidingEntity; //12c
    public Entity? XCollisionEntity;
    public int FloorHeight;
    public int TerrainHeight;//
    public int ForceAdjusted;//0x13c
    public int CollidedWithEntityZ;//0x140
    public int IsOnGround;
    //public readonly MapTile[] MapTiles = new MapTile[4];
    public readonly int[] MapHeights = new int[4]; // 158
    public int PlatformUpdateFlag; //
    public int _16c;
    public int HitBoxOriginX;
    public int HitBoxOriginY;
    public int HitBoxOriginZ;
    public int _17c;
    public uint CombinedVramFlagsOR;
    public uint CombinedVramFlagsAND;
    public int TileAttributes; //188
    public int Slope_18c; //18c
    public int Slope_190; //190
    //public SpriteRef SpriteRef = new SpriteRef();//194
    //public int SpriteSheetOffset, PaletteOffset;
    //public SpriteEffect? ActiveEffect;
    public int ZUpperBound;//1bc
    public int RenderSortKey;//1c0
    //public BalanceRecord? BalanceRecord;//1c4
    //public BalanceAttack? CurrentAttack;//1c8
    public int DamagedTickCounter;//1cc
    public int FrameCollisionTickCounter;//1d0
    //public FrameCollisionData? FrameCollision;//1d4
    public int ModdedPosX, ModdedPosY, ModdedPosZ;
    public int ModX, ModY, ModZ;
    public int Width, Height, Depth;
    public int HitBoxX;//1fc
    public int HitBoxY;//200
    public int HitBoxZ;//204
    public int CollisionOffsetX;//208
    public int CollisionOffsetY;//20c
    public int CollisionOffsetZ;//210
    public int CollisionWidth;//214
    public int CollisionDepth;//218
    public int CollisionHeight;//21c
    public int HitCounter;//220
    public Entity? TouchingEntity;//224
    public int EventTrigger;//228
    public int MapEventProgramId;//22c
    public Entity LogicContextEntity; //self
    //public readonly EventProgramState EventProgramState = new();
    public uint LastTargetAnimationId;//26c
    public uint LastTargetDirection;//270
    public byte[] Bytes = new byte[4];
    public int DelayOrAngleOrEntityId; //278
    public int ItemState;//27C
    public short[] AIValues = new short[10];//280

    //public int AnimationFrameIndex;

    //public bool IsMapSprite { get; set; }

    //for debugging
    //public string? Name { get; set; }



    public override void InitializeWithWorld(World world)
    {
        throw new NotImplementedException();
    }

    public override void Update(float elapsedTime)
    {
        throw new NotImplementedException();
    }

    public override void Draw()
    {
        throw new NotImplementedException();
    }

    public override void OnHit(Collision collision)
    {
        throw new NotImplementedException();
    }

    public override void OnHitEnded(Collision collision)
    {
        throw new NotImplementedException();
    }

    public override void OnBeginPlay(World world)
    {
        throw new NotImplementedException();
    }

    public override void OnEndPlay(World world)
    {
        throw new NotImplementedException();
    }

    public override IGameplayProxy Clone()
    {
        throw new NotImplementedException();
    }
}
