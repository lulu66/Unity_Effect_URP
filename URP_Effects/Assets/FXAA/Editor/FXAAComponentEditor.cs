using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [VolumeComponentEditor(typeof(FXAAComponent))]
    public class FXAAComponentEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Enable;
        SerializedDataParameter m_Quality;
		SerializedDataParameter m_Method;
		public override void OnEnable()
		{
			var o = new PropertyFetcher<FXAAComponent>(serializedObject);
			m_Enable = Unpack(o.Find(x => x.Enable));
			m_Quality = Unpack(o.Find( x => x.Quality));
			m_Method = Unpack(o.Find(x => x.Method));

		}

		public override void OnInspectorGUI()
		{
			PropertyField(m_Enable);
			PropertyField(m_Method);
			if(m_Method.value.enumValueIndex == 0)
			{
				PropertyField(m_Quality);
			}
		}
	}

}

