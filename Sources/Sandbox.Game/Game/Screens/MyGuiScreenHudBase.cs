﻿using Sandbox.Common;
using Sandbox.Definitions;
using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Engine.Utils;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using System;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Gui;
using VRage.Generics;
using VRage.Utils;
using VRageMath;
using VRageRender;
using Color = VRageMath.Color;
using MyGuiConstants = Sandbox.Graphics.GUI.MyGuiConstants;
using Vector2 = VRageMath.Vector2;

//  This class draws crosshair, angles and all HUD stuff.
//
//  Coordinates are ALMOST in <0..1> range, but think about this:
//  Screen size -> width is always 1.0, but height depends on aspect ratio, so usualy it is 0.8 or something.
//
//  Every line is composed of three rectangles. Middle rectangle is for the main line and two other represents circles at the end.
//  IMPORTANT: Above line apply only if DRAW_LINE_ENDS enables it. Reason why I don't use it is because I had problems making that
//  end texture correct and without ends it's even faster (less rectangles to draw)
namespace Sandbox.Game.Gui
{
    public class MyGuiScreenHudBase : MyGuiScreenBase
    {
        protected string m_atlas;
        public string TextureAtlas { get { return m_atlas; } }

        protected MyAtlasTextureCoordinate[] m_atlasCoords;
        protected float m_textScale;
        protected StringBuilder m_hudIndicatorText = new StringBuilder();
        protected StringBuilder m_helperSB = new StringBuilder();

        protected MyObjectsPoolSimple<MyHudText> m_texts;

        public MyGuiScreenHudBase()
            : base(Vector2.Zero, null, null)
        {
            CanBeHidden = true;
            CanHideOthers = false;
            CanHaveFocus = false;
            m_drawEvenWithoutFocus = true;
            m_closeOnEsc = false;
            m_texts = new MyObjectsPoolSimple<MyHudText>(MyHudConstants.MAX_HUD_TEXTS_COUNT);
        }

        public override string GetFriendlyName()
        {
            return "MyGuiScreenHudBase";
        }

        public override void LoadContent()
        {
            LoadTextureAtlas(out m_atlas, out m_atlasCoords);
            base.LoadContent();
        }

        public static void LoadTextureAtlas(out string atlasFile, out MyAtlasTextureCoordinate[] atlasCoords)
        {
            MyTextureAtlasUtils.LoadTextureAtlas(Sandbox.Engine.Utils.MyEnumsToStrings.HudTextures, "Textures\\HUD\\", "Textures\\HUD\\HudAtlas.tai", out atlasFile, out atlasCoords);
        }

        public override void UnloadContent()
        {
            m_atlas = null;
            m_atlasCoords = null;
            base.UnloadContent();
        }

        public static Vector2 ConvertHudToNormalizedGuiPosition(ref Vector2 hudPos)
        {
            var safeFullscreenRectangle = MyGuiManager.GetSafeFullscreenRectangle();
            var safeFullscreenSize = new Vector2(safeFullscreenRectangle.Width, safeFullscreenRectangle.Height);
            var safeFullscreenOffset = new Vector2(safeFullscreenRectangle.X, safeFullscreenRectangle.Y);

            var safeGuiRectangle = MyGuiManager.GetSafeGuiRectangle();
            var safeGuiSize = new Vector2(safeGuiRectangle.Width, safeGuiRectangle.Height);
            var safeGuiOffset = new Vector2(safeGuiRectangle.X, safeGuiRectangle.Y);

            return ((hudPos * safeFullscreenSize + safeFullscreenOffset) - safeGuiOffset) / safeGuiSize;
        }

        protected static Vector2 ConvertNormalizedGuiToHud(ref Vector2 normGuiPos)
        {
            var safeFullscreenRectangle = MyGuiManager.GetSafeFullscreenRectangle();
            var safeFullscreenSize = new Vector2(safeFullscreenRectangle.Width, safeFullscreenRectangle.Height);
            var safeFullscreenOffset = new Vector2(safeFullscreenRectangle.X, safeFullscreenRectangle.Y);

            var safeGuiRectangle = MyGuiManager.GetSafeGuiRectangle();
            var safeGuiSize = new Vector2(safeGuiRectangle.Width, safeGuiRectangle.Height);
            var safeGuiOffset = new Vector2(safeGuiRectangle.X, safeGuiRectangle.Y);

            return ((normGuiPos * safeGuiSize + safeGuiOffset) - safeFullscreenOffset) / safeFullscreenSize;
        }

        public static void HandleSelectedObjectHighlight(MyHudSelectedObject selection, MyHudObjectHighlightStyleData? data)
        {
            if (selection.PreviousObject.Instance != null)
                RemoveObjectHighlightInternal(ref selection.PreviousObject, true);

            switch (selection.State)
            {
                case MyHudSelectedObjectState.VisibleStateSet:
                {
                    if (selection.Visible && (selection.CurrentObject.Style == MyHudObjectHighlightStyle.HighlightStyle1
                            || selection.VisibleRenderID != selection.CurrentObject.Instance.RenderObjectID))
                        MyGuiScreenHudBase.DrawSelectedObjectHighlight(selection, data);

                    break;
                }
                case MyHudSelectedObjectState.MarkedForVisible:
                {
                    MyGuiScreenHudBase.DrawSelectedObjectHighlight(selection, data);
                    break;
                }
                case MyHudSelectedObjectState.MarkedForNotVisible:
                {
                    MyGuiScreenHudBase.RemoveObjectHighlight(selection);
                    break;
                }
            }
        }

        private static void DrawSelectedObjectHighlight(MyHudSelectedObject selection, MyHudObjectHighlightStyleData? data)
        {
            if (selection.InteractiveObject.RenderObjectID == -1)
            {
                // Invalid render object ID
                return;
            }

            switch (selection.HighlightStyle)
            {
                case MyHudObjectHighlightStyle.HighlightStyle1:
                {
                    DrawSelectedObjectHighlight1(selection, data.Value.AtlasTexture, data.Value.TextureCoord);
                    break;
                }
                case MyHudObjectHighlightStyle.HighlightStyle2:
                {
                    int[] sectionIndices = selection.SectionIndices;
                    if (sectionIndices != null && selection.SectionIndices.Length == 0
                        && selection.SubpartIndices == null)
                    {
                        // There was a problem with sections look-up, fallback to previous highlight style
                        DrawSelectedObjectHighlight1(selection, data.Value.AtlasTexture, data.Value.TextureCoord);
                        break;
                    }

                    DrawSelectedObjectHighlight2(selection);
                    break;
                }
                case MyHudObjectHighlightStyle.None:
                {
                    return;
                }
                default:
                    throw new Exception("Unknown highlight style");
            }

            selection.Visible = true;
        }

        private static void RemoveObjectHighlight(MyHudSelectedObject selection)
        {
            RemoveObjectHighlightInternal(ref selection.CurrentObject, false);
        
            selection.Visible = false;
        }

        private static void RemoveObjectHighlightInternal(ref MyHudSelectedObjectStatus status, bool reset)
        {
            switch (status.Style)
            {
                case MyHudObjectHighlightStyle.HighlightStyle2:
                {
                    MyRenderProxy.UpdateModelHighlight((uint)status.Instance.RenderObjectID, null, status.SubpartIndices, null, -1, 0);
                    break;
                }
            }

            if (reset)
                status.Reset();
        }

        public override bool Update(bool hasFocus)
        {
            bool retval = base.Update(hasFocus);

            if (MySandboxGame.Config.ShowCrosshair)
            {
                MyHud.Crosshair.Update();
            }

            return retval;
        }

        public override bool Draw()
        {
            bool retval = base.Draw();

            if (MySandboxGame.Config.ShowCrosshair && !MyHud.MinimalHud)
            {
                MyHud.Crosshair.Draw(m_atlas, m_atlasCoords);
            }

            return retval;
        }

        private static void DrawSelectedObjectHighlight2(MyHudSelectedObject selection)
        {
            Color color = MyDefinitionManager.Static.EnvironmentDefinition.ContourHighlightColor;
            float thickness = MyDefinitionManager.Static.EnvironmentDefinition.ContourHighlightThickness;
            ulong pulseTimeInFrames = (ulong)Math.Round(MyDefinitionManager.Static.EnvironmentDefinition.HighlightPulseInSeconds * MyEngineConstants.UPDATE_STEPS_PER_SECOND);
            MyRenderProxy.UpdateModelHighlight((uint)selection.InteractiveObject.RenderObjectID, selection.SectionIndices, selection.SubpartIndices, color, thickness, pulseTimeInFrames);
        }

        public static void DrawSelectedObjectHighlight1(MyHudSelectedObject selection, string atlasTexture, MyAtlasTextureCoordinate textureCoord)
        {
            var rect = MyGuiManager.GetSafeFullscreenRectangle();

            Vector2 hudSize = new Vector2(rect.Width, rect.Height);

            var worldViewProj = selection.InteractiveObject.ActivationMatrix * MySector.MainCamera.ViewMatrix * (MatrixD)MySector.MainCamera.ProjectionMatrix;
            BoundingBoxD screenSpaceAabb = new BoundingBoxD(-Vector3D.One / 2, Vector3D.One / 2).Transform(worldViewProj);
            var min = new Vector2((float)screenSpaceAabb.Min.X, (float)screenSpaceAabb.Min.Y);
            var max = new Vector2((float)screenSpaceAabb.Max.X, (float)screenSpaceAabb.Max.Y);

            var minToMax = min - max;

            min = min * 0.5f + 0.5f * Vector2.One;
            max = max * 0.5f + 0.5f * Vector2.One;

            min.Y = 1 - min.Y;
            max.Y = 1 - max.Y;

            float textureScale = (float)Math.Pow(Math.Abs(minToMax.X), 0.35f) * 2.5f;

            if (selection.InteractiveObject.ShowOverlay)
            {
                BoundingBoxD one = new BoundingBoxD(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));
                Color color = Color.Gold;
                color *= 0.4f;
                var m = selection.InteractiveObject.ActivationMatrix;
                var localToWorld = MatrixD.Invert(selection.InteractiveObject.WorldMatrix);
                //MySimpleObjectDraw.DrawTransparentBox(ref m, ref one, ref color, MySimpleObjectRasterizer.Solid, 0, 0.05f, "Square", null, true);

                MySimpleObjectDraw.DrawAttachedTransparentBox(ref m, ref one, ref color, selection.InteractiveObject.RenderObjectID,
                    ref localToWorld, MySimpleObjectRasterizer.Solid, 0, 0.05f, "Square", null, true);
            }

            if (MyFakes.ENABLE_USE_OBJECT_CORNERS)
            {
                DrawSelectionCorner(atlasTexture, selection, textureCoord, hudSize, min, -Vector2.UnitY, textureScale);
                DrawSelectionCorner(atlasTexture, selection, textureCoord, hudSize, new Vector2(min.X, max.Y), Vector2.UnitX, textureScale);
                DrawSelectionCorner(atlasTexture, selection, textureCoord, hudSize, new Vector2(max.X, min.Y), -Vector2.UnitX, textureScale);
                DrawSelectionCorner(atlasTexture, selection, textureCoord, hudSize, max, Vector2.UnitY, textureScale);
            }
        }

        public static void DrawSelectionCorner(string atlasTexture, MyHudSelectedObject selection, MyAtlasTextureCoordinate textureCoord, Vector2 scale, Vector2 pos, Vector2 rightVector, float textureScale)
        {
            if (MyVideoSettingsManager.IsTripleHead())
                pos.X = pos.X * 3;

            VRageRender.MyRenderProxy.DrawSpriteAtlas(
                atlasTexture,
                pos,
                textureCoord.Offset,
                textureCoord.Size,
                rightVector,
                scale,
                selection.Color,
                selection.HalfSize / MyGuiManager.GetHudSize() * textureScale);
        }

        public static void DrawFogNotification(ref Vector2 centerPosition, ref Vector2 textSize)
        {
            Vector2 fogFadeSize = textSize * new Vector2(1.6f, 8.0f);
            DrawFogInternal(MyGuiConstants.FOG_SMALL, ref centerPosition, ref fogFadeSize, 1, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
        }

        public static void DrawFog(ref Vector2 centerPosition, ref Vector2 textSize, float fogAlphaMultiplier = 1,
            MyGuiDrawAlignEnum alignment = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER)
        {
            Vector2 fogFadeSize = textSize * new Vector2(1.5f, 3.0f);
            DrawFogInternal(MyGuiConstants.FOG_SMALL1, ref centerPosition, ref fogFadeSize, fogAlphaMultiplier, alignment);
        }

        /// <summary>
        /// Draws fog (eg. background for notifications) at specified position in normalized GUI coordinates.
        /// </summary>
        private static void DrawFogInternal(string texture, ref Vector2 centerPosition, ref Vector2 size, float fogAlphaMultiplier, MyGuiDrawAlignEnum alignment)
        {
            Color color = new Color(0, 0, 0, (byte)(255 * 0.85f * fogAlphaMultiplier));
            MyGuiManager.DrawSpriteBatch(texture, centerPosition, size, color, alignment, MyVideoSettingsManager.IsTripleHead());
        }

        public MyHudText AllocateText()
        {
            return m_texts.Allocate();
        }

        public void DrawTexts()
        {
            if (m_texts.GetAllocatedCount() <= 0) return;

            for (int i = 0; i < m_texts.GetAllocatedCount(); i++)
            {
                MyHudText text = m_texts.GetAllocatedItem(i);
                if (text.GetStringBuilder().Length == 0) continue;

                var font = text.Font;
                text.Position /= MyGuiManager.GetHudSize();
                var normalizedCoord = ConvertHudToNormalizedGuiPosition(ref text.Position);

                Vector2 textSize = MyGuiManager.MeasureString(font, text.GetStringBuilder(), MyGuiSandbox.GetDefaultTextScaleWithLanguage());
                textSize.X *= 0.9f;
                textSize.Y *= 0.7f;
                textSize *= text.Scale;
                MyGuiScreenHudBase.DrawFog(ref normalizedCoord, ref textSize, text.Color.A / 255f, text.Alignement);

                MyGuiManager.DrawString(font, text.GetStringBuilder(), normalizedCoord, text.Scale, colorMask: text.Color, drawAlign: text.Alignement);
            }

            m_texts.ClearAllAllocated();
        }

        public MyAtlasTextureCoordinate GetTextureCoord(MyHudTexturesEnum texture)
        {
            return m_atlasCoords[(int)texture];
        }
    }
}