﻿//***************************************************
//
//  Author: Ben Hopkins
//  Copyright (C) 2016 kode80 LLC, 
//  all rights reserved
// 
//  Free to use for non-commercial purposes, 
//  see full license in project root:
//  PixelRenderNonCommercialLicense.html
//  
//  Commercial licenses available for purchase from:
//  http://kode80.com/
//
//***************************************************

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using kode80.GUIWrapper;

namespace kode80.PixelRender
{
	public class SpriteSheetMakerWindow : EditorWindow
	{
		private const string _RootName = "com.kode80.PixelRender.SpriteSheetMaker.RootGameObject";
		private const string _ModelName = "com.kode80.PixelRender.SpriteSheetMaker.ModelGameObject";
		private const string _CameraName = "com.kode80.PixelRender.SpriteSheetMaker.PreviewCamera";
		private const int _PreviewLayer = 31;

		private GUIHorizontal _gui;
		private GUIScrollView _guiSide;
		private GUIIntSlider _guiFrameCount;
		private GUIIntSlider _guiFrameWidth;
		private GUIIntSlider _guiFrameHeight;
		private GUIIntSlider _guiCurrentFrame;
		private GUIVector3Field _guiPositionOffset;
		private GUISlider _guiScaleOffset;
		private GUIPopup _guiAnimationClips;
		private GUIVector3Field _guiStartRotation;
		private GUIVector3Field _guiEndRotation;
		private GUIIntSlider _guiLoopCount;
		private GUIToggle _guiPingPong;
		private GUIVertical _guiPreview;

		private RenderTexture _previewTexture;
		private Camera _previewCamera;
		private PixelOutlineEffect _previewOutline;
		private GameObject _rootGameObject;
		private GameObject _modelGameObject;
		private float _lastFrameTime;

		[MenuItem( "Window/kode80/PixelRender/Sprite Sheet Maker")]
		public static void Init()
		{
			SpriteSheetMakerWindow win = EditorWindow.GetWindow( typeof( SpriteSheetMakerWindow)) as SpriteSheetMakerWindow;
			win.titleContent = new GUIContent( "Sprite Sheet Maker");
			win.Show();
		}

		void OnEnable()
		{
			_lastFrameTime = Time.realtimeSinceStartup;

			_gui = new GUIHorizontal();

			GUIVertical sideContainer = _gui.Add( new GUIVertical( GUILayout.MaxWidth(290.0f))) as GUIVertical;
			_guiSide = sideContainer.Add( new GUIScrollView()) as GUIScrollView;

			_guiSide.Add( new GUIObjectField<GameObject>( new GUIContent( "GameObject", "GameObject to render as sprite sheet"),
														  true, GameObjectChanged));
			_guiFrameCount = _guiSide.Add( new GUIIntSlider( new GUIContent( "Frame Count", "Number of frames in the sprite sheet"),
				12, 1, 64, FrameCountChanged)) as GUIIntSlider;
			_guiFrameWidth = _guiSide.Add( new GUIIntSlider( new GUIContent( "Frame Width", "Width of each frame in the sprite sheet"),
				100, 32, 512, ResizeFrame)) as GUIIntSlider;
			_guiFrameHeight = _guiSide.Add( new GUIIntSlider( new GUIContent( "Frame Height", "Height of each frame in the sprite sheet"),
				100, 32, 512, ResizeFrame)) as GUIIntSlider;

			_guiSide.Add( new GUISpace());
			_guiCurrentFrame = _guiSide.Add( new GUIIntSlider( new GUIContent( "Current Frame"), 
				0, 0, _guiFrameCount.value-1, CurrentFrameChanged)) as GUIIntSlider;

			_guiSide.Add( new GUISpace());
			_guiPositionOffset = _guiSide.Add( new GUIVector3Field( new GUIContent( "Position Offset"), OffsetChanged)) as GUIVector3Field;
			_guiScaleOffset = _guiSide.Add( new GUISlider( new GUIContent( "Scale Offset"), 0.0f, -10.0f, 10.0f, OffsetChanged)) as GUISlider;

			_guiSide.Add( new GUISpace());
			_guiAnimationClips = _guiSide.Add( new GUIPopup( new GUIContent( "Animation Clips"), null, 0, AnimationClipChanged)) as GUIPopup;
			_guiStartRotation = _guiSide.Add( new GUIVector3Field( new GUIContent( "Start Rotation"), RotationChanged)) as GUIVector3Field;
			_guiEndRotation = _guiSide.Add( new GUIVector3Field( new GUIContent( "End Rotation"), RotationChanged)) as GUIVector3Field;
			_guiLoopCount = _guiSide.Add( new GUIIntSlider( new GUIContent( "Loop Count"), 1, 1, 10, RotationChanged)) as GUIIntSlider;
			_guiPingPong = _guiSide.Add( new GUIToggle( new GUIContent( "Pingpong"), RotationChanged)) as GUIToggle;

			_guiSide.Add( new GUISpace());
			GUIColorField outlineColor = _guiSide.Add( new GUIColorField( new GUIContent( "Outline Color"), 
				OutlineColorChanged)) as GUIColorField;
			GUISlider outlineThreshold = _guiSide.Add( new GUISlider( new GUIContent( "Outline Threshold"), 
				0.05f, 0.0f, 0.05f, OutlineThresholdChanged)) as GUISlider;

			_guiSide.Add( new GUISpace());
			_guiSide.Add( new GUIButton( new GUIContent( "Render"), RenderModel));

			_guiPreview = _gui.Add( new GUIVertical( GUILayout.ExpandWidth( true), GUILayout.ExpandHeight( true))) as GUIVertical;
			_guiPreview.shouldStoreLastRect = true;

			InitPreviewRenderTexture();
			InitPreviewCamera();
			InitRootGameObject();
			RenderPreview( 0);

			_guiStartRotation.vector = Vector3.zero;
			_guiEndRotation.vector = new Vector3( 0.0f, 180.0f, 0.0f);
			outlineColor.color = _previewOutline.outlineColor;
			outlineThreshold.value = _previewOutline.depthThreshold;
		}

		void OnDisable()
		{
			_gui = null;
			_guiSide = null;
			_guiPreview = null;
			_previewTexture = null;
			_rootGameObject = null;
			_modelGameObject = null;
		}

		void Update()
		{
			float fps = 1.0f / 10.0f;

			if( _lastFrameTime <= Time.realtimeSinceStartup - fps)
			{
				_lastFrameTime = Time.realtimeSinceStartup;
			}
		}

		void OnGUI()
		{
			if( _gui != null)
			{
				_gui.OnGUI();

				if( Event.current.rawType == EventType.Repaint && _previewTexture != null && _previewCamera != null)
				{
					Rect rect = SizeRectToFit( new Rect( 0.0f, 0.0f, _previewCamera.pixelWidth, _previewTexture.height),
											   _guiPreview.lastRect);
					EditorGUI.DrawPreviewTexture( rect, _previewTexture);
				}
			}
		}

		#region GUI Callbacks

		private void GameObjectChanged( GUIBase sender)
		{
			GUIObjectField<GameObject> gameObjectField = sender as GUIObjectField<GameObject>;

			if( _modelGameObject != null)
			{
				_modelGameObject.transform.parent = null;
				DestroyImmediate( _modelGameObject);
			}

			_modelGameObject = GameObject.Instantiate( gameObjectField.value);
			_modelGameObject.name = _ModelName;
			_modelGameObject.transform.parent = _rootGameObject.transform;
			_modelGameObject.transform.localPosition = Vector3.zero;

			Transform[] children = _modelGameObject.GetComponentsInChildren<Transform>(true);
			foreach( Transform child in children)
			{
				child.gameObject.hideFlags = HideFlags.HideAndDontSave;
				child.gameObject.layer = _PreviewLayer;
			}

			UpdateAnimationClipsPopup();
			ScaleModelToFitCamera();

			RenderPreview( _guiCurrentFrame.value);
		}

		private void ResizeFrame( GUIBase sender)
		{
			if( _previewTexture == null || _previewCamera == null) { return; }

			InitPreviewRenderTexture();
			_previewCamera.targetTexture = _previewTexture;

			ScaleModelToFitCamera();
			RenderPreview( _guiCurrentFrame.value);
		}

		private void FrameCountChanged( GUIBase sender)
		{
			_guiCurrentFrame.maxValue = _guiFrameCount.value - 1;
			_guiCurrentFrame.value = Math.Min( _guiFrameCount.value, _guiCurrentFrame.value);
		}

		private void CurrentFrameChanged( GUIBase sender)
		{
			RenderPreview( _guiCurrentFrame.value);
		}

		private void OffsetChanged( GUIBase sender)
		{
			ScaleModelToFitCamera();
			RenderPreview( _guiCurrentFrame.value);
		}

		private void AnimationClipChanged( GUIBase sender)
		{
			RenderPreview( _guiCurrentFrame.value);
		}

		private void RotationChanged( GUIBase sender)
		{
			RenderPreview( _guiCurrentFrame.value);
		}

		private void OutlineColorChanged( GUIBase sender)
		{
			GUIColorField color = sender as GUIColorField;
			_previewOutline.outlineColor = color.color;
			RenderPreview( _guiCurrentFrame.value);
		}

		private void OutlineThresholdChanged( GUIBase sender)
		{
			GUISlider threshold = sender as GUISlider;
			_previewOutline.depthThreshold = threshold.value;
			RenderPreview( _guiCurrentFrame.value);
		}

		private void RenderModel( GUIBase sender)
		{
			int frameCount = _guiFrameCount.value;
			Vector2 sheetDimensions = GetSheetDimensions();
			RenderTexture sheet = new RenderTexture( (int)sheetDimensions.x, (int)sheetDimensions.y, 0);
			Color oldBackground = _previewCamera.backgroundColor;
			_previewCamera.backgroundColor = Color.clear;

			RenderTexture.active = sheet;
			GL.Clear( true, true, Color.clear);
			RenderTexture.active = null;

			for( int i=0; i<frameCount; i++)
			{
				RenderPreview( i);
				RenderTexture.active = sheet;
				GL.PushMatrix();
				GL.LoadPixelMatrix( 0, sheet.width, sheet.height, 0);
				Graphics.DrawTexture( GetFrameRect( i), _previewTexture);
				GL.PopMatrix();
				RenderTexture.active = null;

				EditorUtility.DisplayProgressBar( "Rendering", "Rendering frames", (float)i / (float)frameCount);
			}
			_previewCamera.backgroundColor = oldBackground;
			RenderTexture.active = sheet;
			Texture2D sheetTexture = new Texture2D( sheet.width, sheet.height);
			sheetTexture.ReadPixels( new Rect( Vector2.zero, new Vector2( sheet.width, sheet.height)), 0, 0);
			RenderTexture.active = null;

			string path = "Assets/TestSheet.png";
			SaveTexture( sheetTexture, path);
			AssetDatabase.Refresh( ImportAssetOptions.ForceUpdate);
			UpdateSpriteImportSettings( path);

			EditorUtility.ClearProgressBar();

			// Needed to display original background color
			RenderPreview( _guiCurrentFrame.value);
		}

		#endregion

		private void SaveTexture( Texture2D texture, string assetPath)
		{
			string dataPath = Application.dataPath.Substring( 0, Application.dataPath.Length - "/Assets".Length);
			string path = Path.Combine( dataPath, assetPath);

			File.WriteAllBytes( path, texture.EncodeToPNG());
		}

		private void UpdateSpriteImportSettings( string path)
		{
			int frameCount = _guiFrameCount.value;
			SpriteMetaData[] spriteData = new SpriteMetaData[ frameCount];
			for( int i=0; i<frameCount; i++)
			{
				spriteData[i].name = "Sprite_ " + i;
				spriteData[i].rect = GetFrameRect( i, true);
			}

			TextureImporter importer = AssetImporter.GetAtPath( path) as TextureImporter;
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Multiple;
			importer.spritesheet = spriteData;
			importer.textureFormat = TextureImporterFormat.AutomaticTruecolor;
			importer.filterMode = FilterMode.Point;
			importer.SaveAndReimport();
			AssetDatabase.ImportAsset( path);
		}

		private void InitPreviewRenderTexture()
		{
			_previewTexture = new RenderTexture( _guiFrameWidth.value, _guiFrameHeight.value, 0);
			_previewTexture.filterMode = FilterMode.Point;
			_previewTexture.hideFlags = HideFlags.HideAndDontSave;
		}

		private void InitPreviewCamera()
		{

			GameObject cameraGO = GameObject.Find( _CameraName);
			if( cameraGO == null)
			{
				cameraGO = EditorUtility.CreateGameObjectWithHideFlags( _CameraName, 
																		HideFlags.HideAndDontSave, 
																		typeof(Camera), 
																		typeof( PixelOutlineEffect));
			}

			cameraGO.hideFlags = HideFlags.HideAndDontSave;

			_previewCamera = cameraGO.GetComponent<Camera>();
			_previewOutline = cameraGO.GetComponent<PixelOutlineEffect>();
			_previewOutline.outlineColor = Color.black;
			_previewOutline.depthThreshold = 0.0001f;

			_previewCamera.targetTexture = _previewTexture;
			_previewCamera.clearFlags = CameraClearFlags.SolidColor;
			_previewCamera.backgroundColor = Color.gray;
			_previewCamera.cullingMask = 1 << _PreviewLayer;
			_previewCamera.transform.position = new Vector3( 0.0f, 0.0f, -6.0f);
			_previewCamera.enabled = false;
		}

		private void InitRootGameObject()
		{
			Debug.Log( "InitRootGameObject");
			_rootGameObject = GameObject.Find( _RootName);
			if( _rootGameObject == null)
			{
				Debug.Log("Creating preview root");
				_rootGameObject = EditorUtility.CreateGameObjectWithHideFlags( _RootName, 
																			   HideFlags.HideAndDontSave);
			}
			else
			{
				Debug.Log( "Root GameObject already exists");
			}

			// Use transform.Find, as model may be deactivated
			Transform transform = _rootGameObject.transform.Find( _ModelName);
			_modelGameObject = transform == null ? null : transform.gameObject;
			if( _modelGameObject == null)
			{
				Debug.Log("Creating model gameobject");

				_modelGameObject = GameObject.CreatePrimitive( PrimitiveType.Sphere);
				_modelGameObject.hideFlags = HideFlags.HideAndDontSave;
				_modelGameObject.name = _ModelName;
				_modelGameObject.transform.parent = _rootGameObject.transform;
			}
			else
			{
				Debug.Log("Model gameobject already exists");
			}

			_rootGameObject.transform.position = Vector3.zero;
			_rootGameObject.layer = _PreviewLayer;

			_modelGameObject.transform.localPosition = Vector3.zero;
			_modelGameObject.layer = _PreviewLayer;
			_modelGameObject.SetActive( false);
		}

		private void UpdateAnimationClipsPopup()
		{
			Animator animator = _modelGameObject.GetComponentInChildren<Animator>( true);
			GUIContent[] options = null;

			if( animator != null && 
				animator.runtimeAnimatorController != null &&
				animator.runtimeAnimatorController.animationClips != null &&
				animator.runtimeAnimatorController.animationClips.Length > 0)
			{
				AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
				int count = clips.Length;
				options = new GUIContent[ count];

				for( int i=0; i<count; i++)
				{
					options[i] = new GUIContent( clips[i].name);
				}
			}

			_guiAnimationClips.displayedOptions = options;
			_guiAnimationClips.isEnabled = options != null;
		}

		private Rect SizeRectToFit( Rect input, Rect container, bool clip=true)
		{
			float scale = Mathf.Min( container.width/input.width, container.height/input.height);
			scale = Mathf.Max( 1.0f, Mathf.Floor( scale));
			input.width *= scale;
			input.height *= scale;
			input.x = (container.width - input.width) * 0.5f + container.x;
			input.y = (container.height - input.height) * 0.5f + container.y;

			if( clip &&
				(input.x < container.x || input.y < container.y ||
					input.xMax > container.xMax || input.yMax > container.yMax))
			{
				input.x = Mathf.Max( input.x, container.x);
				input.y = Mathf.Max( input.y, container.y);
				input.width = Mathf.Min( input.x + input.width, container.width);
				input.height = Mathf.Min( input.y + input.height, container.height);
			}

			return input;
		}

		private void ScaleModelToFitCamera()
		{
			if( _modelGameObject == null || _previewCamera == null) { return; }

			Bounds bounds = GetBounds( _modelGameObject);
			float maxDimension = Mathf.Max( bounds.extents.x, Mathf.Max( bounds.extents.y, bounds.extents.z)) * 2.0f;
			maxDimension += maxDimension * 0.5f;

			float distance = Mathf.Abs( _previewCamera.transform.position.z);
			Vector3 bottomLeft = _previewCamera.ViewportToWorldPoint( new Vector3( 0, 0, distance));
			Vector3 topRight = _previewCamera.ViewportToWorldPoint( new Vector3( 1, 1, distance));
			Vector3 delta = topRight - bottomLeft;
			float minViewDimension = Mathf.Min( Mathf.Abs( delta.x), Mathf.Abs( delta.y));
			float scale = minViewDimension / maxDimension;
			scale += _guiScaleOffset.value;

			_modelGameObject.transform.localScale = new Vector3( scale, scale, scale);
			Vector3 center = (bounds.center + _guiPositionOffset.vector) * -scale;
			_modelGameObject.transform.position = center;
		}

		private Bounds GetBounds( GameObject gameObject)
		{
			gameObject.transform.localScale = Vector3.one;
			gameObject.transform.position = Vector3.zero;

			Bounds bounds = new Bounds();
			Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>( false);

			foreach( Renderer renderer in renderers)
			{
				bounds.Encapsulate( renderer.bounds);
			}

			return bounds;
		}

		private int GetFramesPerRow()
		{
			return (int) Mathf.Ceil( Mathf.Sqrt( _guiFrameCount.value));
		}

		private Vector2 GetSheetDimensions()
		{
			int framesPerRow = GetFramesPerRow();
			int framesPerColumn = (int) Mathf.Ceil( (float)_guiFrameCount.value / (float)framesPerRow);
			return new Vector2( framesPerRow * _previewTexture.width, framesPerColumn * _previewTexture.height);
		}

		private void SetupFrame( int frame)
		{
			float t = (float)frame / (float)(_guiFrameCount.value - 1);

			float loopedT = t * (float)_guiLoopCount.value;
			t = loopedT - Mathf.Floor( loopedT);

			if( _guiPingPong.isToggled && ((int)loopedT % 2) == 1)
			{
				t = 1.0f - t;
			}

			_rootGameObject.transform.localEulerAngles = Vector3.Lerp( _guiStartRotation.vector, _guiEndRotation.vector, t);

			Animator animator = _modelGameObject.GetComponentInChildren<Animator>( true);
			if( animator != null && 
				animator.runtimeAnimatorController != null &&
				animator.runtimeAnimatorController.animationClips != null)
			{
				AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
				int index = _guiAnimationClips.selectedIndex;

				AnimationMode.BeginSampling();
				AnimationMode.SampleAnimationClip( animator.gameObject, clips[ index], t * clips[ index].length);
				AnimationMode.EndSampling();
			}
		}

		private Rect GetFrameRect( int frameIndex, bool bottomToTop=false)
		{
			Vector2 sheetDimensions = GetSheetDimensions();
			int framesPerRow = GetFramesPerRow();
			int w = _previewTexture.width;
			int h = _previewTexture.height;
			int x = (frameIndex % framesPerRow) * w;
			int y = (frameIndex / framesPerRow) * h;

			if( bottomToTop) { y = (int)sheetDimensions.y - y - h; }

			return new Rect( x, y, w, h);
		}

		private void RenderPreview( int frame)
		{
			string type = _modelGameObject == null ? "null" : _modelGameObject.GetType().ToString();
			if( _modelGameObject != null)
			{
				_modelGameObject.SetActive( true);
				AnimationMode.StartAnimationMode();
				SetupFrame( frame);
				_previewCamera.Render();
				AnimationMode.StopAnimationMode();
				_modelGameObject.SetActive( false);
			}
		}
	}
}
