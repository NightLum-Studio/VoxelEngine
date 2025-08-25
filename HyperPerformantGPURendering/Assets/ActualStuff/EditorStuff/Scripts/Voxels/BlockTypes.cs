using System;
using System.Runtime.InteropServices;

namespace HyperVoxel
{
    public enum BlockId : byte
    {
        Air = 0,
        Grass = 1,
        Dirt = 2,
        Stone = 3,
        Sand = 4,
        Water = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BlockUVTriplet
    {
        public uint topTileIndex;
        public uint sideTileIndex;
        public uint bottomTileIndex;
    }

    public static class BlockDatabase
    {
        public static int AtlasTilesPerRow = 16;

        private static readonly BlockUVTriplet[] s_BlockFaceTiles = new BlockUVTriplet[]
        {
            new BlockUVTriplet { topTileIndex = 0, sideTileIndex = 0, bottomTileIndex = 0 }, // Air
            new BlockUVTriplet { topTileIndex = 0, sideTileIndex = 1, bottomTileIndex = 2 }, // Grass
            new BlockUVTriplet { topTileIndex = 2, sideTileIndex = 2, bottomTileIndex = 2 }, // Dirt
            new BlockUVTriplet { topTileIndex = 3, sideTileIndex = 3, bottomTileIndex = 3 }, // Stone
            new BlockUVTriplet { topTileIndex = 4, sideTileIndex = 4, bottomTileIndex = 4 }, // Sand
            new BlockUVTriplet { topTileIndex = 5, sideTileIndex = 5, bottomTileIndex = 5 }, // Water
        };

        public static int BlockCount => s_BlockFaceTiles.Length;

        public static BlockUVTriplet Get(BlockId id)
        {
            int idx = (int)id;
            if (idx < 0 || idx >= s_BlockFaceTiles.Length) return default;
            return s_BlockFaceTiles[idx];
        }

        public static BlockUVTriplet[] GetAll()
        {
            var copy = new BlockUVTriplet[s_BlockFaceTiles.Length];
            Array.Copy(s_BlockFaceTiles, copy, s_BlockFaceTiles.Length);
            return copy;
        }
    }
}


