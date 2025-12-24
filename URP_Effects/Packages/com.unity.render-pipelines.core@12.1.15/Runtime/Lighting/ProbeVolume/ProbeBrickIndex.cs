//#define USE_INDEX_NATIVE_ARRAY
using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.Collections;
using Chunk = UnityEngine.Experimental.Rendering.ProbeBrickPool.BrickChunkAlloc;
using RegId = UnityEngine.Experimental.Rendering.ProbeReferenceVolume.RegId;

namespace UnityEngine.Experimental.Rendering
{
    // 一个自适应八叉树结构，存储和管理不同分辨率的probe volume数量,高细节的地方使用小格子，低细节的地方使用大格子
    // bricks被划分到chunks当中，减少内存，chunck是一个逻辑区域，每个区域管理243个brick的状态(3种)
    internal class ProbeBrickIndex
    {
        // a few constants
        // 一个brick下最大的细分级别，
        internal const int kMaxSubdivisionLevels = 7; // 3 bits
        // 3进制编码，每个八叉树节点有3中状态，0-没有brick;1-有1个brick;2-多个brick，需继续细分
        // 243 = 3^5；5层足够编码7级八叉树，这里不是所有的分支，而是某一个7层的分支
        // 每个chunk包含243个brick?
        internal const int kIndexChunkSize = 243;

        // 位数组，存储brick的存在状态
        BitArray m_IndexChunks;
        // 在chunk中的索引
        int m_IndexInChunks;
        // 下一个空闲chunk的位置
        int m_NextFreeChunk;
        // GPU端的索引buffer
        ComputeBuffer m_PhysicalIndexBuffer;
        // CPU端的索引数据，用于传递brick的chunk id和层级
        int[] m_PhysicalIndexBufferData;

        internal int estimatedVMemCost { get; private set; }

        [DebuggerDisplay("Brick [{position}, {subdivisionLevel}]")]
        [Serializable]

        // 八叉树的一个基本存储单元
        public struct Brick : IEquatable<Brick>
        {
            // 位置坐标
            public Vector3Int position;   // refspace index, indices are cell coordinates at max resolution
            // brick在cell中的哪一个细分层级，用于计算一个cell单元包含多少个brick
            public int subdivisionLevel;              // size as factor covered elementary cells

            internal Brick(Vector3Int position, int subdivisionLevel)
            {
                this.position = position;
                this.subdivisionLevel = subdivisionLevel;
            }

            public bool Equals(Brick other) => position == other.position && subdivisionLevel == other.subdivisionLevel;
        }

        [DebuggerDisplay("Brick [{brick.position}, {brick.subdivisionLevel}], {flattenedIdx}")]
        struct ReservedBrick
        {
            public Brick brick;
            // 包含的信息，所在的chunk的一维索引和brick层级
            public int flattenedIdx;
        }

        struct VoxelMeta
        {
            // 所在的chunk组的id
            public RegId id;
            // 存放一个chunk中brick的索引
            public List<ushort> brickIndices;
        }

        struct BrickMeta
        {
            public HashSet<Vector3Int> voxels;
            public List<ReservedBrick> bricks;
        }

        Vector3Int m_CenterRS;   // the anchor in ref space, around which the index is defined. [IMPORTANT NOTE! For now we always have it at 0, so is not passed to the shader, but is kept here until development is active in case we find it useful]

        Dictionary<Vector3Int, List<VoxelMeta>> m_VoxelToBricks;

        // key : 一组chunk的id，也是一个cell下的probe被组织为一个chunk组，所以可以理解为一个cell的id;
        // value : 记录一组chunk所占用的voxel和brick的3d location
        Dictionary<RegId, BrickMeta> m_BricksToVoxels;

        /// <summary>
        /// voxel的细分层级，固定值3
        /// </summary>
        /// <returns></returns>
        int GetVoxelSubdivLevel()
        {
            int defaultVoxelSubdivLevel = 3;
            return Mathf.Min(defaultVoxelSubdivLevel, ProbeReferenceVolume.instance.GetMaxSubdivision() - 1);
        }

        bool m_NeedUpdateIndexComputeBuffer;

        // 输入：内存预算级别
        // 输出：物理索引缓冲区大小，单位：字节
        int SizeOfPhysicalIndexFromBudget(ProbeVolumeTextureMemoryBudget memoryBudget)
        {
            switch (memoryBudget)
            {
                // 低预算分配16MB内存，存储约400万个brick，换成场景尺寸，探头间距1m，则可以覆盖474x474x474的范围,y轴用0.25倍密度的话，可以覆盖948x118x948的范围
                case ProbeVolumeTextureMemoryBudget.MemoryBudgetLow:
                    // 16 MB - 4 million of bricks worth of space. At full resolution and a distance of 1 meter between probes, this is roughly 474 * 474 * 474 meters worth of bricks. If 0.25x on Y axis, this is equivalent to 948 * 118 * 948 meters
                    return 16000000;
                case ProbeVolumeTextureMemoryBudget.MemoryBudgetMedium:
                    // 32 MB - 8 million of bricks worth of space. At full resolution and a distance of 1 meter between probes, this is roughly 600 * 600 * 600 meters worth of bricks. If 0.25x on Y axis, this is equivalent to 1200 * 150 * 1200 meters
                    return 32000000;
                case ProbeVolumeTextureMemoryBudget.MemoryBudgetHigh:
                    // 64 MB - 16 million of bricks worth of space. At full resolution and a distance of 1 meter between probes, this is roughly 756 * 756 * 756 meters worth of bricks. If 0.25x on Y axis, this is equivalent to 1512 * 184 * 1512 meters
                    return 64000000;
            }
            return 32000000;
        }

        /// <summary>
        /// 初始化bricks和chuncks的大小
        /// </summary>
        /// <param name="memoryBudget"></param>
        internal ProbeBrickIndex(ProbeVolumeTextureMemoryBudget memoryBudget)
        {
            Profiler.BeginSample("Create ProbeBrickIndex");
            m_CenterRS = new Vector3Int(0, 0, 0);

            m_VoxelToBricks = new Dictionary<Vector3Int, List<VoxelMeta>>();
            m_BricksToVoxels = new Dictionary<RegId, BrickMeta>();

            m_NeedUpdateIndexComputeBuffer = false;

            //根据预算计算出的chunk的个数，这里的内存预算是专门给存储索引的内存预算
            m_IndexInChunks = Mathf.CeilToInt((float)SizeOfPhysicalIndexFromBudget(memoryBudget) / kIndexChunkSize);
            // chunck的位索引，当位的值为0：表示该chunk无效了，1表示有效
            m_IndexChunks = new BitArray(Mathf.Max(1, m_IndexInChunks));

            // buffer的总大小(总共可以存放的brick的个数)
            int physicalBufferSize = m_IndexInChunks * kIndexChunkSize;
            m_PhysicalIndexBufferData = new int[physicalBufferSize];
            m_PhysicalIndexBuffer = new ComputeBuffer(physicalBufferSize, sizeof(int), ComputeBufferType.Structured);
            m_NextFreeChunk = 0;

            estimatedVMemCost = physicalBufferSize * sizeof(int);

            // Should be done by a compute shader
            Clear();
            Profiler.EndSample();
        }

        /// <summary>
        /// 传递chuncks buffer到GPU端
        /// </summary>
        internal void UploadIndexData()
        {
            m_PhysicalIndexBuffer.SetData(m_PhysicalIndexBufferData);
            m_NeedUpdateIndexComputeBuffer = false;
        }

        internal void Clear()
        {
            Profiler.BeginSample("Clear Index");

            for (int i = 0; i < m_PhysicalIndexBufferData.Length; ++i)
                m_PhysicalIndexBufferData[i] = -1;

            m_NeedUpdateIndexComputeBuffer = true;
            m_NextFreeChunk = 0;
            m_IndexChunks.SetAll(false);

            m_VoxelToBricks.Clear();
            m_BricksToVoxels.Clear();
            Profiler.EndSample();
        }

        /// <summary>
        /// 给定brick,计算brick包含的voxel的坐标
        /// </summary>
        /// <param name="brick">当前的brick</param>
        /// <param name="voxels">brick需要被转换为voxel的索引</param>
        void MapBrickToVoxels(ProbeBrickIndex.Brick brick, HashSet<Vector3Int> voxels)
        {
            // create a list of all voxels this brick will touch
            int brick_subdiv = brick.subdivisionLevel;

            // 单个轴上，当前brick覆盖voxel的数量
            int voxels_touched_cnt = (int)Mathf.Pow(3, Mathf.Max(0, brick_subdiv - GetVoxelSubdivLevel()));

            Vector3Int ipos = brick.position;
            // 一个cell中包含的brick数量
            int brick_size = ProbeReferenceVolume.CellSize(brick.subdivisionLevel);
            // 一个cell中包含的voxel数量
            int voxel_size = ProbeReferenceVolume.CellSize(GetVoxelSubdivLevel());
            // brick太小不足以覆盖一个voxel，计算brick在voxel空间中的位置
            if (voxels_touched_cnt <= 1)
            {
                Vector3 pos = brick.position;
                // 当前brick所在位置转换到voxel空间中去，计算在voxel空间下的位置，并对齐到voxel网格
                pos = pos * (1.0f / voxel_size);
                ipos = new Vector3Int(Mathf.FloorToInt(pos.x) * voxel_size, Mathf.FloorToInt(pos.y) * voxel_size, Mathf.FloorToInt(pos.z) * voxel_size);
            }
            // 当前brick包含的voxel的3d坐标
            for (int z = ipos.z; z < ipos.z + brick_size; z += voxel_size)
                for (int y = ipos.y; y < ipos.y + brick_size; y += voxel_size)
                    for (int x = ipos.x; x < ipos.x + brick_size; x += voxel_size)
                    {
                        voxels.Add(new Vector3Int(x, y, z));
                    }
        }

        void ClearVoxel(Vector3Int pos, CellIndexUpdateInfo cellInfo)
        {
            Vector3Int vx_min, vx_max;

            // 返回voxel坐标的限制范围 vx_min是voxel的起始坐标,vx_max是步进一个cell之后的末尾坐标
            ClipToIndexSpace(pos, GetVoxelSubdivLevel(), out vx_min, out vx_max, cellInfo);
            // 将给定范围内的brick的索引置为-1
            UpdatePhysicalIndex(vx_min, vx_max, -1, cellInfo);
        }

        internal void GetRuntimeResources(ref ProbeReferenceVolume.RuntimeResources rr)
        {
            // If we are pending an update of the actual compute buffer we do it here
            if (m_NeedUpdateIndexComputeBuffer)
            {
                UploadIndexData();
            }
            rr.index = m_PhysicalIndexBuffer;
        }

        internal void Cleanup()
        {
            CoreUtils.SafeRelease(m_PhysicalIndexBuffer);
            m_PhysicalIndexBuffer = null;
        }

        // 单元格边界信息
        // 对最大分辨率的理解：就是cell中存放的brick的层级最高，也就是这个cell内需要更细致的烘焙，因此每个brick的尺寸会更小，那么cell包含的brick就会更多
        public struct CellIndexUpdateInfo
        {
            // 当前cell所在的第一个chunk的索引
            public int firstChunkIndex;
            // 当前cell所占用的chunk的个数
            public int numberOfChunks;
            // cell中brick的最小细分层级，可以推出至少要包含的brick数量
            public int minSubdivInCell;
            // IMPORTANT, These values should be at max resolution. This means that
            // The map to the lower possible resolution is done after.  However they are still in local space.
            // 最大分辨率下，Cell内有效brick的最小索引
            public Vector3Int minValidBrickIndexForCellAtMaxRes;
            // 最大分辨率下，Cell内有效brick的最大索引+1
            public Vector3Int maxValidBrickIndexForCellAtMaxResPlusOne;
            // 最大分辨率下，cell的位置(以brick为单位)
            public Vector3Int cellPositionInBricksAtMaxRes;
        }

        /// <summary>
        /// 将chunk的index和brick的层级打包到一个32位的整数中（高4位是细分级别，低28位是chunk索引）
        /// </summary>
        /// <param name="index">chunk在全局pool中的1d索引</param>
        /// <param name="size">brick的层级</param>
        /// <returns></returns>
        int MergeIndex(int index, int size)
        {
            const int mask = kMaxSubdivisionLevels;
            const int shift = 28;
            return (index & ~(mask << shift)) | ((size & mask) << shift);
        }

        /// <summary>
        /// 为cell分配chunk,记录相关定位
        /// </summary>
        /// <param name="cell">包含brick的单元格</param>
        /// <param name="bricksCount">一个cell中brick的数量</param>
        /// <param name="cellUpdateInfo">cell的更新信息</param>
        /// <returns></returns>
        internal bool AssignIndexChunksToCell(ProbeReferenceVolume.Cell cell, int bricksCount, ref CellIndexUpdateInfo cellUpdateInfo)
        {
            // We need to better handle the case where the chunks are full, this is where streaming will need to come into place swapping in/out
            // Also the current way to find an empty spot might be sub-optimal, when streaming is in place it'd be nice to have this more efficient
            // if it is meant to happen frequently.

            int numberOfChunks = Mathf.CeilToInt((float)bricksCount / kIndexChunkSize);

            // Search for the first empty element with enough space.
            // 找到可以连续装chunk的个数的空闲位置索引
            int firstValidChunk = -1;
            for (int i = 0; i < m_IndexInChunks; ++i)
            {
                if (!m_IndexChunks[i] && (i + numberOfChunks) < m_IndexInChunks)
                {
                    int emptySlotsStartingHere = 0;
                    for (int k = i; k < (i + numberOfChunks); ++k)
                    {
                        if (!m_IndexChunks[k]) emptySlotsStartingHere++;
                        else break;
                    }

                    if (emptySlotsStartingHere == numberOfChunks)
                    {
                        firstValidChunk = i;
                        break;
                    }
                }
            }
            // 没有找到连续空闲的位置，返回false
            if (firstValidChunk < 0) return false;

            // This assert will need to go away or do something else when streaming is allowed (we need to find holes in available chunks or stream out stuff)
            //
            cellUpdateInfo.firstChunkIndex = firstValidChunk;
            cellUpdateInfo.numberOfChunks = numberOfChunks;
            // 将占用的chunk的位标记为true(已被占用)
            for (int i = firstValidChunk; i < (firstValidChunk + numberOfChunks); ++i)
            {
                Debug.Assert(!m_IndexChunks[i]);
                m_IndexChunks[i] = true;
            }

            // 更新下一个空闲chunk索引
            m_NextFreeChunk += Mathf.Max(0, (firstValidChunk + numberOfChunks) - m_NextFreeChunk);

            return true;
        }

        /// <summary>
        /// 添加chunk组的brick信息到buffer中以便GPU使用
        /// </summary>
        /// <param name="id">当前的一组chunk的id</param>
        /// <param name="bricks">当前的一组chunk内的bricks</param>
        /// <param name="allocations">当前的chunks数组</param>
        /// <param name="allocationSize">当前chunk的size,以brick的个数为单位</param>
        /// <param name="poolWidth">全局Pool的宽度</param>
        /// <param name="poolHeight">全局Pool的高度</param>
        /// <param name="cellInfo">cell中brick和chunk组织信息</param>
        public void AddBricks(RegId id, List<Brick> bricks, List<Chunk> allocations, int allocationSize, int poolWidth, int poolHeight, CellIndexUpdateInfo cellInfo)
        {
            Debug.Assert(bricks.Count <= ushort.MaxValue, "Cannot add more than 65K bricks per RegId.");

            // 每个cell中单个轴能容纳的最大bricks的数量
            int largest_cell = ProbeReferenceVolume.CellSize(kMaxSubdivisionLevels);

            // create a new copy
            BrickMeta bm = new BrickMeta();
            bm.voxels = new HashSet<Vector3Int>();
            bm.bricks = new List<ReservedBrick>(bricks.Count);
            m_BricksToVoxels.Add(id, bm);

            int brick_idx = 0;
            // find all voxels each brick will touch
            for (int i = 0; i < allocations.Count; i++)
            {
                // 拿到一个chunk
                Chunk alloc = allocations[i];
                int cnt = Mathf.Min(allocationSize, bricks.Count - brick_idx);
                for (int j = 0; j < cnt; j++, brick_idx++, alloc.x += ProbeBrickPool.kBrickProbeCountPerDim)
                {
                    // 再拿到chunk中的一个brick
                    Brick brick = bricks[brick_idx];

                    int cellSize = ProbeReferenceVolume.CellSize(brick.subdivisionLevel);
                    Debug.Assert(cellSize <= largest_cell, "Cell sizes are not correctly sorted.");
                    largest_cell = Mathf.Min(largest_cell, cellSize);

                    // 计算brick覆盖的voxel的3d索引数组
                    MapBrickToVoxels(brick, bm.voxels);

                    // 将brick所在的chunk的索引和brick的层级信息填入列表中
                    ReservedBrick rbrick = new ReservedBrick();
                    rbrick.brick = brick;
                    rbrick.flattenedIdx = MergeIndex(alloc.flattenIndex(poolWidth, poolHeight), brick.subdivisionLevel);
                    bm.bricks.Add(rbrick);

                    // 拿到被brick覆盖的每一个voxel坐标，每个voxel都有一个voxelMeta信息，记录当前voxel所属的chunk id和brick在chunk内的索引
                    foreach (var v in bm.voxels)
                    {
                        List<VoxelMeta> vm_list;
                        if (!m_VoxelToBricks.TryGetValue(v, out vm_list)) // first time the voxel is touched
                        {
                            vm_list = new List<VoxelMeta>(1);
                            m_VoxelToBricks.Add(v, vm_list);
                        }

                        VoxelMeta vm;
                        int vm_idx = vm_list.FindIndex((VoxelMeta lhs) => lhs.id == id);
                        if (vm_idx == -1) // first time a brick from this id has touched this voxel
                        {
                            vm.id = id;
                            vm.brickIndices = new List<ushort>(4); 
                            vm_list.Add(vm);
                        }
                        else
                        {
                            vm = vm_list[vm_idx];
                        }

                        // add this brick to the voxel under its regId
                        vm.brickIndices.Add((ushort)brick_idx);
                    }
                }
            }

            // 此时所有brick的voxel信息都计算完成，更新cell中所有brick的索引信息
            foreach (var voxel in bm.voxels)
            {
                UpdateIndexForVoxel(voxel, cellInfo);
            }
        }

        public void RemoveBricks(RegId id, CellIndexUpdateInfo cellInfo)
        {
            if (!m_BricksToVoxels.ContainsKey(id))
                return;

            BrickMeta bm = m_BricksToVoxels[id];
            foreach (var v in bm.voxels)
            {
                List<VoxelMeta> vm_list = m_VoxelToBricks[v];
                int idx = vm_list.FindIndex((VoxelMeta lhs) => lhs.id == id);
                if (idx >= 0)
                {
                    vm_list.RemoveAt(idx);
                    if (vm_list.Count > 0)
                    {
                        UpdateIndexForVoxel(v, cellInfo);
                    }
                    else
                    {
                        ClearVoxel(v, cellInfo);
                        m_VoxelToBricks.Remove(v);
                    }
                }
            }
            m_BricksToVoxels.Remove(id);

            // Clear allocated chunks
            for (int i = cellInfo.firstChunkIndex; i < (cellInfo.firstChunkIndex + cellInfo.numberOfChunks); ++i)
            {
                m_IndexChunks[i] = false;
            }
        }

        /// <summary>
        /// 更新cell中brick的索引信息
        /// </summary>
        /// <param name="voxel"></param>
        /// <param name="cellInfo"></param>
        void UpdateIndexForVoxel(Vector3Int voxel, CellIndexUpdateInfo cellInfo)
        {
            ClearVoxel(voxel, cellInfo);
            // 先拿到当前voxel的meta信息
            List<VoxelMeta> vm_list = m_VoxelToBricks[voxel];
            foreach (var vm in vm_list)
            {
                // 从voxel meta信息中读取voxel所属的chunk的id
                // 再从该chunk中拿到该chunk的所有brick meta信息
                // get the list of bricks and indices
                List<ReservedBrick> bricks = m_BricksToVoxels[vm.id].bricks;
                // 从voxel meta信息中读取该voxel所属的brick在chunk中的索引
                List<ushort> indcs = vm.brickIndices;
                UpdateIndexForVoxel(voxel, bricks, indcs, cellInfo);
            }
        }

        /// <summary>
        /// 将brick索引更新到索引缓冲区，完成从世界坐标到GPU缓冲区的复杂空间转换
        /// </summary>
        /// <param name="brickMin"></param>
        /// <param name="brickMax"></param>
        /// <param name="value"></param>
        /// <param name="cellInfo"></param>
        void UpdatePhysicalIndex(Vector3Int brickMin, Vector3Int brickMax, int value, CellIndexUpdateInfo cellInfo)
        {
            // We need to do our calculations in local space to the cell, so we move the brick to local space as a first step.
            // Reminder that at this point we are still operating at highest resolution possible, not necessarily the one that will be
            // the final resolution for the chunk.
            brickMin = brickMin - cellInfo.cellPositionInBricksAtMaxRes;
            brickMax = brickMax - cellInfo.cellPositionInBricksAtMaxRes;

            // Since the index is spurious (not same resolution, but varying per cell) we need to bring to the output resolution the brick coordinates
            // Before finding the locations inside the Index for the current cell/chunk.

            brickMin /= ProbeReferenceVolume.CellSize(cellInfo.minSubdivInCell);
            brickMax /= ProbeReferenceVolume.CellSize(cellInfo.minSubdivInCell);

            // Verify we are actually in local space now.
            int maxCellSizeInOutputRes = ProbeReferenceVolume.CellSize(ProbeReferenceVolume.instance.GetMaxSubdivision() - 1 - cellInfo.minSubdivInCell);
            Debug.Assert(brickMin.x >= 0 && brickMin.y >= 0 && brickMin.z >= 0 && brickMax.x >= 0 && brickMax.y >= 0 && brickMax.z >= 0);
            Debug.Assert(brickMin.x < maxCellSizeInOutputRes && brickMin.y < maxCellSizeInOutputRes && brickMin.z < maxCellSizeInOutputRes && brickMax.x <= maxCellSizeInOutputRes && brickMax.y <= maxCellSizeInOutputRes && brickMax.z <= maxCellSizeInOutputRes);

            // We are now in the right resolution, but still not considering the valid area, so we need to still normalize against that.
            // To do so first let's move back the limits to the desired resolution
            var cellMinIndex = cellInfo.minValidBrickIndexForCellAtMaxRes / ProbeReferenceVolume.CellSize(cellInfo.minSubdivInCell);
            var cellMaxIndex = cellInfo.maxValidBrickIndexForCellAtMaxResPlusOne / ProbeReferenceVolume.CellSize(cellInfo.minSubdivInCell);

            // Then perform the rescale of the local indices for min and max.
            brickMin -= cellMinIndex;
            brickMax -= cellMinIndex;

            // In theory now we are all positive since we clipped during the voxel stage. Keeping assert for debugging, but can go later.
            Debug.Assert(brickMin.x >= 0 && brickMin.y >= 0 && brickMin.z >= 0 && brickMax.x >= 0 && brickMax.y >= 0 && brickMax.z >= 0);


            // Compute the span of the valid part
            var size = (cellMaxIndex - cellMinIndex);

            // Loop through all touched indices
            int chunkStart = cellInfo.firstChunkIndex * kIndexChunkSize;
            for (int z = brickMin.z; z < brickMax.z; ++z)
            {
                for (int y = brickMin.y; y < brickMax.y; ++y)
                {
                    for (int x = brickMin.x; x < brickMax.x; ++x)
                    {
                        int localFlatIdx = z * (size.x * size.y) + x * size.y + y;
                        int actualIdx = chunkStart + localFlatIdx;
                        m_PhysicalIndexBufferData[actualIdx] = value;
                    }
                }
            }

            m_NeedUpdateIndexComputeBuffer = true;
        }

        /// <summary>
        /// 将voxel的位置限制到有效单元格范围内，返回当前voxel坐标的有效范围（在outMinpos和outMaxpos之间）
        /// </summary>
        /// <param name="pos">voxel的3d位置</param>
        /// <param name="subdiv">voxel的细分级别</param>
        /// <param name="outMinpos">裁剪后的最小位置</param>
        /// <param name="outMaxpos">裁剪后的最大位置</param>
        /// <param name="cellInfo">Cell的信息</param>
        void ClipToIndexSpace(Vector3Int pos, int subdiv, out Vector3Int outMinpos, out Vector3Int outMaxpos, CellIndexUpdateInfo cellInfo)
        {
            // to relative coordinates
            // 以voxel为单位的cell大小
            int cellSize = ProbeReferenceVolume.CellSize(subdiv);

            // The position here is in global space, however we want to constraint this voxel update to the valid cell area
            // 当前cell，以brick为单位，在全局空间中的最小有效位置
            var minValidPosition = cellInfo.cellPositionInBricksAtMaxRes + cellInfo.minValidBrickIndexForCellAtMaxRes;
            // 当前cell,以brick为单位，在全局空间中的最大有效位置
            var maxValidPosition = cellInfo.cellPositionInBricksAtMaxRes + cellInfo.maxValidBrickIndexForCellAtMaxResPlusOne - Vector3Int.one;

            // 当前voxel的位置相对中心点的偏移，作为最小值
            int minpos_x = pos.x - m_CenterRS.x;
            int minpos_y = pos.y;
            int minpos_z = pos.z - m_CenterRS.z;
            // 当前voxel的最大位置为最小值+cell以voxel为单位的size
            int maxpos_x = minpos_x + cellSize;
            int maxpos_y = minpos_y + cellSize;
            int maxpos_z = minpos_z + cellSize;
            // clip to valid region
            minpos_x = Mathf.Max(minpos_x, minValidPosition.x);
            minpos_y = Mathf.Max(minpos_y, minValidPosition.y);
            minpos_z = Mathf.Max(minpos_z, minValidPosition.z);
            maxpos_x = Mathf.Min(maxpos_x, maxValidPosition.x);
            maxpos_y = Mathf.Min(maxpos_y, maxValidPosition.y);
            maxpos_z = Mathf.Min(maxpos_z, maxValidPosition.z);

            outMinpos = new Vector3Int(minpos_x, minpos_y, minpos_z);
            outMaxpos = new Vector3Int(maxpos_x, maxpos_y, maxpos_z);
        }

        /// <summary>
        /// 将brick的信息更新到physical index buffer中
        /// </summary>
        /// <param name="voxel">voxel的坐标（在cell内的局部坐标）</param>
        /// <param name="bricks">一个chunk内的bricks</param>
        /// <param name="indices">该voxel所属的brick在chunk中的索引</param>
        /// <param name="cellInfo"></param>
        void UpdateIndexForVoxel(Vector3Int voxel, List<ReservedBrick> bricks, List<ushort> indices, CellIndexUpdateInfo cellInfo)
        {
            // clip voxel to index space
            // 拿到voxel的最小坐标和最大坐标
            Vector3Int vx_min, vx_max;
            ClipToIndexSpace(voxel, GetVoxelSubdivLevel(), out vx_min, out vx_max, cellInfo);

            foreach (var rbrick in bricks)
            {
                // clip brick to clipped voxel
                // 以brick为单位的cell size
                int brick_cell_size = ProbeReferenceVolume.CellSize(rbrick.brick.subdivisionLevel);
                // brick的相对cell位置的最小坐标和最大坐标
                Vector3Int brick_min = rbrick.brick.position;
                Vector3Int brick_max = rbrick.brick.position + Vector3Int.one * brick_cell_size;
                brick_min.x = Mathf.Max(vx_min.x, brick_min.x - m_CenterRS.x);
                brick_min.y = Mathf.Max(vx_min.y, brick_min.y);
                brick_min.z = Mathf.Max(vx_min.z, brick_min.z - m_CenterRS.z);
                brick_max.x = Mathf.Min(vx_max.x, brick_max.x - m_CenterRS.x);
                brick_max.y = Mathf.Min(vx_max.y, brick_max.y);
                brick_max.z = Mathf.Min(vx_max.z, brick_max.z - m_CenterRS.z);

                UpdatePhysicalIndex(brick_min, brick_max, rbrick.flattenedIdx, cellInfo);
            }
        }
    }
}
