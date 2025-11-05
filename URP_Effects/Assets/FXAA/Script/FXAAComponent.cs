using System;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
	[Serializable, VolumeComponentMenu("CustomPostProcessing/EasyFXAA")]
	public class FXAAComponent : VolumeComponent, IPostProcessComponent
	{
		public enum FXAAQuality
		{
			HighPerformance = 0,
			Performance,
			Balance,
			Quality,
			HighQuality,
		}

		public enum FXAAMethod
		{
			EasyFXAA = 0,
			FS,
		}

		public BoolParameter Enable = new BoolParameter(false, true);
		public FXAAMethodParameter Method = new FXAAMethodParameter(FXAAMethod.EasyFXAA, true);
		public FXAAQualityParameter Quality = new FXAAQualityParameter(FXAAQuality.Balance, true);

		public bool IsActive()
		{
			return Enable.value;
		}

		public bool IsTileCompatible()
		{
			return true;
		}

		[Serializable]
		public sealed class FXAAQualityParameter : VolumeParameter<FXAAQuality>
		{
			public FXAAQualityParameter(FXAAQuality value, bool overrideState = false)
				: base(value, overrideState) { }
		}

		[Serializable]
		public sealed class FXAAMethodParameter : VolumeParameter<FXAAMethod>
		{
			public FXAAMethodParameter(FXAAMethod value, bool overrideState = false)
				: base(value, overrideState) { }
		}
	}
}

