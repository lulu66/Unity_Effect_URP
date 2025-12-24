using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering
{
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// A global manager that tracks all the Volumes in the currently loaded Scenes and does all the
    /// interpolation work.
    /// </summary>
    public sealed class VolumeManager
    {
        // Lazy<VolumeManager> 是C#中的延迟初始化模式，用于实现资源的按需创建和初始化
        static readonly Lazy<VolumeManager> s_Instance = new Lazy<VolumeManager>(() => new VolumeManager());

        /// <summary>
        /// The current singleton instance of <see cref="VolumeManager"/>.
        /// </summary>
        public static VolumeManager instance => s_Instance.Value;

        /// <summary>
        /// A reference to the main <see cref="VolumeStack"/>.
        /// </summary>
        /// <seealso cref="VolumeStack"/>
        public VolumeStack stack { get; set; }

        /// <summary>
        /// The current list of all available types that derive from <see cref="VolumeComponent"/>.
        /// </summary>
        [Obsolete("Please use baseComponentTypeArray instead.")]
        public IEnumerable<Type> baseComponentTypes
        {
            get => baseComponentTypeArray;
            private set => baseComponentTypeArray = value.ToArray();
        }

        /// <summary>
        /// The current list of all available types that derive from <see cref="VolumeComponent"/>.
        /// 当前拥有的所有组件的类型
        /// </summary>
        public Type[] baseComponentTypeArray { get; private set; }

        // Max amount of layers available in Unity
        const int k_MaxLayerCount = 32;

        // Cached lists of all volumes (sorted by priority) by layer mask
        readonly Dictionary<int, List<Volume>> m_SortedVolumes;

        // Holds all the registered volumes
        readonly List<Volume> m_Volumes;

        // Keep track of sorting states for layer masks
        readonly Dictionary<int, bool> m_SortNeeded;

        // Internal list of default state for each component type - this is used to reset component
        // states on update instead of having to implement a Reset method on all components (which
        // would be error-prone)
        // 当前拥有的所有volume组件的实例
        readonly List<VolumeComponent> m_ComponentsDefaultState;

        // Recycled list used for volume traversal
        readonly List<Collider> m_TempColliders;

        // The default stack the volume manager uses.
        // We cache this as users able to change the stack through code and
        // we want to be able to switch to the default one through the ResetMainStack() function.
        VolumeStack m_DefaultStack = null;

        VolumeManager()
        {
            m_SortedVolumes = new Dictionary<int, List<Volume>>();
            m_Volumes = new List<Volume>();
            m_SortNeeded = new Dictionary<int, bool>();
            m_TempColliders = new List<Collider>(8);
            m_ComponentsDefaultState = new List<VolumeComponent>();

            ReloadBaseTypes();

            // 默认的volume stack, stack是可以变化的
            m_DefaultStack = CreateStack();
            stack = m_DefaultStack;
        }

        /// <summary>
        /// Creates and returns a new <see cref="VolumeStack"/> to use when you need to store
        /// the result of the Volume blending pass in a separate stack.
        /// 将默认的volume组件实例存储为volume stack
        /// </summary>
        /// <returns></returns>
        /// <seealso cref="VolumeStack"/>
        /// <seealso cref="Update(VolumeStack,Transform,LayerMask)"/>
        public VolumeStack CreateStack()
        {
            var stack = new VolumeStack();
            stack.Reload(m_ComponentsDefaultState);
            return stack;
        }

        /// <summary>
        /// Resets the main stack to be the default one.
        /// Call this function if you've assigned the main stack to something other than the default one.
        /// </summary>
        public void ResetMainStack()
        {
            stack = m_DefaultStack;
        }

        /// <summary>
        /// Destroy a Volume Stack
        /// </summary>
        /// <param name="stack">Volume Stack that needs to be destroyed.</param>
        public void DestroyStack(VolumeStack stack)
        {
            stack.Dispose();
        }

        // This will be called only once at runtime and everytime script reload kicks-in in the
        // editor as we need to keep track of any compatible component in the project
        /// <summary>
        /// 通过反射找到当前所拥有的所有volume组件类型，并创建对应的默认实例；
        /// </summary>
        void ReloadBaseTypes()
        {
            m_ComponentsDefaultState.Clear();

            // Grab all the component types we can find
            // 查找所有继承volume component的组件，返回组件数组
            baseComponentTypeArray = CoreUtils.GetAllTypesDerivedFrom<VolumeComponent>()
                .Where(t => !t.IsAbstract).ToArray();

            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            // Keep an instance of each type to be used in a virtual lowest priority global volume
            // so that we have a default state to fallback to when exiting volumes
            // 创建组件的实例加入列表中
            foreach (var type in baseComponentTypeArray)
            {
                type.GetMethod("Init", flags)?.Invoke(null, null);
                var inst = (VolumeComponent)ScriptableObject.CreateInstance(type);
                m_ComponentsDefaultState.Add(inst);
            }
        }

        /// <summary>
        /// Registers a new Volume in the manager. Unity does this automatically when a new Volume is
        /// enabled, or its layer changes, but you can use this function to force-register a Volume
        /// that is currently disabled.
        /// 注册volume,并根据它的layermask，在字典中排序
        /// </summary>
        /// <param name="volume">The volume to register.</param>
        /// <param name="layer">The LayerMask that this volume is in.</param>
        /// <seealso cref="Unregister"/>
        public void Register(Volume volume, int layer)
        {
            if (m_Volumes.Contains(volume))
                return;

            m_Volumes.Add(volume);

            // Look for existing cached layer masks and add it there if needed
            foreach (var kvp in m_SortedVolumes)
            {
                // We add the volume to sorted lists only if the layer match and if it doesn't contain the volume already.
                if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(volume))
                    kvp.Value.Add(volume);
            }

            SetLayerDirty(layer);
        }

        /// <summary>
        /// Unregisters a Volume from the manager. Unity does this automatically when a Volume is
        /// disabled or goes out of scope, but you can use this function to force-unregister a Volume
        /// that you added manually while it was disabled.
        /// </summary>
        /// <param name="volume">The Volume to unregister.</param>
        /// <param name="layer">The LayerMask that this Volume is in.</param>
        /// <seealso cref="Register"/>
        public void Unregister(Volume volume, int layer)
        {
            if (m_Volumes.Remove(volume))
            {
                foreach (var kvp in m_SortedVolumes)
                {
                    // Skip layer masks this volume doesn't belong to
                    if ((kvp.Key & (1 << layer)) == 0)
                        continue;

                    kvp.Value.Remove(volume);
                }
            }
        }

        /// <summary>
        /// Checks if a <see cref="VolumeComponent"/> is active in a given LayerMask.
        /// 当前的layermask下是否存在激活的volume component
        /// </summary>
        /// <typeparam name="T">A type derived from <see cref="VolumeComponent"/></typeparam>
        /// <param name="layerMask">The LayerMask to check against</param>
        /// <returns><c>true</c> if the component is active in the LayerMask, <c>false</c>
        /// otherwise.</returns>
        public bool IsComponentActiveInMask<T>(LayerMask layerMask)
            where T : VolumeComponent
        {
            int mask = layerMask.value;

            foreach (var kvp in m_SortedVolumes)
            {
                if (kvp.Key != mask)
                    continue;

                foreach (var volume in kvp.Value)
                {
                    if (!volume.enabled || volume.profileRef == null)
                        continue;

                    if (volume.profileRef.TryGet(out T component) && component.active)
                        return true;
                }
            }

            return false;
        }

        internal void SetLayerDirty(int layer)
        {
            Assert.IsTrue(layer >= 0 && layer <= k_MaxLayerCount, "Invalid layer bit");

            foreach (var kvp in m_SortedVolumes)
            {
                var mask = kvp.Key;

                if ((mask & (1 << layer)) != 0)
                    m_SortNeeded[mask] = true;
            }
        }

        internal void UpdateVolumeLayer(Volume volume, int prevLayer, int newLayer)
        {
            Assert.IsTrue(prevLayer >= 0 && prevLayer <= k_MaxLayerCount, "Invalid layer bit");
            Unregister(volume, prevLayer);
            Register(volume, newLayer);
        }

        // Go through all listed components and lerp overridden values in the global state
        // 将stack中的volume组件参数的值用给定的组件列表里对应组件的参数和混合系数覆写
        void OverrideData(VolumeStack stack, List<VolumeComponent> components, float interpFactor)
        {
            foreach (var component in components)
            {
                if (!component.active)
                    continue;

                var state = stack.GetComponent(component.GetType());
                component.Override(state, interpFactor);
            }
        }

        // Faster version of OverrideData to force replace values in the global state
        /// <summary>
        /// 将volume stack中的volume参数全替换为默认参数
        /// </summary>
        /// <param name="stack"></param>
        void ReplaceData(VolumeStack stack)
        {
            var resetParameters = stack.defaultParameters;
            var resetParametersCount = resetParameters.Length;
            for (int i = 0; i < resetParametersCount; i++)
            {
                var resetParam = resetParameters[i];
                var targetParam = resetParam.parameter;
                targetParam.overrideState = false;
                targetParam.SetValue(resetParam.defaultValue);
            }
        }

        /// <summary>
        /// Checks the state of the base type library. This is only used in the editor to handle
        /// entering and exiting of play mode and domain reload.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public void CheckBaseTypes()
        {
            // Editor specific hack to work around serialization doing funky things when exiting
            if (m_ComponentsDefaultState == null || (m_ComponentsDefaultState.Count > 0 && m_ComponentsDefaultState[0] == null))
                ReloadBaseTypes();
        }

        /// <summary>
        /// Checks the state of a given stack. This is only used in the editor to handle entering
        /// and exiting of play mode and domain reload.
        /// </summary>
        /// <param name="stack">The stack to check.</param>
        [Conditional("UNITY_EDITOR")]
        public void CheckStack(VolumeStack stack)
        {
            // The editor doesn't reload the domain when exiting play mode but still kills every
            // object created while in play mode, like stacks' component states
            var components = stack.components;

            if (components == null)
            {
                stack.Reload(m_ComponentsDefaultState);
                return;
            }

            foreach (var kvp in components)
            {
                if (kvp.Key == null || kvp.Value == null)
                {
                    stack.Reload(m_ComponentsDefaultState);
                    return;
                }
            }
        }

        // Returns true if must execute Update() in full, and false if we can early exit.
        bool CheckUpdateRequired(VolumeStack stack)
        {
            if (m_Volumes.Count == 0)
            {
                if (stack.requiresReset)
                {
                    // Update the stack one more time in case there was a volume that just ceased to exist. This ensures
                    // the stack will return to default values correctly.
                    stack.requiresReset = false;
                    return true;
                }

                // There were no volumes last frame either, and stack has been returned to defaults, so no update is
                // needed and we can early exit from Update().
                return false;
            }
            stack.requiresReset = true; // Stack must be reset every frame whenever there are volumes present
            return true;
        }

        /// <summary>
        /// Updates the global state of the Volume manager. Unity usually calls this once per Camera
        /// in the Update loop before rendering happens.
        /// </summary>
        /// <param name="trigger">A reference Transform to consider for positional Volume blending
        /// </param>
        /// <param name="layerMask">The LayerMask that the Volume manager uses to filter Volumes that it should consider
        /// for blending.</param>
        public void Update(Transform trigger, LayerMask layerMask)
        {
            Update(stack, trigger, layerMask);
        }

        /// <summary>
        /// Updates the Volume manager and stores the result in a custom <see cref="VolumeStack"/>.
        /// 混合和更新各后处理效果的参数，根据权重和volume的距离进行插值
        /// </summary>
        /// <param name="stack">The stack to store the blending result into.</param>
        /// <param name="trigger">A reference Transform to consider for positional Volume blending.
        /// </param>
        /// <param name="layerMask">The LayerMask that Unity uses to filter Volumes that it should consider
        /// for blending.</param>
        /// <seealso cref="VolumeStack"/>
        public void Update(VolumeStack stack, Transform trigger, LayerMask layerMask)
        {
            Assert.IsNotNull(stack);

            // 如果当前volume组件不存在，需重新加载volume组件
            CheckBaseTypes();
            // 重载stack中volume组件类型及参数
            CheckStack(stack);

            if (!CheckUpdateRequired(stack))
                return;

            // Start by resetting the global state to default values
            // 重置stack中的volume 参数
            ReplaceData(stack);

            // 如果没有触发器，则判定为只有全局volume
            bool onlyGlobal = trigger == null;
            // 只有全局volume，触发位置为原点，否则为触发器的位置
            var triggerPos = onlyGlobal ? Vector3.zero : trigger.position;

            // Sort the cached volume list(s) for the given layer mask if needed and return it
            // 获取layermask 为给定的参数的volumes
            var volumes = GrabVolumes(layerMask);

            Camera camera = null;
            // Behavior should be fine even if camera is null
            // 如果不是全局volume,则获取触发器上的相机
            if (!onlyGlobal)
                trigger.TryGetComponent<Camera>(out camera);

            // Traverse all volumes
            foreach (var volume in volumes)
            {
                if (volume == null)
                    continue;

#if UNITY_EDITOR
                // Skip volumes that aren't in the scene currently displayed in the scene view
                if (!IsVolumeRenderedByCamera(volume, camera))
                    continue;
#endif

                // Skip disabled volumes and volumes without any data or weight
                if (!volume.enabled || volume.profileRef == null || volume.weight <= 0f)
                    continue;

                // Global volumes always have influence
                // 如果是全局volume，则stack中的volume参数用profileRef的组件中参数和权重进行插值覆写
                if (volume.isGlobal)
                {
                    OverrideData(stack, volume.profileRef.components, Mathf.Clamp01(volume.weight));
                    continue;
                }

                if (onlyGlobal)
                    continue;

                // If volume isn't global and has no collider, skip it as it's useless
                // 对于局部volume，获取碰撞体列表
                var colliders = m_TempColliders;
                volume.GetComponents(colliders);
                if (colliders.Count == 0)
                    continue;

                // Find closest distance to volume, 0 means it's inside it
                float closestDistanceSqr = float.PositiveInfinity;

                foreach (var collider in colliders)
                {
                    if (!collider.enabled)
                        continue;

                    // 获取碰撞体上离触发点最近的点
                    var closestPoint = collider.ClosestPoint(triggerPos);
                    // 触发点离最近的碰撞体上的点的距离的平方
                    var d = (closestPoint - triggerPos).sqrMagnitude;

                    // 更新最近距离
                    if (d < closestDistanceSqr)
                        closestDistanceSqr = d;
                }

                colliders.Clear();
                // 混合距离的平方
                float blendDistSqr = volume.blendDistance * volume.blendDistance;

                // Volume has no influence, ignore it
                // Note: Volume doesn't do anything when `closestDistanceSqr = blendDistSqr` but we
                //       can't use a >= comparison as blendDistSqr could be set to 0 in which case
                //       volume would have total influence
                if (closestDistanceSqr > blendDistSqr)
                    continue;

                // Volume has influence
                float interpFactor = 1f;

                // 混合系数：基于混合距离和当前离触发点的最近距离的比例
                if (blendDistSqr > 0f)
                    interpFactor = 1f - (closestDistanceSqr / blendDistSqr);

                // No need to clamp01 the interpolation factor as it'll always be in [0;1[ range
                // 插值覆写stack中各volume参数
                OverrideData(stack, volume.profileRef.components, interpFactor * Mathf.Clamp01(volume.weight));
            }
        }

        /// <summary>
        /// Get all volumes on a given layer mask sorted by influence.
        /// </summary>
        /// <param name="layerMask">The LayerMask that Unity uses to filter Volumes that it should consider.</param>
        /// <returns>An array of volume.</returns>
        public Volume[] GetVolumes(LayerMask layerMask)
        {
            var volumes = GrabVolumes(layerMask);
            volumes.RemoveAll(v => v == null);
            return volumes.ToArray();
        }

        // 按Layer掩码获取volume对象列表，每个layer下的volume都按照优先级进行排序
        List<Volume> GrabVolumes(LayerMask mask)
        {
            List<Volume> list;

            if (!m_SortedVolumes.TryGetValue(mask, out list))
            {
                // New layer mask detected, create a new list and cache all the volumes that belong
                // to this mask in it
                list = new List<Volume>();

                foreach (var volume in m_Volumes)
                {
                    if ((mask & (1 << volume.gameObject.layer)) == 0)
                        continue;

                    list.Add(volume);
                    m_SortNeeded[mask] = true;
                }

                m_SortedVolumes.Add(mask, list);
            }

            // Check sorting state
            bool sortNeeded;
            if (m_SortNeeded.TryGetValue(mask, out sortNeeded) && sortNeeded)
            {
                m_SortNeeded[mask] = false;
                SortByPriority(list);
            }

            return list;
        }

        // Stable insertion sort. Faster than List<T>.Sort() for our needs.
        static void SortByPriority(List<Volume> volumes)
        {
            Assert.IsNotNull(volumes, "Trying to sort volumes of non-initialized layer");

            for (int i = 1; i < volumes.Count; i++)
            {
                var temp = volumes[i];
                int j = i - 1;

                // Sort order is ascending
                while (j >= 0 && volumes[j].priority > temp.priority)
                {
                    volumes[j + 1] = volumes[j];
                    j--;
                }

                volumes[j + 1] = temp;
            }
        }

        static bool IsVolumeRenderedByCamera(Volume volume, Camera camera)
        {
#if UNITY_2018_3_OR_NEWER && UNITY_EDITOR
            // IsGameObjectRenderedByCamera does not behave correctly when camera is null so we have to catch it here.
            return camera == null ? true : UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(volume.gameObject, camera);
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// A scope in which a Camera filters a Volume.
    /// </summary>
    [Obsolete("VolumeIsolationScope is deprecated, it does not have any effect anymore.")]
    public struct VolumeIsolationScope : IDisposable
    {
        /// <summary>
        /// Constructs a scope in which a Camera filters a Volume.
        /// </summary>
        /// <param name="unused">Unused parameter.</param>
        public VolumeIsolationScope(bool unused) { }

        /// <summary>
        /// Stops the Camera from filtering a Volume.
        /// </summary>
        void IDisposable.Dispose() { }
    }
}
