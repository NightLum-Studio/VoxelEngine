using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core;
using VoxelEngine.Data;

namespace VoxelEngine.Generation
{
    [BurstCompile]
    public struct GenerateChunkVoxelsJob : IJob
    {
        [WriteOnly]
        public NativeArray<byte> voxels; // BlockId per voxel; 0 = Air

        public int3 chunkCoord;
        public int seed;

        public void Execute()
        {
            var rng = new Unity.Mathematics.Random((uint)seed + 1u);
            int width = ChunkDefs.ChunkSizeXZ;
            int height = ChunkDefs.ChunkSizeY;

            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int wx = chunkCoord.x * width + x;
                    int wz = chunkCoord.z * width + z;

                    float elevation = fbm(wx, wz, seed);
                    int surfaceY = (int)math.clamp(math.floor(elevation * (height - 1)), 1, height - 2);

                    for (int y = 0; y < height; y++)
                    {
                        int index = x + z * width + y * width * width;
                        if (y > surfaceY)
                        {
                            voxels[index] = (byte)BlockId.Air;
                        }
                        else if (y == surfaceY)
                        {
                            voxels[index] = (byte)BlockId.Grass;
                        }
                        else if (y > surfaceY - 4)
                        {
                            voxels[index] = (byte)BlockId.Dirt;
                        }
                        else
                        {
                            voxels[index] = (byte)BlockId.Stone;
                        }
                    }
                }
            }
        }

        private static float fbm(int x, int z, int seed)
        {
            float2 p = new float2(x, z) * 0.01f + new float2(seed * 0.123f, seed * 0.456f);
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float norm = 0f;
            for (int i = 0; i < 5; i++)
            {
                sum += noise.snoise(p * frequency) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            sum /= math.max(norm, 1e-5f);
            return math.saturate(0.5f + 0.5f * sum);
        }
    }
}

