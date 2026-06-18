using System.Numerics;
using AutoPBR.Core.Models;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Java cuboid UV builder (nested RigBuilder).

    /// <summary>
    /// Builds merged block-model elements whose face UVs follow Java <c>net.minecraft.client.model.ModelPart.Cuboid</c> rules
    /// (<see cref="AddBox"/> / <see cref="BuildCubeUvLayout"/>) or, for renderer billboards without cuboids,
    /// <see cref="AddBillboardPlane"/> (north/south slab, explicit UV corners in texture pixel space).
    /// </summary>
    /// <remarks>
    /// Paths resolved via <see cref="EntityTextureParityCatalog"/> must pass explicit <c>texU</c>/<c>texV</c> (and mirror when the Java
    /// cuboid uses <c>mirror()</c> or geometry IR <c>mirrorU</c> — see <see cref="GeometryIrCuboidMetadata"/>). Omitting UV coordinates invokes <see cref="AllocateUvBox"/> — atlas packing that does not match vanilla.
    /// </remarks>
    internal sealed class RigBuilder(int atlasW, int atlasH)
    {
        private readonly int _atlasW = Math.Max(16, atlasW);
        private readonly int _atlasH = Math.Max(16, atlasH);
        private int _packCursorU;
        private int _packCursorV;
        private int _packRowHeight;
        private readonly List<ModelElement> _elements = [];

        /// <summary>
        /// Java <c>ModelPart</c>-style Euler (radians): apply X, then Y, then Z — same net basis as stacking
        /// <c>RotateX</c> → <c>RotateY</c> → <c>RotateZ</c> on a column-vector mesh.
        /// </summary>
        private static Matrix4x4 ComposeEntityPartEulerRad(float xRot, float yRot, float zRot)
        {
            if (xRot == 0f && yRot == 0f && zRot == 0f)
            {
                return Matrix4x4.Identity;
            }

            return Matrix4x4.Multiply(
                Matrix4x4.CreateRotationZ(zRot),
                Matrix4x4.Multiply(Matrix4x4.CreateRotationY(yRot), Matrix4x4.CreateRotationX(xRot)));
        }

        private static Matrix4x4 RotateAroundPivot(Matrix4x4 rotation, Vector3 pivot) =>
            Matrix4x4.Multiply(
                Matrix4x4.CreateTranslation(pivot),
                Matrix4x4.Multiply(rotation, Matrix4x4.CreateTranslation(-pivot)));

        /// <summary>
        /// Cuboid in model space with Java-style UV: pass <c>texU</c>/<c>texV</c> as <c>texOffs</c> (omit or use <c>-1</c> only for non-parity packed UV).
        /// Java <c>CubeListBuilder.addBox(ox,oy,oz, sx,sy,sz)</c> ends at <c>(ox+sx, oy+sy, oz+sz)</c>; this API takes two diagonal corners <c>(x0,y0,z0)</c>–<c>(x1,y1,z1)</c>.
        /// Use <c>uvSizeW</c>/<c>uvSizeH</c>/<c>uvSizeD</c> when inflated geometry still maps with vanilla integer footprint.
        /// Set <c>mirrorCuboidUv</c> when the Java builder used <c>mirror()</c>, or when lifted IR has <c>mirrorU: true</c>
        /// (<see cref="GeometryIrCuboidMetadata.GetMirrorCuboidUv"/>).
        /// </summary>
        public void AddBox(
            float x0, float y0, float z0,
            float x1, float y1, float z1,
            string texKey,
            float scale,
            float offsetX,
            float offsetY,
            float offsetZ,
            int texU = -1,
            int texV = -1,
            Matrix4x4? localToParent = null,
            float xRot = 0f,
            float yRot = 0f,
            float zRot = 0f,
            Vector3? rotationPivot = null,
            int uvSizeW = -1,
            int uvSizeH = -1,
            int uvSizeD = -1,
            bool mirrorCuboidUv = false,
            string[]? faceMask = null,
            PreviewDepthLayerKind depthLayerKind = PreviewDepthLayerKind.Base,
            int layerOrdinal = 0,
            bool castsShadow = false)
        {
            var extentX = MathF.Abs(x1 - x0);
            var extentY = MathF.Abs(y1 - y0);
            var extentZ = MathF.Abs(z1 - z0);

            // UV footprint follows vanilla texOffs + integer box dimensions on the skin atlas (model texels).
            // Geometry still scales for baby transforms — decouple UV sizes from scaled vertex extents.
            var uw = Math.Max(1, (int)MathF.Round(extentX));
            var uh = Math.Max(1, (int)MathF.Round(extentY));
            var ud = Math.Max(1, (int)MathF.Round(extentZ));
            if (uvSizeW > 0)
            {
                uw = uvSizeW;
                if (uvSizeH > 0)
                {
                    uh = uvSizeH;
                }
                else if (uvSizeD > 0)
                {
                    // [w,0,d] horizontal sheets (Ender Dragon wing membranes).
                    uh = 0;
                }

                if (uvSizeD > 0)
                {
                    ud = uvSizeD;
                }
            }

            var cx = (x0 + x1) * 0.5f;
            var cy = (y0 + y1) * 0.5f;
            var cz = (z0 + z1) * 0.5f;
            var hx = extentX * 0.5f * scale;
            var hy = extentY * 0.5f * scale;
            var hz = extentZ * 0.5f * scale;
            // Only auto-pack when texOffs were omitted (-1,-1). Negative Java origins are valid.
            if (texU < 0 && texV < 0)
            {
                AllocateUvBox(uw, uh, ud, out texU, out texV);
            }

            var centerX = cx + offsetX;
            var centerY = cy + offsetY;
            var centerZ = cz + offsetZ;
            var pivot = rotationPivot ?? new Vector3(centerX, centerY, centerZ);
            var eulerLocal = Matrix4x4.Identity;
            if (xRot != 0f || yRot != 0f || zRot != 0f)
            {
                eulerLocal = RotateAroundPivot(ComposeEntityPartEulerRad(xRot, yRot, zRot), pivot);
            }

            var parent = localToParent ?? Matrix4x4.Identity;
            var meshLocal = Matrix4x4.Multiply(parent, eulerLocal);

            if (EntityRigPoseCapture.IsActive)
            {
                EntityRigPoseCapture.Append(meshLocal);
                return;
            }

            if (faceMask is { Length: 0 })
            {
                return;
            }

            var useNorthSouthUvSpan =
                uvSizeW > 0 &&
                uvSizeH > 0 &&
                faceMask is { Length: > 0 } &&
                IsNorthSouthFaceMaskOnly(faceMask);

            (float[] North, float[] South, float[] West, float[] East, float[] Up, float[] Down) uv;
            if (useNorthSouthUvSpan)
            {
                uv = BuildNorthSouthUvSpanLayout(texU, texV, uvSizeW, uvSizeH, mirrorCuboidUv);
            }
            else
            {
                uv = BuildCubeUvLayout(texU, texV, uw, uh, ud, mirrorCuboidUv);
            }

            var allFaces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
            {
                ["north"] = new() { TextureKey = texKey, Uv = uv.North, RotationDegrees = 0 },
                ["south"] = new() { TextureKey = texKey, Uv = uv.South, RotationDegrees = 0 },
                ["west"] = new() { TextureKey = texKey, Uv = uv.West, RotationDegrees = 0 },
                ["east"] = new() { TextureKey = texKey, Uv = uv.East, RotationDegrees = 0 },
                ["up"] = new() { TextureKey = texKey, Uv = uv.Up, RotationDegrees = 0 },
                ["down"] = new() { TextureKey = texKey, Uv = uv.Down, RotationDegrees = 0 }
            };

            Dictionary<string, ModelFace> faces;
            if (faceMask is { Length: > 0 })
            {
                faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in faceMask)
                {
                    if (allFaces.TryGetValue(name, out var face))
                    {
                        faces[name] = face;
                    }
                }

                // Zero-height horizontal sheets (dragon wing membranes): vanilla only masks "up" but
                // preview must be double-sided so the membrane is visible from below.
                if (uh == 0 &&
                    uvSizeW > 0 &&
                    uvSizeD > 0 &&
                    faceMask.Length == 1 &&
                    faceMask[0].Equals("up", StringComparison.OrdinalIgnoreCase) &&
                    allFaces.TryGetValue("up", out var upFace))
                {
                    faces["down"] = new ModelFace
                    {
                        TextureKey = upFace.TextureKey,
                        Uv = upFace.Uv,
                        RotationDegrees = upFace.RotationDegrees,
                    };
                }

                if (faces.Count == 0)
                {
                    return;
                }
            }
            else
            {
                faces = allFaces;
            }

            _elements.Add(new ModelElement
            {
                From = [centerX - hx, centerY - hy, centerZ - hz],
                To = [centerX + hx, centerY + hy, centerZ + hz],
                LocalToParent = meshLocal,
                Faces = faces,
                DepthLayerKind = depthLayerKind,
                LayerOrdinal = layerOrdinal,
                CastsShadow = castsShadow,
                MirrorCuboidUv = mirrorCuboidUv,
            });
        }

        /// <summary>
        /// Thin north/south slab for renderer billboards (no Java cuboid cross-unfold). <paramref name="u0"/>–<paramref name="v1"/>
        /// are texture pixel corners on the entity sheet (same convention as <see cref="AddBox"/> float UV inputs where used).
        /// </summary>
        public void AddBillboardPlane(
            string texKey,
            float halfWidth,
            float halfHeight,
            float halfThick,
            float centerY,
            float u0,
            float v0,
            float u1,
            float v1,
            Matrix4x4 localToParent)
        {
            var uvNorth = new[] { u0, v0, u1, v1 };
            var uvSouth = new[] { u0, v0, u1, v1 };
            var y0 = centerY - halfHeight;
            var y1 = centerY + halfHeight;
            if (EntityRigPoseCapture.IsActive)
            {
                EntityRigPoseCapture.Append(localToParent);
                return;
            }

            _elements.Add(new ModelElement
            {
                From = [-halfWidth, y0, -halfThick],
                To = [halfWidth, y1, halfThick],
                LocalToParent = localToParent,
                Faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase)
                {
                    ["north"] = new() { TextureKey = texKey, Uv = uvNorth, RotationDegrees = 0 },
                    ["south"] = new() { TextureKey = texKey, Uv = uvSouth, RotationDegrees = 0 },
                },
            });
        }

        private void AllocateUvBox(int width, int height, int depth, out int texU, out int texV)
        {
            var boxW = depth * 2 + width * 2 + 2;
            var boxH = depth + height + 2;
            if (_packCursorU + boxW > _atlasW)
            {
                _packCursorU = 0;
                _packCursorV += _packRowHeight;
                _packRowHeight = 0;
            }

            if (_packCursorV + boxH > _atlasH)
            {
                // Wrap safely; not perfect but avoids invalid UV overflow in compact atlases.
                _packCursorU = 0;
                _packCursorV = 0;
                _packRowHeight = 0;
            }

            texU = _packCursorU;
            texV = _packCursorV;
            _packCursorU += boxW;
            _packRowHeight = Math.Max(_packRowHeight, boxH);
        }

        private static (float[] North, float[] South, float[] West, float[] East, float[] Up, float[] Down)
            BuildCubeUvLayout(int u, int v, int w, int h, int d, bool mirrorCuboidUv = false)
        {
            _ = mirrorCuboidUv;
            // Minecraft-style unfolded cube layout from texOffs(u, v) + addBox(w, h, d).
            var west = new float[] { u, v + d, u + d, v + d + h };
            var north = new float[] { u + d, v + d, u + d + w, v + d + h };
            var east = new float[] { u + d + w, v + d, u + d + w + d, v + d + h };
            var south = new float[] { u + d + w + d, v + d, u + d + w + d + w, v + d + h };
            // Java Direction.DOWN / UP rectangles (26.1.2 ModelPart.Cube javap); keys match physical face names.
            // UP is intentionally passed with reversed V bounds: (u+d+w, v+d) to (u+d+2w, v).
            var down = new float[] { u + d, v, u + d + w, v + d };
            var up = new float[] { u + d + w, v + d, u + d + w + w, v };

            return (north, south, west, east, up, down);
        }

        /// <summary>
        /// Direction-mask / <c>uvSpan</c> north–south sheets: <c>uvOrigin</c> is the first face corner (no full-box
        /// <c>+d</c> padding). Opposite face is offset by <c>w + TemplateDepthGap</c> on U (vanilla template depth 2).
        /// </summary>
        private const int NorthSouthUvSpanTemplateDepthGap = 2;

        private static bool IsNorthSouthFaceMaskOnly(string[] faceMask)
        {
            foreach (var name in faceMask)
            {
                if (!string.Equals(name, "north", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "south", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return faceMask.Length > 0;
        }

        private static (float[] North, float[] South, float[] West, float[] East, float[] Up, float[] Down)
            BuildNorthSouthUvSpanLayout(int u, int v, int w, int h, bool mirrorCuboidUv = false)
        {
            _ = mirrorCuboidUv;
            var north = new float[] { u, v, u + w, v + h };
            var southU0 = u + w + NorthSouthUvSpanTemplateDepthGap;
            var south = new float[] { southU0, v, southU0 + w, v + h };

            var empty = Array.Empty<float>();
            return (north, south, empty, empty, empty, empty);
        }

        public MergedJavaBlockModel Build(
            string skinTextureRef,
            IReadOnlyDictionary<string, string>? additionalTextureRefs = null)
        {
            var textures = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["skin"] = skinTextureRef
            };

            if (additionalTextureRefs is not null)
            {
                foreach (var kv in additionalTextureRefs)
                {
                    textures[kv.Key] = kv.Value;
                }
            }

            return new MergedJavaBlockModel
            {
                Elements = _elements,
                Textures = textures
            };
        }

        internal int EmittedElementCount => _elements.Count;

        internal void ClearEmittedElements()
        {
            _elements.Clear();
            _packCursorU = 0;
            _packCursorV = 0;
            _packRowHeight = 0;
        }
    }
}
