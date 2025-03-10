﻿using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Monocle;
using Celeste.Mod.ConsistencyTracker.Entities.Summary;

namespace Celeste.Mod.ConsistencyTracker.Utility {
    public static class DrawHelper {
        
        public static void Move(ref Vector2 vec, float x, float y) {
            SummaryHud.Move(ref vec, x, y);
        }
        public static Vector2 MoveCopy(Vector2 vec, float x, float y) {
            return SummaryHud.MoveCopy(vec, x, y);
        }

        
        public static Vector2 DrawText(string text, Vector2 pointer, float fontSize, Color color, Vector2 justify = default) {
            Vector2 measure = ActiveFont.Measure(text) * fontSize;
            ActiveFont.Draw(text, pointer, justify, Vector2.One * fontSize, color);
            return measure;
        }

        
        public static void DrawTrapezoid(Vector2 start, float widthTop, float widthBottom, float height, Color color) {
            Vector2[] points = new Vector2[] { 
                start,
                MoveCopy(start, widthTop, 0),
                MoveCopy(start, widthTop / 2 - widthBottom / 2, height),
                MoveCopy(start, widthTop / 2 + widthBottom / 2, height),
            };

            VertexPositionColor[] vertices = new VertexPositionColor[6];
            float depth = 0f;
            vertices[0] = new VertexPositionColor(new Vector3(points[0], depth), color);
            vertices[1] = new VertexPositionColor(new Vector3(points[1], depth), color);
            vertices[2] = new VertexPositionColor(new Vector3(points[2], depth), color);

            vertices[3] = new VertexPositionColor(new Vector3(points[1], depth), color);
            vertices[4] = new VertexPositionColor(new Vector3(points[2], depth), color);
            vertices[5] = new VertexPositionColor(new Vector3(points[3], depth), color);

            Matrix m = Engine.ScreenMatrix;
            string matrix = $"[ {m.M11}, {m.M12}, {m.M13}, {m.M14} ]\n" +
                            $"[ {m.M21}, {m.M22}, {m.M23}, {m.M24} ]\n" +
                            $"[ {m.M31}, {m.M32}, {m.M33}, {m.M34} ]\n" +
                            $"[ {m.M41}, {m.M42}, {m.M43}, {m.M44} ]";
            ConsistencyTrackerModule.Instance.LogOnce("matrix", $"Engine.ScreenMatrix:" +
                $"\n{matrix}" +
                $"\nEngine.ScreenMatrix.Translation: {m.Translation}");

            DrawVertices(Matrix.Identity, vertices);
        }

        public static void DrawVertices(Matrix mat, VertexPositionColor[] vertices) {
            SpriteSortMode ssm = SpriteSortMode.Deferred;
            BlendState bs = BlendState.AlphaBlend;
            SamplerState ss = SamplerState.LinearClamp;
            DepthStencilState dss = DepthStencilState.None;
            RasterizerState rs = RasterizerState.CullCounterClockwise;
            Effect effect = null;

            try {
                GraphicsDevice gd = Draw.SpriteBatch.GraphicsDevice;
                bs = gd.BlendState;
                ss = gd.SamplerStates[0];
                dss = gd.DepthStencilState;
                rs = gd.RasterizerState;
                
            } catch (Exception) { }
            try {
                Draw.SpriteBatch.End(); 
            } catch (Exception) { }
            GFX.DrawVertices(mat, vertices, vertices.Length);
            try { Draw.SpriteBatch.Begin(ssm, bs, ss, dss, rs, effect, mat); } catch (Exception) { }

        }
    }
}
