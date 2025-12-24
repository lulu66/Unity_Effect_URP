using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.IO;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Experimental.Rendering
{
    // TMP to be moved to ProbeReferenceVolume when we define the concept, here it is just to make stuff compile
    enum ProbeVolumeState
    {
        Default = 0,
        Invalid = 999
    }

    // 每个场景的probe volume数据容器，专门存储和管理单个场景的烘焙光照探头数据
    [ExecuteAlways]
    [AddComponentMenu("")] // Hide.
    internal class ProbeVolumePerSceneData : MonoBehaviour, ISerializationCallbackReceiver
    {

        [System.Serializable]
        struct SerializableAssetItem
        {
            [SerializeField] public ProbeVolumeState state;
            [SerializeField] public ProbeVolumeAsset asset;
        }

        internal Dictionary<ProbeVolumeState, ProbeVolumeAsset> assets = new Dictionary<ProbeVolumeState, ProbeVolumeAsset>();

        [SerializeField] List<SerializableAssetItem> serializedAssets;

        ProbeVolumeState m_CurrentState = ProbeVolumeState.Default;
        ProbeVolumeState m_PreviousState = ProbeVolumeState.Invalid;

        /// <summary>
        /// OnAfterDeserialize implementation.
        /// 将列表的数据转化为asset字典
        /// </summary>
        public void OnAfterDeserialize()
        {
            if (serializedAssets == null) return;

            assets = new Dictionary<ProbeVolumeState, ProbeVolumeAsset>();
            foreach (var assetItem in serializedAssets)
            {
                assets.Add(assetItem.state, assetItem.asset);
            }
        }

        /// <summary>
        /// OnBeforeSerialize implementation.
        /// 将asset字典的数据转换为列表，以便于之后进行序列化
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (assets == null || serializedAssets == null) return;

            serializedAssets.Clear();
            foreach (var k in assets.Keys)
            {
                SerializableAssetItem item;
                item.state = k;
                item.asset = assets[k];
                serializedAssets.Add(item);
            }
        }

        internal void StoreAssetForState(ProbeVolumeState state, ProbeVolumeAsset asset)
        {
            assets[state] = asset;
        }

        /// <summary>
        /// 清空资源字典
        /// </summary>
        internal void InvalidateAllAssets()
        {
            foreach (var asset in assets.Values)
            {
                if (asset != null)
                    ProbeReferenceVolume.instance.AddPendingAssetRemoval(asset);
            }

            assets.Clear();
        }
        /// <summary>
        /// 返回当前状态的资源
        /// </summary>
        /// <returns></returns>
        internal ProbeVolumeAsset GetCurrentStateAsset()
        {
            if (assets.ContainsKey(m_CurrentState)) return assets[m_CurrentState];
            else return null;
        }

        /// <summary>
        /// 即将加载的probe volume资源加入队列
        /// </summary>
        internal void QueueAssetLoading()
        {
            var refVol = ProbeReferenceVolume.instance;
            if (assets.ContainsKey(m_CurrentState) && assets[m_CurrentState] != null)
            {
                // 加载probe volume assets的信息初始化
                refVol.AddPendingAssetLoading(assets[m_CurrentState]);
#if UNITY_EDITOR
                // 更新dilation的有效阈值
                if (refVol.sceneData != null)
                {
                    refVol.dilationValidtyThreshold = refVol.sceneData.GetBakeSettingsForScene(gameObject.scene).dilationSettings.dilationValidityThreshold;
                }
#endif
                m_PreviousState = m_CurrentState;
            }
        }

        /// <summary>
        /// 即将卸载的probe volume资源加入队列
        /// </summary>
        internal void QueueAssetRemoval()
        {
            if (assets.ContainsKey(m_CurrentState) && assets[m_CurrentState] != null)
                ProbeReferenceVolume.instance.AddPendingAssetRemoval(assets[m_CurrentState]);
        }

        void OnEnable()
        {
            QueueAssetLoading();
        }

        void OnDisable()
        {
            QueueAssetRemoval();
        }

        void OnDestroy()
        {
            QueueAssetRemoval();
        }

        void Update()
        {
            // Query state from ProbeReferenceVolume.instance.
            // This is temporary here until we implement a state system.
            m_CurrentState = ProbeVolumeState.Default;

            if (m_PreviousState != m_CurrentState)
            {
                if (assets.ContainsKey(m_PreviousState) && assets[m_PreviousState] != null)
                    ProbeReferenceVolume.instance.AddPendingAssetRemoval(assets[m_PreviousState]);

                QueueAssetLoading();
            }
        }
    }
}
