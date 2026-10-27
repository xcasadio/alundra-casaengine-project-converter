"""Read-only MODEL of the E20.h3b change (commit 161ac276) on the carried object's sort coordinate.
Before: FollowCarrier -> PushLogicalPositionToRoot()      -> root = (PosX >> 16, PosY >> 16, PosZ >> 16)            (EntitySpawnFactory.ResolveLogicalPosition)
After : FollowCarrier -> PushLogicalPositionToRoot(true)  -> root = (PosX / 65536f, PosY / 65536f, PosZ >> 16)     (EntityScriptProxy.cs:2631-2638)
Render: RenderProjectionComponent.UpdateProjection with SnapToPixel (true for every spawned entity, EntitySpawnFactory.cs:766): DeriveRenderPosition = (X, -(Y - Z), 0) then
SnapRenderPosition = (floor(X), ceil(renderY), 0)  (SimulationSpacePolicy.cs TopDownElevationSimulationSpacePolicy).
Sort coordinate = round(-renderY * 100) (DepthSortable2DComponent TopDownYUp, sort anchor 0). float32 emulated with struct."""
import math
import random
import struct


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def coordinate(root_y, root_z):
    render_y = f32(-f32(f32(root_y) - f32(root_z)))
    snapped = math.ceil(render_y)
    return int(round(f32(-snapped * 100.0)))


def pre(pos_y, pos_z):
    return coordinate(float(pos_y >> 16), float(pos_z >> 16))


def post(pos_y, pos_z):
    return coordinate(f32(f32(float(pos_y)) / f32(65536.0)), float(pos_z >> 16))


random.seed(7)
n = 200000
diff = 0
big = 0
for _ in range(n):
    py = random.randrange(0, 944 * 65536)        # up to the last row bucket (59 rows of 16 px)
    pz = random.randrange(0, 80 * 65536)
    if pre(py, pz) != post(py, pz):
        diff += 1
        if diff <= 3:
            print("diff example: PosY=%d (%.6f px) PosZ=%d pre=%d post=%d" % (py, py / 65536, pz, pre(py, pz), post(py, pz)))
print("random (PosY, PosZ) pairs:", n, " coordinate differs between the h3b states:", diff)
# the case of the report: hero at 206.09375 px, carried object at Z = 32 px
py, pz = int(206.09375 * 65536), 32 << 16
print("PosY=206.09375 px, Z=32 px: pre=%d post=%d (hero at Z=0: %d)" % (pre(py, pz), post(py, pz), post(py, 0)))
