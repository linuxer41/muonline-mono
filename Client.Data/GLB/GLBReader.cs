using SharpGLTF.Schema2;
using System.Linq;
using System.Numerics;
using System.IO;

namespace Client.Data.GLB
{
    public class GLBReader : BaseReader<GLBModel>
    {
        /// <summary>
        /// Public method to read GLB from byte array
        /// Note: GLB files are not encrypted, unlike BMD files which may be encrypted based on version.
        /// </summary>
        public GLBModel ReadFromBuffer(byte[] buffer) => Read(buffer);

        protected override GLBModel Read(byte[] buffer)
        {
            var model = ModelRoot.ParseGLB(buffer);

            var meshes = new List<GLBMesh>();
            var bones = new List<GLBBone>();
            var actions = new List<GLBAction>();

            // For simplicity, assume no bones and no actions for now
            // TODO: Implement bone and animation conversion

            // Process default scene
            var scene = model.DefaultScene;
            foreach (var node in scene.VisualChildren)
            {
                ProcessNode(node, meshes);
            }

            var glbModel = new GLBModel
            {
                Version = 0x0C,
                Name = model.Asset.Extras?.ToString() ?? "GLB Model",
                Meshes = meshes.ToArray(),
                Bones = bones.ToArray(),
                Actions = actions.ToArray()
            };

            Console.WriteLine($"GLB loaded: {glbModel.Name}, Meshes: {glbModel.Meshes.Length}, Bones: {glbModel.Bones.Length}, Actions: {glbModel.Actions.Length}");
            foreach (var mesh in glbModel.Meshes)
            {
                Console.WriteLine($"  Mesh: {mesh.TexturePath}, Vertices: {mesh.Vertices.Length}, Normals: {mesh.Normals.Length}, TexCoords: {mesh.TexCoords.Length}, Triangles: {mesh.Triangles.Length}");
            }

            return glbModel;
        }

        private void ProcessNode(Node node, List<GLBMesh> meshes)
        {
            if (node.Mesh != null)
            {
                foreach (var primitive in node.Mesh.Primitives)
                {
                    var mesh = ConvertPrimitiveToGLBMesh(primitive, node.Mesh.Name);
                    if (mesh != null) meshes.Add(mesh);
                }
            }
        
            foreach (var child in node.VisualChildren)
            {
                ProcessNode(child, meshes);
            }
        }
        
        private GLBMesh ConvertPrimitiveToGLBMesh(MeshPrimitive primitive, string meshName)
        {
            var positionsAccessor = primitive.GetVertexAccessor("POSITION");
            if (positionsAccessor == null) return null;
            var positions = positionsAccessor.AsVector3Array().ToArray();
        
            var normalsAccessor = primitive.GetVertexAccessor("NORMAL");
            var normals = normalsAccessor != null ? normalsAccessor.AsVector3Array().ToArray() : new System.Numerics.Vector3[positions.Length];
            if (normals.Length > 0)
            {
                bool allZero = true;
                for (int j = 0; j < normals.Length; j++)
                {
                    if (normals[j] != System.Numerics.Vector3.Zero) { allZero = false; break; }
                }
                if (allZero)
                {
                    for (int j = 0; j < normals.Length; j++) normals[j] = System.Numerics.Vector3.UnitZ;
                }
            }
        
            var texCoordsAccessor = primitive.GetVertexAccessor("TEXCOORD_0");
            var texCoords = texCoordsAccessor != null ? texCoordsAccessor.AsVector2Array().ToArray() : new System.Numerics.Vector2[positions.Length];
        
            var indices = primitive.GetIndices();
            if (indices == null)
            {
                var temp = new uint[positions.Length];
                for (uint i = 0; i < temp.Length; i++) temp[i] = i;
                indices = temp;
            }

            string texturePath = "default.png";
            var material = primitive.Material;
            if (material != null)
            {
                var baseColorChannel = material.FindChannel("BaseColor");
                if (baseColorChannel.HasValue && baseColorChannel.Value.Texture != null)
                {
                    var image = baseColorChannel.Value.Texture.PrimaryImage;
                    if (image != null)
                    {
                        texturePath = image.Name ?? "embedded_texture";
                        Console.WriteLine($"GLB texture: {texturePath}");
                    }
                }
            }

            var vertices = new GLBVertex[positions.Length];
            var glbNormals = new GLBNormal[normals.Length];
            var glbTexCoords = new GLBTexCoord[texCoords.Length];
        
            const float SCALE = 100f; // Scale glTF units to MuOnline units
            for (int i = 0; i < positions.Length; i++)
            {
                // Convert from glTF right-handed Y up to XNA left-handed Y up
                var pos = new System.Numerics.Vector3(positions[i].X * SCALE, positions[i].Y * SCALE, -positions[i].Z * SCALE);
                vertices[i] = new GLBVertex
                {
                    Node = -1, // No bones for GLB models
                    Position = pos
                };
            }
        
            for (int i = 0; i < normals.Length; i++)
            {
                // Convert normal Z
                var normal = new System.Numerics.Vector3(normals[i].X, normals[i].Y, -normals[i].Z);
                glbNormals[i] = new GLBNormal
                {
                    Node = -1,
                    Normal = normal,
                    BindVertex = (short)i
                };
            }
        
            for (int i = 0; i < texCoords.Length; i++)
            {
                glbTexCoords[i] = new GLBTexCoord
                {
                    U = texCoords[i].X,
                    V = texCoords[i].Y
                };
            }
        
            // Assume triangles
            var triangles = new GLBTriangle[indices.Count / 3];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = new GLBTriangle
                {
                    Polygon = 3,
                    VertexIndex = new short[] { (short)indices[i * 3], (short)indices[i * 3 + 1], (short)indices[i * 3 + 2], 0 },
                    NormalIndex = new short[] { (short)indices[i * 3], (short)indices[i * 3 + 1], (short)indices[i * 3 + 2], 0 },
                    TexCoordIndex = new short[] { (short)indices[i * 3], (short)indices[i * 3 + 1], (short)indices[i * 3 + 2], 0 },
                    LightMapCoord = new GLBTexCoord[4],
                    LightMapIndexes = 0
                };
            }
        
            return new GLBMesh
            {
                Vertices = vertices,
                Normals = glbNormals,
                TexCoords = glbTexCoords,
                Triangles = triangles,
                Texture = 0,
                TexturePath = texturePath
            };
        }
    }
}