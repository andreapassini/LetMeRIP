// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/roundedSqaure"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		[MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
		
		//[PerRendererData] 
		_Color ("Tint Color", Color) = (1,1,1,1)	


//		_Radius ("_Radius", Range (0, 1.77)) = 0.1
		_Radius ("_Radius", Range (0, 0.5)) = 0.1

		_scale ("_scale", Vector) = (0.0, 0.0,0.0)

		[MaterialToggle] _TR ("_TopRightCorner", Float) = 1
		[MaterialToggle] _BR ("_BottomRightCorner", Float) = 1
		[MaterialToggle] _BL ("_BottomLeftCorner", Float) = 1
		[MaterialToggle] _TL ("_TopLeftCorner", Float) = 1

		[MaterialToggle] _Invert ("_Invert", Float) = 0

	}
 
	SubShader
	{
		Tags
		{ 
			"Queue"="Transparent" 
			"IgnoreProjector"="True" 
			"RenderType"="Transparent" 
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}
 
		Cull Off
		Lighting Off
		ZWrite Off
		Fog { Mode Off }
		Blend One OneMinusSrcAlpha
 
		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile DUMMY PIXELSNAP_ON
			#include "UnityCG.cginc"


			uniform half _TR;
			uniform half _BR;
			uniform half _BL;
			uniform half _TL;

//			uniform float2 _TRC;
//			uniform float2 _BRC;
//			uniform float2 _BLC;
//			uniform float2 _TLC;

			uniform half2 _scale;

			uniform half _Invert;

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};
 
			struct v2f
			{
				float4 vertex   : SV_POSITION;
				fixed4 color    : COLOR;
				half2 texcoord  : TEXCOORD0;
				half2 mask : TEXCOORD1; 
			};

			float4 _MainTex_ST;

			fixed4 _Color;
			
			v2f vert(appdata_t IN)
			{
				v2f OUT;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;

				OUT.color = fixed4(1,1,1,1);

				#ifdef PIXELSNAP_ON
				OUT.vertex = UnityPixelSnap (OUT.vertex);
				#endif

				OUT.mask = TRANSFORM_TEX( IN.texcoord, _MainTex );
				return OUT;
			}
 
			sampler2D _MainTex;
			
			float _Radius;
			
			fixed4 frag(v2f IN) : SV_Target
			{

				half4 m = tex2D(_MainTex, IN.mask) * _Color;

				half4 c = half4(0.0,0.0,0.0,0.0);

				if (_Invert == 1)
				{
					//none
				}
				else
				{
					c = IN.color;
				}


				half dist = 0;

				half2 center = half2(0,0);

				half2 _TRC = half2(1,1);
				half2 _BRC = half2(1,0);
				half2 _BLC = half2(0,0);
				half2 _TLC = half2(0,1);

				half sfx0 = 1;
				half sfx1 = 1;
				half sfy0 = 1;
				half sfy1 = 1;

				if (_scale.x > _scale.y)
				{
					sfx0  = _scale.y/_scale.x;
					sfx1  = (_scale.y/ pow(_scale.x,2));
				}
				else
				{
					sfy0  = _scale.x/_scale.y;
					sfy1  = (_scale.x/ pow(_scale.y,2));
				}



				center = half2((_TRC.x - (_Radius * sfx0)),(_TRC.y - (_Radius * sfy0) ));
				if (_TR == 1 )
				{

					if (
						center.x < IN.texcoord.x
						&& center.y < IN.texcoord.y
						)
					{
						dist = sqrt( 
									pow( center.x - IN.texcoord.x , 2 ) / sfx1
									+ 
									pow( center.y - IN.texcoord.y , 2 ) / sfy1
									);

						if (dist > _Radius)
						{
							if (_Invert == 1 )
							{
								c = IN.color;
							}
							else
							{
								c = half4(0,0,0,0);
							}
						}
					}
				}


				center = half2((_BRC.x - (_Radius * sfx0) ),(_BRC.y + (_Radius * sfy0) ));
				if (_BR == 1 )
				{
					if (
						center.x < IN.texcoord.x
						&& center.y > IN.texcoord.y
						)
					{
						dist = sqrt( 
									pow( center.x - IN.texcoord.x , 2 ) / sfx1
									+ 
									pow( center.y - IN.texcoord.y , 2 ) / sfy1
									);

						if (dist > _Radius)
						{
							if (_Invert == 1 )
							{
								c = IN.color;
							}
							else
							{
								c = half4(0,0,0,0);
							}
						}
					}
				}

				center = half2((_BLC.x + (_Radius * sfx0) ),(_BLC.y + (_Radius * sfy0) ));
				if (_BL == 1 )
				{
					if (
						center.x > IN.texcoord.x
						&& center.y > IN.texcoord.y
						)
					{
						dist = sqrt( 
									pow( center.x - IN.texcoord.x , 2 ) / sfx1
									+ 
									pow( center.y - IN.texcoord.y , 2 ) / sfy1
									);

						if (dist > _Radius)
						{
							if (_Invert == 1 )
							{
								c = IN.color;
							}
							else
							{
								c = half4(0,0,0,0);
							}
						}
					}
				}

				center = half2((_TLC.x + (_Radius * sfx0) ),(_TLC.y - (_Radius * sfy0) ));
				if (_TL == 1 )
				{
					if (
						center.x > IN.texcoord.x
						&& center.y < IN.texcoord.y
						)
					{
						dist = sqrt( 
									pow( center.x - IN.texcoord.x , 2 ) / sfx1
									+ 
									pow( center.y - IN.texcoord.y , 2 ) / sfy1
									);

						if (dist > _Radius)
						{
							if (_Invert == 1 )
							{
								c = IN.color;
							}
							else
							{
								c = half4(0,0,0,0);
							}
						}
					}
				}

				c = lerp(c,m,c.a);

				return c;
			}
		ENDCG
		}
	}
	
}